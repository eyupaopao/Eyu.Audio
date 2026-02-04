using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Alsa;

public class ALSAApi
{
    static readonly object PlaybackInitializationLock = new();
    static readonly object RecordingInitializationLock = new();
    static readonly object MixerInitializationLock = new();

    public SoundDeviceSettings Settings
    {
        get;
    }

    public WaveFormat GetFormat(bool capture)
    {
        nint pcm = default;
        nint parameters = default;
        snd_pcm_stream_t streamType = capture ? snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE : snd_pcm_stream_t.SND_PCM_STREAM_PLAYBACK;
        string deviceName = capture ? Settings.RecordingDeviceName : Settings.PlaybackDeviceName;

        // Get the requested settings
        uint requestedSampleRate, requestedChannels;
        ushort requestedBitsPerSample;
        
        if (capture)
        {
            requestedSampleRate = Settings.RecordingSampleRate;
            requestedChannels = Settings.RecordingChannels;
            requestedBitsPerSample = Settings.RecordingBitsPerSample;
        }
        else
        {
            requestedSampleRate = Settings.PlaybackSampleRate;
            requestedChannels = Settings.PlaybackChannels;
            requestedBitsPerSample = Settings.PlaybackBitsPerSample;
        }

        try
        {
            // Open PCM device
            int err = InteropAlsa.snd_pcm_open(ref pcm, deviceName, streamType, 0);
            if (err < 0)
            {
                // If we can't open the device, return the requested format
                // In a real scenario, we might want to try other devices or return common defaults
                return new WaveFormat((int)requestedSampleRate, requestedBitsPerSample, (int)requestedChannels);
            }

            // Allocate hardware parameters
            err = InteropAlsa.snd_pcm_hw_params_malloc(ref parameters);
            if (err < 0)
            {
                // If we can't allocate parameters, return the requested format
                return new WaveFormat((int)requestedSampleRate, requestedBitsPerSample, (int)requestedChannels);
            }

            // Fill hardware parameters with default values
            err = InteropAlsa.snd_pcm_hw_params_any(pcm, parameters);
            if (err < 0)
            {
                // If we can't get default parameters, return the requested format
                return new WaveFormat((int)requestedSampleRate, requestedBitsPerSample, (int)requestedChannels);
            }

            // Try to set the access type
            err = InteropAlsa.snd_pcm_hw_params_set_access(pcm, parameters, snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED);
            if (err < 0)
            {
                // If we can't set access type, return the requested format
                return new WaveFormat((int)requestedSampleRate, requestedBitsPerSample, (int)requestedChannels);
            }

            // Try to set the format based on bits per sample
            snd_pcm_format_t formatType = requestedBitsPerSample switch
            {
                8 => snd_pcm_format_t.SND_PCM_FORMAT_U8,
                16 => snd_pcm_format_t.SND_PCM_FORMAT_S16_LE,
                24 => snd_pcm_format_t.SND_PCM_FORMAT_S24_LE,
                32 => snd_pcm_format_t.SND_PCM_FORMAT_S32_LE,
                _ => snd_pcm_format_t.SND_PCM_FORMAT_S16_LE // Default to 16-bit
            };

            err = InteropAlsa.snd_pcm_hw_params_set_format(pcm, parameters, formatType);
            ushort actualBitsPerSample = requestedBitsPerSample;
            if (err < 0)
            {
                // If the requested format isn't supported, try common alternatives in order of preference
                snd_pcm_format_t[] formatsToTry = {
                    snd_pcm_format_t.SND_PCM_FORMAT_S16_LE,  // 16-bit is most commonly supported
                    snd_pcm_format_t.SND_PCM_FORMAT_U8,    // 8-bit as fallback
                    snd_pcm_format_t.SND_PCM_FORMAT_S24_LE, // 24-bit if supported
                    snd_pcm_format_t.SND_PCM_FORMAT_S32_LE  // 32-bit if supported
                };
                
                bool formatFound = false;
                foreach (var fmt in formatsToTry)
                {
                    err = InteropAlsa.snd_pcm_hw_params_set_format(pcm, parameters, fmt);
                    if (err >= 0)
                    {
                        formatType = fmt;
                        actualBitsPerSample = fmt switch
                        {
                            snd_pcm_format_t.SND_PCM_FORMAT_U8 => 8,
                            snd_pcm_format_t.SND_PCM_FORMAT_S16_LE => 16,
                            snd_pcm_format_t.SND_PCM_FORMAT_S24_LE => 24,
                            snd_pcm_format_t.SND_PCM_FORMAT_S32_LE => 32,
                            _ => 16
                        };
                        formatFound = true;
                        break;
                    }
                }
                
                // If no format is supported, return the requested format
                if (!formatFound)
                {
                    return new WaveFormat((int)requestedSampleRate, requestedBitsPerSample, (int)requestedChannels);
                }
            }
            else
            {
                // Format was set successfully, update actual bits per sample
                actualBitsPerSample = formatType switch
                {
                    snd_pcm_format_t.SND_PCM_FORMAT_U8 => 8,
                    snd_pcm_format_t.SND_PCM_FORMAT_S16_LE => 16,
                    snd_pcm_format_t.SND_PCM_FORMAT_S24_LE => 24,
                    snd_pcm_format_t.SND_PCM_FORMAT_S32_LE => 32,
                    _ => 16
                };
            }

            // Try to set the number of channels
            // We'll try the requested number first, then fall back to defaults if it fails
            err = InteropAlsa.snd_pcm_hw_params_set_channels(pcm, parameters, requestedChannels);
            uint actualChannels = requestedChannels;
            if (err < 0)
            {
                // If requested channels fail, try common values in order of preference
                uint[] commonChannels = { 2, 1, 4, 6, 8 }; // Stereo, mono, then surround sound configs
                foreach (uint ch in commonChannels)
                {
                    err = InteropAlsa.snd_pcm_hw_params_set_channels(pcm, parameters, ch);
                    if (err >= 0)
                    {
                        actualChannels = ch;
                        break;
                    }
                }
                
                // If no common channel count works, return the requested format
                if (err < 0)
                {
                    return new WaveFormat((int)requestedSampleRate, actualBitsPerSample, (int)requestedChannels);
                }
            }

            // Try to set the sample rate
            // We'll use the set_rate_near function that is available
            uint actualSampleRate = requestedSampleRate;
            int dir = 0;
            unsafe
            {
                uint* actualSampleRatePtr = &actualSampleRate;
                int* dirPtr = &dir;
                err = InteropAlsa.snd_pcm_hw_params_set_rate_near(pcm, parameters, actualSampleRatePtr, dirPtr);
            }
            
            // If setting the exact rate fails, try common sample rates in order of preference
            if (err < 0)
            {
                uint[] commonRates = { 44100, 48000, 32000, 22050, 16000, 11025, 8000 };
                foreach (uint rate in commonRates)
                {
                    uint testRate = rate;
                    int testDir = 0;
                    unsafe
                    {
                        uint* testRatePtr = &testRate;
                        int* testDirPtr = &testDir;
                        err = InteropAlsa.snd_pcm_hw_params_set_rate_near(pcm, parameters, testRatePtr, testDirPtr);
                    }
                    
                    if (err >= 0)
                    {
                        actualSampleRate = testRate;
                        break;
                    }
                }
                
                // If no common rate works, return the requested format
                if (err < 0)
                {
                    return new WaveFormat((int)requestedSampleRate, actualBitsPerSample, (int)actualChannels);
                }
            }

            // Apply the hardware parameters
            err = InteropAlsa.snd_pcm_hw_params(pcm, parameters);
            if (err < 0)
            {
                // If applying parameters fails, return the requested format
                return new WaveFormat((int)requestedSampleRate, actualBitsPerSample, (int)actualChannels);
            }

            // Return the actual supported format
            return new WaveFormat((int)actualSampleRate, actualBitsPerSample, (int)actualChannels);
        }
        finally
        {
            // Clean up
            if (parameters != default)
            {
                // Since snd_pcm_hw_params_free doesn't exist, we'll just set to default
                parameters = default;
            }
            if (pcm != default)
            {
                InteropAlsa.snd_pcm_close(pcm);
            }
        }
    }

