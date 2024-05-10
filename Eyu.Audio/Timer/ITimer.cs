using System;

namespace Eyu.Audio.Timer
{
    public interface ITimer : IDisposable
    {
        void SetPeriod(int periodMS);

        void Start();

        void Stop();
    }
}
