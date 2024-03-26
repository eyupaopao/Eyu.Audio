using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer
{
    /// <summary>
    /// A timer based on the multimedia timer API with 1ms precision.
    /// </summary>
    internal class MultimediaTimer : ITimer, IDisposable
    {
        private const int EventTypeSingle = 0;
        private const int EventTypePeriodic = 1;
        private bool disposed = false;
        private int interval, resolution;
        private volatile uint timerId;

        // Hold the timer callback to prevent garbage collection.
        private readonly MultimediaTimerCallback callback;
        private readonly Action tick;

        public MultimediaTimer(Action tick)
        {
            callback = new MultimediaTimerCallback(TimerCallbackMethod);
            Resolution = 5;
            Interval = 10;
            this.tick = tick;
        }

        ~MultimediaTimer()
        {
            Dispose(false);
        }

        /// <summary>
        /// The period of the timer in milliseconds.
        /// </summary>
        private int Interval
        {
            get
            {
                return interval;
            }
            set
            {
                CheckDisposed();

                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");

                interval = value;
                if (Resolution > Interval)
                    Resolution = value;
            }
        }

        /// <summary>
        /// The resolution of the timer in milliseconds. The minimum resolution is 0, meaning highest possible resolution.
        /// </summary>
        private int Resolution
        {
            get
            {
                return resolution;
            }
            set
            {
                CheckDisposed();

                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");

                resolution = value;
            }
        }

        /// <summary>
        /// Gets whether the timer has been started yet.
        /// </summary>
        private bool IsRunning
        {
            get { return timerId != 0; }
        }

        public void Start()
        {
            CheckDisposed();

            if (IsRunning)
                throw new InvalidOperationException("Timer is already running");

            // Event type = 0, one off event
            // Event type = 1, periodic event
            uint userCtx = 0;
            timerId = NativeMethods.TimeSetEvent((uint)Interval, (uint)Resolution, callback, ref userCtx, EventTypePeriodic);
            if (timerId == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
        }

        public void Stop()
        {
            CheckDisposed();

            if (!IsRunning)
                throw new InvalidOperationException("Timer has not been started");

            StopInternal();
        }

        private void StopInternal()
        {
            NativeMethods.TimeKillEvent(timerId);
            timerId = 0;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void TimerCallbackMethod(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2)
        {
            tick?.Invoke();
        }

        private void CheckDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("MultimediaTimer");
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;

            disposed = true;
            if (IsRunning)
            {
                StopInternal();
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void SetPeriod(int periodMS)
        {
            Interval = periodMS;
        }
    }

    internal delegate void MultimediaTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2);

    internal static class NativeMethods
    {
        [DllImport("winmm.dll", SetLastError = true, EntryPoint = "timeSetEvent")]
        internal static extern uint TimeSetEvent(uint msDelay, uint msResolution, MultimediaTimerCallback callback, ref uint userCtx, uint eventType);

        [DllImport("winmm.dll", SetLastError = true, EntryPoint = "timeKillEvent")]
        internal static extern void TimeKillEvent(uint uTimerId);
    }
}
