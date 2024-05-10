using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eyu.Audio.Timer
{
    internal class LinuxTimer : ITimer, IDisposable
    {
        private readonly int fileDescriptor;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly Action tick;
        private bool isRunning;

        public LinuxTimer(Action tick)
        {
            fileDescriptor = Interop.timerfd_create(Interop.ClockIds.CLOCK_MONOTONIC, 0);

            if (fileDescriptor == -1)
                throw new Exception($"Unable to create timer, errno = {Marshal.GetLastWin32Error()}");
            {

            }

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
            var itval = new Interop.itimerspec
            {
                it_interval = new Interop.timespec
                {
                    tv_sec = sec,
                    tv_nsec = ns
                },
                it_value = new Interop.timespec
                {

                    tv_sec = sec,
                    tv_nsec = ns
                }
            };

            int ret = Interop.timerfd_settime(fileDescriptor, 0, itval, null);
            if (ret != 0)
                throw new Exception($"Error from timerfd_settime = {Marshal.GetLastWin32Error()}");
        }

        private long Wait()
        {
            // Wait for the next timer event. If we have missed any the number is written to "missed"
            byte[] buf = new byte[16];
            var handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            nint pointer = handle.AddrOfPinnedObject();
            int ret = Interop.read(fileDescriptor, pointer, buf.Length);
            // ret = bytes read
            long missed = Marshal.ReadInt64(pointer);
            handle.Free();

            if (ret < 0)
                throw new Exception($"Error in read = {Marshal.GetLastWin32Error()}");

            return missed;
        }

        public void Dispose()
        {
            cts.Cancel();

            Interop.close(fileDescriptor);
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