    public long PlaybackVolume
    {
        get => GetPlaybackVolume(); set => SetPlaybackVolume(value);
    }
    public bool PlaybackMute
    {
        get => _playbackMute; set => SetPlaybackMute(value);
    }
    public long RecordingVolume
    {
        get => GetRecordingVolume(); set => SetRecordingVolume(value);
    }
    public bool RecordingMute
    {
        get => _recordingMute; set => SetRecordingMute(value);
    }

    bool _playbackMute;
    bool _recordingMute;
    nint _playbackPcm;
    nint _recordingPcm;
    nint _loopbackPcm;
    nint _mixer;
    nint _mixelElement;
    bool _wasDisposed;

    public ALSAApi(SoundDeviceSettings settings)
    {
        Settings = settings;
    }
    #region play
    public void Play(IWaveProvider waveProvider, CancellationToken cancellationToken)
    {
        Console.WriteLine("start play");
        Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(ALSAApi));

            var parameter = new nint();
            var dir = 0;
            var header = WavHeader.Build((uint)waveProvider.WaveFormat.SampleRate, (ushort)waveProvider.WaveFormat.Channels, (ushort)waveProvider.WaveFormat.BitsPerSample);

            Console.WriteLine("start play open");
            OpenPlaybackPcm();
            Console.WriteLine("start init");
            PcmInitialize(_playbackPcm, header, ref parameter, ref dir);
            Console.WriteLine("start play write");
            WriteStream(waveProvider, header, ref parameter, ref dir, cancellationToken);
            Console.WriteLine("start close");
            ClosePlaybackPcm();
        });
    }

    unsafe void WriteStream(IWaveProvider wavStream, WavHeader header, ref nint @params, ref int dir, CancellationToken cancellationToken)
    {
        PlaybackState = PlaybackState.Playing;
        ulong frames;

        fixed (int* dirP = &dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_get_period_size(@params, &frames, dirP), ExceptionMessages.CanNotGetPeriodSize);

        var bufferSize = frames * header.BlockAlign;
        var readBuffer = new byte[(int)bufferSize];
        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        try
        {
            Console.WriteLine("start play while");
            while (!_wasDisposed && !cancellationToken.IsCancellationRequested)
            {
                if (PlaybackState == PlaybackState.Stopped)
                {
                    Dispose();
                    break;
                }
                else if (PlaybackState == PlaybackState.Paused)
                {
                    Console.WriteLine("start play pause");
                    Thread.Sleep(1);
                    continue;
                }
                int read = wavStream.Read(readBuffer, 0, readBuffer.Length);
                if (read == 0)
                {
                    Console.WriteLine("start play read 0");
                    Thread.Sleep(1);
                    continue;
                }

                // 只复制实际读取的数据
                Marshal.Copy(readBuffer, 0, buffer, read);

                //Console.WriteLine("start play write");
                ThrowErrorMessage(InteropAlsa.snd_pcm_writei(_playbackPcm, (nint)buffer, (ulong)(read / header.BlockAlign)), ExceptionMessages.CanNotWriteToDevice);
                InteropAlsa.snd_pcm_start(_playbackPcm);
                //Thread.Sleep(1);
                //if (!started)
                //{
                //    writeCount++;
                //    if (writeCount == 10)
                //    {
                //
                //        started = true;
                //    }
                //}

                //Console.WriteLine("start play write 1");
            }
            PlaybackState = PlaybackState.Stopped;
            Console.WriteLine("play end");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public PlaybackState PlaybackState
    {
        get; private set;
    }
    public void Pause()
    {
        if (PlaybackState == PlaybackState.Paused)
            return;
        PlaybackState = PlaybackState.Paused;
        InteropAlsa.snd_pcm_pause(_playbackPcm, 1);
    }
    public void Stop()
    {
        PlaybackState = PlaybackState.Stopped;
    }

    public void Resume()
    {
        if (PlaybackState == PlaybackState.Playing)
            return;
        InteropAlsa.snd_pcm_resume(_playbackPcm);
        PlaybackState = PlaybackState.Playing;
    }

    public void Play(string wavPath)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(ALSAApi));

        using var fs = File.Open(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Play(fs, CancellationToken.None);
    }

    public void Play(string wavPath, CancellationToken cancellationToken)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(ALSAApi));

        using var fs = File.Open(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Play(fs, cancellationToken);
    }

    public void Play(Stream wavStream)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(ALSAApi));

        Play(wavStream, CancellationToken.None);
    }

    public void Play(Stream wavStream, CancellationToken cancellationToken)
    {
        Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(ALSAApi));

            var parameter = new nint();
            var dir = 0;
            var header = WavHeader.FromStream(wavStream);

            OpenPlaybackPcm();
            PcmInitialize(_playbackPcm, header, ref parameter, ref dir);
            WriteStream(wavStream, header, ref parameter, ref dir, cancellationToken);           
            Dispose();
        });
    }

    unsafe void WriteStream(Stream wavStream, WavHeader header, ref nint @params, ref int dir, CancellationToken cancellationToken)
    {
        ulong frames;

        fixed (int* dirP = &dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_get_period_size(@params, &frames, dirP), ExceptionMessages.CanNotGetPeriodSize);

        var bufferSize = frames * header.BlockAlign;
        var readBuffer = new byte[(int)bufferSize];

        fixed (byte* buffer = readBuffer)
        {
            while (!_wasDisposed && !cancellationToken.IsCancellationRequested)
            {
                int read = wavStream.Read(readBuffer, 0, readBuffer.Length);
                if (read <= 0)
                    break;

                ThrowErrorMessage(InteropAlsa.snd_pcm_writei(_playbackPcm, (nint)buffer, (ulong)(read / header.BlockAlign)), ExceptionMessages.CanNotWriteToDevice);
            }
        }
    }



    void OpenPlaybackPcm()
    {
        if (_playbackPcm != default)
            return;

        lock (PlaybackInitializationLock)
            ThrowErrorMessage(InteropAlsa.snd_pcm_open(ref _playbackPcm, Settings.PlaybackDeviceName, snd_pcm_stream_t.SND_PCM_STREAM_PLAYBACK, 0), ExceptionMessages.CanNotOpenPlayback);
    }

    void ClosePlaybackPcm()
    {
        if (_playbackPcm == default)
            return;

        //ThrowErrorMessage(InteropAlsa.snd_pcm_drain(_playbackPcm), ExceptionMessages.CanNotDropDevice);
        ThrowErrorMessage(InteropAlsa.snd_pcm_close(_playbackPcm), ExceptionMessages.CanNotCloseDevice);

        _playbackPcm = default;
    }


    #endregion

    #region record   
    public void Record(Action<byte[]> onDataAvailable, CancellationToken token)
    {
        _ = Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(ALSAApi));

            var parameters = new nint();
            var dir = 0;

            var header = WavHeader.Build(Settings.RecordingSampleRate, Settings.RecordingChannels, Settings.RecordingBitsPerSample);
            using (var memoryStream = new MemoryStream())
            {
                header.WriteToStream(memoryStream);
                onDataAvailable?.Invoke(memoryStream.ToArray());
            }

            OpenRecordingPcm();
            PcmInitialize(_recordingPcm, header, ref parameters, ref dir);
            ReadStream(onDataAvailable, header, ref parameters, ref dir, token);            
            Dispose();
        });
    }




    void OpenRecordingPcm()
    {
        if (_recordingPcm != default)
            return;

        lock (RecordingInitializationLock)
            ThrowErrorMessage(InteropAlsa.snd_pcm_open(ref _recordingPcm, Settings.RecordingDeviceName, snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE, 0), ExceptionMessages.CanNotOpenRecording);
    }

    void CloseRecordingPcm()
    {
        if (_recordingPcm == default)
            return;
        // ThrowErrorMessage(InteropAlsa.snd_pcm_drain(_recordingPcm), ExceptionMessages.CanNotDropDevice);
        ThrowErrorMessage(InteropAlsa.snd_pcm_close(_recordingPcm), ExceptionMessages.CanNotCloseDevice);
        _recordingPcm = default;
    }

    public void StopRecording()
    {
        // Only dispose if not already disposed
        if (!_wasDisposed)
        {
            Dispose();
        }
    }

    #endregion

    #region loopback

    public void Loopback(Action<byte[]> onDataAvailable, CancellationToken token)
    {
        _ = Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(ALSAApi));

            var parameters = new nint();
            var dir = 0;

            var header = WavHeader.Build(Settings.RecordingSampleRate, Settings.RecordingChannels, Settings.RecordingBitsPerSample);
            using (var memoryStream = new MemoryStream())
            {
                header.WriteToStream(memoryStream);
                onDataAvailable?.Invoke(memoryStream.ToArray());
            }

            OpenLoopbackPcm();
            PcmInitialize(_loopbackPcm, header, ref parameters, ref dir);
            ReadStream(onDataAvailable, header, ref parameters, ref dir, token);           
            Dispose();
        });
    }


    void CloseLoopbackPcm()
    {
        if (_loopbackPcm == default)
            return;
        ThrowErrorMessage(InteropAlsa.snd_pcm_close(_loopbackPcm), ExceptionMessages.CanNotCloseDevice);
        _loopbackPcm = default;
    }

    private void OpenLoopbackPcm()
    {
        if (_loopbackPcm != default)
            return;
        lock (RecordingInitializationLock)
            ThrowErrorMessage(InteropAlsa.snd_pcm_open(ref _loopbackPcm, Settings.RecordingDeviceName, snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE, 0), ExceptionMessages.CanNotOpenRecording);
    }


    #endregion

    unsafe void ReadStream(Action<byte[]>? onDataAvailable, WavHeader header, ref nint @params, ref int dir, CancellationToken cancellationToken)
    {
        ulong frames;

        fixed (int* dirP = &dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_get_period_size(@params, &frames, dirP), ExceptionMessages.CanNotGetPeriodSize);

        var bufferSize = frames * header.BlockAlign;
        var readBuffer = new byte[(int)bufferSize];

        fixed (byte* buffer = readBuffer)
        {
            while (!_wasDisposed && !cancellationToken.IsCancellationRequested)
            {
                ThrowErrorMessage(InteropAlsa.snd_pcm_readi(_recordingPcm, (nint)buffer, frames), ExceptionMessages.CanNotReadFromDevice);
                onDataAvailable?.Invoke(readBuffer);
            }
        }
    }

    unsafe void PcmInitialize(nint pcm, WavHeader header, ref nint @params, ref int dir)
    {
        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_malloc(ref @params), ExceptionMessages.CanNotAllocateParameters);
        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_any(pcm, @params), ExceptionMessages.CanNotFillParameters);
        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_access(pcm, @params, snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED), ExceptionMessages.CanNotSetAccessMode);

        var formatResult = (header.BitsPerSample / 8) switch
        {
            1 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_U8),
            2 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S16_LE),
            3 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S24_LE),
            _ => throw new AlsaDeviceException(ExceptionMessages.BitsPerSampleError)
        };
        ThrowErrorMessage(formatResult, ExceptionMessages.CanNotSetFormat);

        // Try to set the requested number of channels, but fall back to 1 if that fails
        int channelsResult = InteropAlsa.snd_pcm_hw_params_set_channels(pcm, @params, header.NumChannels);
        if (channelsResult < 0)
        {
            // If the requested number of channels fails, try to set to 1 channel as a fallback
            int fallbackResult = InteropAlsa.snd_pcm_hw_params_set_channels(pcm, @params, 1);
            if (fallbackResult < 0)
            {
                ThrowErrorMessage(channelsResult, ExceptionMessages.CanNotSetChannel);
            }
        }

        var val = header.SampleRate;
        fixed (int* dirP = &dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_rate_near(pcm, @params, &val, dirP), ExceptionMessages.CanNotSetRate);

        //uint bufferSize = (uint)(header.BlockAlign * 50);
        //InteropAlsa.snd_pcm_hw_params_set_buffer_size_near(pcm, @params, new(&bufferSize));

        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params(pcm, @params), ExceptionMessages.CanNotSetHwParams);
    }


    #region set properties
    void SetPlaybackVolume(long volume)
    {
        OpenMixer();
        
        if (_mixelElement != default)
        {
            // Check if the element has playback volume capability
            if (InteropAlsa.snd_mixer_selem_has_playback_volume(_mixelElement) > 0)
            {
                ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, volume), ExceptionMessages.CanNotSetVolume);
                ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, volume), ExceptionMessages.CanNotSetVolume);
            }
        }

        CloseMixer();
    }

    unsafe long GetPlaybackVolume()
    {
        long volumeLeft = 0;
        long volumeRight = 0;

        OpenMixer();

        if (_mixelElement != default && InteropAlsa.snd_mixer_selem_has_playback_volume(_mixelElement) > 0)
        {
            ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, &volumeLeft), ExceptionMessages.CanNotSetVolume);
            ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, &volumeRight), ExceptionMessages.CanNotSetVolume);
        }

        CloseMixer();

        return (volumeLeft + volumeRight) / 2;
    }

    void SetRecordingVolume(long volume)
    {
        OpenMixer();
        
        if (_mixelElement != default)
        {
            // Check if the element has capture volume capability
            if (InteropAlsa.snd_mixer_selem_has_capture_volume(_mixelElement) > 0)
            {
                ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, volume), ExceptionMessages.CanNotSetVolume);
                ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, volume), ExceptionMessages.CanNotSetVolume);
            }
        }

        CloseMixer();
    }

    unsafe long GetRecordingVolume()
    {
        long volumeLeft = 0;
        long volumeRight = 0;

        OpenMixer();

        if (_mixelElement != default && InteropAlsa.snd_mixer_selem_has_capture_volume(_mixelElement) > 0)
        {
            ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, &volumeLeft), ExceptionMessages.CanNotSetVolume);
            ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, &volumeRight), ExceptionMessages.CanNotSetVolume);
        }

        CloseMixer();

        return (volumeLeft + volumeRight) / 2;
    }

    void SetPlaybackMute(bool isMute)
    {
        _playbackMute = isMute;

        OpenMixer();

        if (_mixelElement != default)
        {
            // Check if the element has playback switch capability
            if (InteropAlsa.snd_mixer_selem_has_playback_switch(_mixelElement) > 0)
            {
                ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_playback_switch_all(_mixelElement, isMute ? 0 : 1), ExceptionMessages.CanNotSetMute);
            }
        }

        CloseMixer();
    }

    void SetRecordingMute(bool isMute)
    {
        _recordingMute = isMute;

        OpenMixer();

        if (_mixelElement != default)
        {
            // Check if the element has capture switch capability
            if (InteropAlsa.snd_mixer_selem_has_capture_switch(_mixelElement) > 0)
            {
                ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_capture_switch_all(_mixelElement, isMute ? 0 : 1), ExceptionMessages.CanNotSetMute);
            }
        }

        CloseMixer();
    }

    #endregion


    void OpenMixer()
    {
        if (_mixer != default)
            return;

        lock (MixerInitializationLock)
        {
            ThrowErrorMessage(InteropAlsa.snd_mixer_open(ref _mixer, 0), ExceptionMessages.CanNotOpenMixer);
            ThrowErrorMessage(InteropAlsa.snd_mixer_attach(_mixer, Settings.MixerDeviceName), ExceptionMessages.CanNotAttachMixer);
            ThrowErrorMessage(InteropAlsa.snd_mixer_selem_register(_mixer, nint.Zero, nint.Zero), ExceptionMessages.CanNotRegisterMixer);
            ThrowErrorMessage(InteropAlsa.snd_mixer_load(_mixer), ExceptionMessages.CanNotLoadMixer);

            _mixelElement = InteropAlsa.snd_mixer_first_elem(_mixer);
        }
    }

    void CloseMixer()
    {
        if (_mixer == default)
            return;

        ThrowErrorMessage(InteropAlsa.snd_mixer_close(_mixer), ExceptionMessages.CanNotCloseMixer);

        _mixer = default;
        _mixelElement = default;
    }

    public async Task Dispose()
    {
        if (_wasDisposed)
            return;        
        _wasDisposed = true;
        await Task.Delay(100);
        ClosePlaybackPcm();
        CloseLoopbackPcm();
        CloseRecordingPcm();
    }

    void ThrowErrorMessage(int errorNum, string message)
    {
        if (errorNum >= 0)
            return;

        var errorMsg = Marshal.PtrToStringAnsi(InteropAlsa.snd_strerror(errorNum));
        throw new AlsaDeviceException($"{message}. Error {errorNum}. {errorMsg}.");
    }

}
