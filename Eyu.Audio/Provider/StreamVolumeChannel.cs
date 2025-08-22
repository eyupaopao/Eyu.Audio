using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NWaves.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Provider;

public class StreamVolumeChannel : SampleChannel
{
    private float[] maxSamples;
    private int sampleCount;
    private int channels;
    private StreamVolumeEventArgs args;
    public int SamplesPerNotification { get; set; }
    public event EventHandler<StreamVolumeEventArgs> StreamVolume;
    public StreamVolumeChannel(IWaveProvider waveProvider) : base(waveProvider)
    {
        channels = WaveFormat.Channels;
        maxSamples = new float[channels];
        SamplesPerNotification = WaveFormat.SampleRate / 10;
        args = new StreamVolumeEventArgs() { MaxSampleValues = maxSamples };
    }
    public new int Read(float[] buffer, int offset, int count)
    {
        var len = base.Read(buffer, offset, count);
        WaveFormCalculator(buffer, 0, len);
        return len;
    }

    private void WaveFormCalculator(float[] buffer, int offset, int samplesRead)
    {
        if (StreamVolume is not null)
        {
            for (int index = 0; index < samplesRead; index += channels)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    float sampleValue = Math.Abs(buffer[offset + index + channel]);
                    maxSamples[channel] = Math.Max(maxSamples[channel], sampleValue);
                }
                sampleCount++;
                if (sampleCount >= SamplesPerNotification)
                {
                    StreamVolume(this, args);
                    sampleCount = 0;
                    Array.Clear(maxSamples, 0, channels);
                }
            }
        }
    }
}
