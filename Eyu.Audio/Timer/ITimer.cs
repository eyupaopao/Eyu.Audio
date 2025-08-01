using System;

namespace Eyu.Audio.Timer
{
    public interface ITimer : IDisposable
    {
        void SetPeriod(double milliseconds);

        void Start();

        void Stop();
    }
}
