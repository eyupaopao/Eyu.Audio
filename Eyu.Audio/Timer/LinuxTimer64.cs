using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer
{
    internal class LinuxTimer64 : ITimer, IDisposable
    {
        private readonly int fileDescriptor;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly Action tick;
        private bool isRunning;

        public LinuxTimer64(Action tick)
        {
            fileDescriptor = Interop64.timerfd_create(Interop64.ClockIds.CLOCK_MONOTONIC, 0);

            if (fileDescriptor == -1)
                throw new Exception($"Unable to create timer, errno = {Marshal.GetLastWin32Error()}");

            ThreadPool.QueueUserWorkItem(Scheduler);
            this.tick = tick;
        }


        public void SetPeriod(int periodMS)
        {
            SetFrequency((uint)periodMS * 1_000);
        }

        private void Scheduler(object state)
        {
            while (!cts.IsCancellationRequested)
            {
                Wait();

                if (isRunning)
                    tick?.Invoke();
            }
        }

        private void SetFrequency(uint period)
        {
            uint sec = period / 1000000;
            uint ns = (period - sec * 1000000) * 1000;
            var itval = new Interop64.itimerspec64
            {
                it_interval = new Interop64.timespec64
                {
                    tv_sec = sec,
                    tv_nsec = ns
                },
                it_value = new Interop64.timespec64
                {
                    tv_sec = sec,
                    tv_nsec = ns
                }
            };

            int ret = Interop64.timerfd_settime(fileDescriptor, 0, itval, null);
            if (ret != 0)
                throw new Exception($"Error from timerfd_settime = {Marshal.GetLastWin32Error()}");
        }

        private long Wait()
        {
            // Wait for the next timer event. If we have missed any the number is written to "missed"
            byte[] buf = new byte[8];
            var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                nint pointer = handle.AddrOfPinnedObject();

                long ret = Interop64.read(fileDescriptor, pointer, 8);
                // ret = bytes read
                if (ret < 0)
                    throw new Exception($"Error in read = {Marshal.GetLastWin32Error()}");

                long missed = Marshal.ReadInt64(pointer);

                return missed;
            }
            finally
            {
                handle.Free();
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            Interop64.close(fileDescriptor);
        }

        public void Start()
        {
            isRunning = true;
        }

        public void Stop()
        {
            isRunning = false;
        }
    }
}
