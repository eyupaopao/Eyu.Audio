using NAudio.Wave;
using NAudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio;




public class SDLOutEvent : IWavePlayer, IDisposable, IWavePosition
{
    private readonly object waveOutLock;

    private readonly SynchronizationContext syncContext;

    private IntPtr hWaveOut;

    private WaveOutBuffer[] buffers;

    private IWaveProvider waveStream;

    private volatile PlaybackState playbackState;

    private AutoResetEvent callbackEvent;

    public int DesiredLatency
    {
        get; set;
    }

    public int NumberOfBuffers
    {
        get; set;
    }

    public int DeviceNumber { get; set; } = -1;


    public WaveFormat OutputWaveFormat => waveStream.WaveFormat;

    public PlaybackState PlaybackState => playbackState;

    public float Volume
    {
        get
        {
            return WaveOutUtils.GetWaveOutVolume(hWaveOut, waveOutLock);
        }
        set
        {
            WaveOutUtils.SetWaveOutVolume(value, hWaveOut, waveOutLock);
        }
    }

    public event EventHandler<StoppedEventArgs> PlaybackStopped;

    public SDLOutEvent()
    {
        syncContext = SynchronizationContext.Current;
        if (syncContext != null && (syncContext.GetType().Name == "LegacyAspNetSynchronizationContext" || syncContext.GetType().Name == "AspNetSynchronizationContext"))
        {
            syncContext = null;
        }
        DesiredLatency = 300;
        NumberOfBuffers = 2;
        waveOutLock = new object();
    }

    public void Init(IWaveProvider waveProvider)
    {
        if (playbackState != 0)
        {
            throw new InvalidOperationException("Can't re-initialize during playback");
        }
        if (hWaveOut != IntPtr.Zero)
        {
            DisposeBuffers();
            CloseWaveOut();
        }
        callbackEvent = new AutoResetEvent(initialState: false);
        waveStream = waveProvider;
        int bufferSize = waveProvider.WaveFormat.ConvertLatencyToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);
        MmResult result;
        lock (waveOutLock)
        {
            result = WaveInterop.waveOutOpenWindow(out hWaveOut, (IntPtr)DeviceNumber, waveStream.WaveFormat, callbackEvent.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackEvent);
        }
        MmException.Try(result, "waveOutOpen");
        buffers = new WaveOutBuffer[NumberOfBuffers];
        playbackState = PlaybackState.Stopped;
        for (int i = 0; i < NumberOfBuffers; i++)
        {
            buffers[i] = new WaveOutBuffer(hWaveOut, bufferSize, waveStream, waveOutLock);
        }
    }

    public void Play()
    {
        if (buffers == null || waveStream == null)
        {
            throw new InvalidOperationException("Must call Init first");
        }
        if (playbackState == PlaybackState.Stopped)
        {
            playbackState = PlaybackState.Playing;
            callbackEvent.Set();
            ThreadPool.QueueUserWorkItem(delegate {
                PlaybackThread();
            }, null);
        }
        else if (playbackState == PlaybackState.Paused)
        {
            Resume();
            callbackEvent.Set();
        }
    }

    private void PlaybackThread()
    {
        Exception e = null;
        try
        {
            DoPlayback();
        }
        catch (Exception ex)
        {
            e = ex;
        }
        finally
        {
            playbackState = PlaybackState.Stopped;
            RaisePlaybackStoppedEvent(e);
        }
    }

    private void DoPlayback()
    {
        while (playbackState != 0)
        {
            if (!callbackEvent.WaitOne(DesiredLatency))
            {
                _ = playbackState;
                _ = 1;
            }
            if (playbackState != PlaybackState.Playing)
            {
                continue;
            }
            int num = 0;
            WaveOutBuffer[] array = buffers;
            foreach (WaveOutBuffer waveOutBuffer in array)
            {
                if (waveOutBuffer.InQueue || waveOutBuffer.OnDone())
                {
                    num++;
                }
            }
            if (num == 0)
            {
                playbackState = PlaybackState.Stopped;
                callbackEvent.Set();
            }
        }
    }

    public void Pause()
    {
        if (playbackState == PlaybackState.Playing)
        {
            playbackState = PlaybackState.Paused;
            MmResult mmResult;
            lock (waveOutLock)
            {
                mmResult = WaveInterop.waveOutPause(hWaveOut);
            }
            if (mmResult != 0)
            {
                throw new MmException(mmResult, "waveOutPause");
            }
        }
    }

    private void Resume()
    {
        if (playbackState == PlaybackState.Paused)
        {
            MmResult mmResult;
            lock (waveOutLock)
            {
                mmResult = WaveInterop.waveOutRestart(hWaveOut);
            }
            if (mmResult != 0)
            {
                throw new MmException(mmResult, "waveOutRestart");
            }
            playbackState = PlaybackState.Playing;
        }
    }

    public void Stop()
    {
        if (playbackState != 0)
        {
            playbackState = PlaybackState.Stopped;
            MmResult mmResult;
            lock (waveOutLock)
            {
                mmResult = WaveInterop.waveOutReset(hWaveOut);
            }
            if (mmResult != 0)
            {
                throw new MmException(mmResult, "waveOutReset");
            }
            callbackEvent.Set();
        }
    }

    public long GetPosition()
    {
        return WaveOutUtils.GetPositionBytes(hWaveOut, waveOutLock);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Dispose(disposing: true);
    }

    protected void Dispose(bool disposing)
    {
        Stop();
        if (disposing)
        {
            DisposeBuffers();
        }
        CloseWaveOut();
    }

    private void CloseWaveOut()
    {
        if (callbackEvent != null)
        {
            callbackEvent.Close();
            callbackEvent = null;
        }
        lock (waveOutLock)
        {
            if (hWaveOut != IntPtr.Zero)
            {
                WaveInterop.waveOutClose(hWaveOut);
                hWaveOut = IntPtr.Zero;
            }
        }
    }

    private void DisposeBuffers()
    {
        if (buffers != null)
        {
            WaveOutBuffer[] array = buffers;
            for (int i = 0; i < array.Length; i++)
            {
                array[i].Dispose();
            }
            buffers = null;
        }
    }

    ~SDLOutEvent()
    {
        Dispose(disposing: false);
    }

    private void RaisePlaybackStoppedEvent(Exception e)
    {
        EventHandler<StoppedEventArgs> handler = this.PlaybackStopped;
        if (handler == null)
        {
            return;
        }
        if (syncContext == null)
        {
            handler(this, new StoppedEventArgs(e));
            return;
        }
        syncContext.Post(delegate {
            handler(this, new StoppedEventArgs(e));
        }, null);
    }
}
