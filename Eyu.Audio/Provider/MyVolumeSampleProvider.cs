using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Provider
{
    /// <summary>
    /// Very simple sample provider supporting adjustable gain
    /// </summary>
    public class MyVolumeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        /// <summary>
        /// Initializes a new instance of VolumeSampleProvider
        /// </summary>
        /// <param name="source">Source Sample Provider</param>
        public MyVolumeSampleProvider(ISampleProvider source)
        {
            this.source = source;
            Volume = 1.0f;
        }

        /// <summary>
        /// WaveFormat
        /// </summary>
        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="sampleCount">Number of samples desired</param>
        /// <returns>Number of samples read</returns>
        public int Read(float[] buffer, int offset, int sampleCount)
        {
            try
            {

                int samplesRead = source.Read(buffer, offset, sampleCount);
                byte[] samples = buffer.SelectMany(x => BitConverter.GetBytes(x)).ToArray();
                if (Volume != 1f)
                {
                    for (int n = 0; n < sampleCount; n++)
                    {
                        buffer[offset + n] *= Volume;
                    }
                }
                return samplesRead;
            }
            catch { throw; }
        }

        /// <summary>
        /// Allows adjusting the volume, 1.0f = full volume
        /// </summary>
        public float Volume
        {
            get; set;
        }
    }

}
