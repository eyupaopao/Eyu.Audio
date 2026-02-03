using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Alsa;

class UnixSoundDevice : ISoundDevice
{
    static readonly object PlaybackInitializationLock = new();
    static readonly object RecordingInitializationLock = new();
    static readonly object MixerInitializationLock = new();

    public SoundDeviceSettings Settings
    {
        get;
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

    public UnixSoundDevice(SoundDeviceSettings settings)
    {
        Settings = settings;
    }


    public void Play(IWaveProvider waveProvider, CancellationToken cancellationToken)
    {
        Console.WriteLine("start play");
        Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(UnixSoundDevice));

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
            throw new ObjectDisposedException(nameof(UnixSoundDevice));

        using var fs = File.Open(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Play(fs, CancellationToken.None);
    }

    public void Play(string wavPath, CancellationToken cancellationToken)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(UnixSoundDevice));

        using var fs = File.Open(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Play(fs, cancellationToken);
    }

    public void Play(Stream wavStream)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(UnixSoundDevice));

        Play(wavStream, CancellationToken.None);
    }

    public void Play(Stream wavStream, CancellationToken cancellationToken)
    {
        Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(UnixSoundDevice));

            var parameter = new nint();
            var dir = 0;
            var header = WavHeader.FromStream(wavStream);

            OpenPlaybackPcm();
            PcmInitialize(_playbackPcm, header, ref parameter, ref dir);
            WriteStream(wavStream, header, ref parameter, ref dir, cancellationToken);
            ClosePlaybackPcm();
        });
    }

    public void Record(uint second, string savePath)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(UnixSoundDevice));

        using var fs = File.Open(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
        Record(second, fs);

    }

    public void Record(uint second, Stream saveStream)
    {
        if (_wasDisposed)
            throw new ObjectDisposedException(nameof(UnixSoundDevice));

        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(second));
        Record(saveStream, tokenSource.Token);
    }

    public void Record(Stream saveStream, CancellationToken token)
    {
        _ = Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(UnixSoundDevice));

            var parameters = new nint();
            var dir = 0;
            var header = WavHeader.Build(Settings.RecordingSampleRate, Settings.RecordingChannels, Settings.RecordingBitsPerSample);
            header.WriteToStream(saveStream);

            OpenRecordingPcm();
            PcmInitialize(_recordingPcm, header, ref parameters, ref dir);
            ReadStream(saveStream, header, ref parameters, ref dir, token);
            CloseRecordingPcm();
        });
    }

    public void Record(Action<byte[]> onDataAvailable, CancellationToken token)
    {
        _ = Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(UnixSoundDevice));

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
            CloseRecordingPcm();
        });
    }

    public void Loopback(Action<byte[]> onDataAvailable, CancellationToken token)
    {
        _ = Task.Run(() =>
        {
            if (_wasDisposed)
                throw new ObjectDisposedException(nameof(UnixSoundDevice));

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
            CloseLoopbackPcm();
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

    unsafe void ReadStream(Stream saveStream, WavHeader header, ref nint @params, ref int dir, CancellationToken cancellationToken)
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
                saveStream.Write(readBuffer);
            }
        }

        saveStream.Flush();
    }

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

        var formatResult = (header.BitsPerSample / 8) switch {
            1 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_U8),
            2 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S16_LE),
            3 => InteropAlsa.snd_pcm_hw_params_set_format(pcm, @params, snd_pcm_format_t.SND_PCM_FORMAT_S24_LE),
            _ => throw new AlsaDeviceException(ExceptionMessages.BitsPerSampleError)
        };
        ThrowErrorMessage(formatResult, ExceptionMessages.CanNotSetFormat);

        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_channels(pcm, @params, header.NumChannels), ExceptionMessages.CanNotSetChannel);

        var val = header.SampleRate;
        fixed (int* dirP = &dir)
            ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params_set_rate_near(pcm, @params, &val, dirP), ExceptionMessages.CanNotSetRate);

        //uint bufferSize = (uint)(header.BlockAlign * 50);
        //InteropAlsa.snd_pcm_hw_params_set_buffer_size_near(pcm, @params, new(&bufferSize));

        ThrowErrorMessage(InteropAlsa.snd_pcm_hw_params(pcm, @params), ExceptionMessages.CanNotSetHwParams);
    }

    void SetPlaybackVolume(long volume)
    {
        OpenMixer();

        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, volume), ExceptionMessages.CanNotSetVolume);
        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, volume), ExceptionMessages.CanNotSetVolume);

        CloseMixer();
    }

    unsafe long GetPlaybackVolume()
    {
        long volumeLeft;
        long volumeRight;

        OpenMixer();

        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, &volumeLeft), ExceptionMessages.CanNotSetVolume);
        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_playback_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, &volumeRight), ExceptionMessages.CanNotSetVolume);

        CloseMixer();

        return (volumeLeft + volumeRight) / 2;
    }

    void SetRecordingVolume(long volume)
    {
        OpenMixer();

        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, volume), ExceptionMessages.CanNotSetVolume);
        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, volume), ExceptionMessages.CanNotSetVolume);

        CloseMixer();
    }

    unsafe long GetRecordingVolume()
    {
        long volumeLeft, volumeRight;

        OpenMixer();

        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_LEFT, &volumeLeft), ExceptionMessages.CanNotSetVolume);
        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_get_capture_volume(_mixelElement, snd_mixer_selem_channel_id.SND_MIXER_SCHN_FRONT_RIGHT, &volumeRight), ExceptionMessages.CanNotSetVolume);

        CloseMixer();

        return (volumeLeft + volumeRight) / 2;
    }

    void SetPlaybackMute(bool isMute)
    {
        _playbackMute = isMute;

        OpenMixer();

        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_playback_switch_all(_mixelElement, isMute ? 0 : 1), ExceptionMessages.CanNotSetMute);

        CloseMixer();
    }

    void SetRecordingMute(bool isMute)
    {
        _recordingMute = isMute;

        OpenMixer();

        ThrowErrorMessage(InteropAlsa.snd_mixer_selem_set_capture_switch_all(_mixelElement, isMute ? 0 : 1), ExceptionMessages.CanNotSetMute);

        CloseMixer();
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

        //ThrowErrorMessage(InteropAlsa.snd_pcm_drain(_recordingPcm), ExceptionMessages.CanNotDropDevice);
        ThrowErrorMessage(InteropAlsa.snd_pcm_close(_recordingPcm), ExceptionMessages.CanNotCloseDevice);

        _recordingPcm = default;
    }

    void CloseLoopbackPcm()
    {
        if (_loopbackPcm == default)
            return;

        //ThrowErrorMessage(InteropAlsa.snd_pcm_drain(_loopbackPcm), ExceptionMessages.CanNotDropDevice);
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

    public void Dispose()
    {
        if (_wasDisposed)
            return;

        _wasDisposed = true;

        ClosePlaybackPcm();
        CloseRecordingPcm();
        CloseMixer();
    }

    void ThrowErrorMessage(int errorNum, string message)
    {
        if (errorNum >= 0)
            return;

        var errorMsg = Marshal.PtrToStringAnsi(InteropAlsa.snd_strerror(errorNum));
        throw new AlsaDeviceException($"{message}. Error {errorNum}. {errorMsg}.");
    }

}
