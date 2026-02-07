using Eyu.Audio.Alsa;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Eyu.Audio.Utils
{
    /// <summary>
    /// 通过 ALSA 枚举真实音频硬件设备（使用 arecord -l / aplay -l 解析，避免 -L 列表中的插件/描述行）
    /// </summary>
    public static class AlsaDeviceEnumerator
    {
        // arecord -l / aplay -l 中硬件设备行格式: "card 0: AudioPCI [Ensoniq AudioPCI], device 0: ES1371/1 [ES1371 DAC2/ADC]"
        private static readonly Regex HardwareDeviceLineRegex = new Regex(
            @"card\s+(\d+):\s*([^,[\]]+)(?:\s*\[([^\]]*)\])?\s*,\s*device\s+(\d+):\s*([^,[\]]+)(?:\s*\[([^\]]*)\])?",
            RegexOptions.Compiled);

        /// <summary>
        /// 执行外部命令并返回标准输出
        /// </summary>
        private static string? RunCommand(string fileName, string arguments, int timeoutMs = 5000)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(startInfo);
                var output = process?.StandardOutput.ReadToEnd();
                process?.WaitForExit(timeoutMs);
                return output;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从 arecord -L / aplay -L 输出中解析 "default" 设备对应的描述（下一行内容），用于显示默认设备名称。
        /// </summary>
        private static string GetDefaultDeviceDescription(string? output)
        {
            if (string.IsNullOrWhiteSpace(output)) return "默认 (Default)";
            var lines = output.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                // 设备名行：行首无空格，且内容为 default
                var line = lines[i];
                if (line.Length == 0 || line[0] == ' ' || line[0] == '\t') continue;
                if (!string.Equals(line.Trim(), "default", StringComparison.OrdinalIgnoreCase)) continue;
                // 下一行即描述（可能带缩进）
                if (i + 1 < lines.Length)
                {
                    var desc = lines[i + 1].Trim();
                    if (!string.IsNullOrEmpty(desc)) return desc;
                }
                break;
            }
            return "默认 (Default)";
        }

        /// <summary>
        /// 解析 arecord -l / aplay -l 输出，提取真实硬件设备（card N, device M）
        /// </summary>
        private static List<AudioDevice> ParseHardwareList(string? output, bool isCapture)
        {
            var list = new List<AudioDevice>();
            if (string.IsNullOrWhiteSpace(output)) return list;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var match = HardwareDeviceLineRegex.Match(line.Trim());
                if (!match.Success) continue;

                var cardIndex = match.Groups[1].Value;
                var cardShort = match.Groups[2].Value.Trim();
                var cardLong = match.Groups[3].Success ? match.Groups[3].Value.Trim() : cardShort;
                var devIndex = match.Groups[4].Value;
                var devShort = match.Groups[5].Value.Trim();
                var devLong = match.Groups[6].Success ? match.Groups[6].Value.Trim() : devShort;

                var deviceId = $"hw:{cardIndex},{devIndex}";
                var displayName = $"{cardLong} - {devLong}";

                list.Add(new AudioDevice
                {
                    Device = deviceId,
                    Name = displayName,
                    IsCapture = isCapture,
                    DriverType = DriverType.Alsa
                });
            }
            return list;
        }

        /// <summary>
        /// 枚举录音（采集）设备：默认设备 + 真实硬件设备列表
        /// </summary>
        /// <returns>可用的录音设备列表</returns>
        public static List<AudioDevice> GetCaptureDevices()
        {
            var devices = new List<AudioDevice>();

            var defaultDesc = GetDefaultDeviceDescription(RunCommand("arecord", "-L"));
            devices.Add(new AudioDevice
            {
                Device = "default",
                Name = defaultDesc,
                IsCapture = true,
                DriverType = DriverType.Alsa
            });

            var output = RunCommand("arecord", "-l");
            var hardware = ParseHardwareList(output, isCapture: true);
            foreach (var dev in hardware)
                devices.Add(dev);

            if (devices.Count == 1)
                devices[0].Name = string.IsNullOrEmpty(devices[0].Name) ? "Default ALSA Device" : devices[0].Name;

            return devices;
        }

        /// <summary>
        /// 枚举播放设备：默认设备 + 真实硬件设备列表
        /// </summary>
        /// <returns>可用的播放设备列表</returns>
        public static List<AudioDevice> GetPlaybackDevices()
        {
            var devices = new List<AudioDevice>();

            var defaultDesc = GetDefaultDeviceDescription(RunCommand("aplay", "-L"));
            devices.Add(new AudioDevice
            {
                Device = "default",
                Name = defaultDesc,
                IsCapture = false,
                DriverType = DriverType.Alsa
            });

            var output = RunCommand("aplay", "-l");
            var hardware = ParseHardwareList(output, isCapture: false);
            foreach (var dev in hardware)
                devices.Add(dev);

            if (devices.Count == 1)
                devices[0].Name = string.IsNullOrEmpty(devices[0].Name) ? "Default ALSA Device" : devices[0].Name;

            return devices;
        }

        /// <summary>
        /// 枚举渲染设备（与播放设备相同）
        /// </summary>
        public static List<AudioDevice> GetRenderDevices() => GetPlaybackDevices();
        
        /// <summary>
        /// Gets only the default capture device using ALSA
        /// </summary>
        /// <returns>The default capture device</returns>
        public static List<AudioDevice> GetDefaultCaptureDevice()
        {
            var allDevices = GetCaptureDevices();
            var defaultDevice = allDevices.FirstOrDefault(d => d.Device == "default") ?? 
                              allDevices.FirstOrDefault() ?? 
                              new AudioDevice
                              {
                                  Device = "default",
                                  Name = "Default ALSA Device",
                                  IsCapture = true,
                                  DriverType = DriverType.Alsa
                              };
            return new List<AudioDevice> { defaultDevice };
        }
        
        /// <summary>
        /// Gets only the default render device using ALSA
        /// </summary>
        /// <returns>The default render device</returns>
        public static List<AudioDevice> GetDefaultRenderDevice()
        {
            var allDevices = GetRenderDevices();
            var defaultDevice = allDevices.FirstOrDefault(d => d.Device == "default") ?? 
                              allDevices.FirstOrDefault() ?? 
                              new AudioDevice
                              {
                                  Device = "default",
                                  Name = "Default ALSA Device",
                                  IsCapture = false,
                                  DriverType = DriverType.Alsa
                              };
            return new List<AudioDevice> { defaultDevice };
        }
        
        /// <summary>
        /// 检查指定设备是否存在且可访问（支持 default 与 hw:card,device）
        /// </summary>
        public static bool DeviceExists(string deviceName, bool isCapture = true)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return false;
            if (string.Equals(deviceName, "default", StringComparison.OrdinalIgnoreCase)) return true;

            var output = RunCommand(isCapture ? "arecord" : "aplay", "-l");
            var list = ParseHardwareList(output, isCapture);
            return list.Any(d => string.Equals(d.Device, deviceName, StringComparison.Ordinal));
        }
    }
}
