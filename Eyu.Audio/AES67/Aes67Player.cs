using Eyu.Audio.Aes67;
using Eyu.Audio.Reader;
using Eyu.Audio.Timer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.AES67;

public class Aes67Player
{
    private readonly string fileName;
    private readonly Aes67Channel aes67Channel;
    private AudioFileReader reader;
    private HighPrecisionTimer timer;

    public TimeSpan Total => reader.TotalTime;
    public TimeSpan CurrentTime => reader.CurrentTime;
    public Action? PlayBackStop;
    public Aes67Player(string fileName,Aes67Channel aes67Channel)
    {
        this.fileName = fileName;
        this.aes67Channel = aes67Channel;
        reader = new AudioFileReader(fileName);
        var waveFormat = reader.WaveFormat;
        timer = new HighPrecisionTimer(Callback);
        timer.SetPeriod(1000);
    }
    public void Start()
    {       
        timer.Start();
    }
    public void Stop()
    {
        timer.Stop();
        timer.Dispose();
    }
    public void Pause()
    {
        timer.Stop();
    }
    public void SelectProgress(TimeSpan time)
    {
        reader.CurrentTime = time;
    }
    private void Callback()
    {

    }

}
