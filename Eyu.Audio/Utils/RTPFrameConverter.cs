using System;

namespace Eyu.Audio.Utils
{
    /*
        header:
           |0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|0 1 2 3 4 5 6 7|
           |v  |p|x|cc     |m|pt           |sequence number 包序列          |
           |timestamp  时间戳                                               |
           |ssrc  序列号                                                    |
           |csrc  32bit * 0 ~ 15  （可选）                                  |
     */
    public class RTPFrameConverter
    {
        public RTPFrameConverter(int sampleRate, int bitDepth, int channel, AudioFormat audioFormat, uint ssrc)
        {
            SampleRate = sampleRate;
            BitDepth = bitDepth;
            Channel = channel;
            AudioFormat = audioFormat;
            this.ssrc = ssrc;
            if (AudioFormat == AudioFormat.Pcm)
            {
                // 包时间ms
                PTime = 4;
                // 每包数据的大小.
                FrameSize = bitDepth * sampleRate * channel * PTime / 8000;
                SamplePerPackage = sampleRate * PTime / 1000;
            }
            else
            {
                FrameSize = 2048;
                PTime = 20;
            }
        }
        public int FrameSize
        {
            get;
            set;
        }

        public int SamplePerPackage
        {
            get; set;
        }

        public int PTime
        {
            get;
            set;
        }

        public AudioFormat AudioFormat
        {
            get;
            set;
        }
        public int Priority
        {
            get;
        }
        public int SampleRate
        {
            get;
            set;
        }
        public int BitDepth
        {
            get;
        }
        public string Encoding => $"L{BitDepth}";
        public int Channel
        {
            get;
        }
        /// <summary>
        /// 媒体时间戳
        /// 1、时间戳单位：时间戳计算的单位不是秒之类的单位，而是由采样频率所代替的单位，这样做的目的就是 为了是时间戳单位更为精准。
        /// 比如说一个音频的采样频率为48000Hz，那么我们可以把时间戳单位设为1 / 48000；就是1秒48000个时间戳单位。
        /// 2、时间戳增量：相邻两个RTP包之间的时间差（以第一点的时间戳单位为基准）；
        /// 3、采样频率： 每秒钟抽取样本的次数，例如音频的采样率一般为48000Hz；
        /// 所以48000hz音频的 经过1秒的时间是: 48000个单位,经过1ms的时间就是 48个单位; 
        /// </summary>
        public uint MediaTimeStamp
        {
            get; private set;
        }


        private int seqNum = 1;
        public uint ssrc;
        public void SetTime(uint newTime)
        {
            MediaTimeStamp = newTime;
        }
        /// <summary>
        /// 对时
        /// </summary>
        /// <param Name="now"></param>
        public void SetTime(TimeSpan? now = null)
        {
            if (now == null)
                now = DateTime.UtcNow - DateTime.UnixEpoch;
            var nanosecond = now.Value.TotalNanoseconds;
            // 媒体时间戳=当前时间*采样率；
            var time = Math.Round(nanosecond * SampleRate) / 1000000000 % 0x100000000;
            // 取整
            MediaTimeStamp = (uint)(Math.Floor(time / SamplePerPackage) * SamplePerPackage);
        }
        public byte[] Convert(byte[] buffer, int count)
        {
            var rtpBuffer = new byte[12 + count];
            Array.Copy(buffer, 0, rtpBuffer, 12, count);
            // 版本号，和rtp playload type
            rtpBuffer.WriteUInt16BE((1 << 15) + 96, 0);
            // 2-3 squNum
            rtpBuffer.WriteUInt16BE((ushort)seqNum, 2);
            rtpBuffer.WriteUInt32BE(MediaTimeStamp, 4);
            // 8-11 ssrc
            rtpBuffer.WriteUInt32BE(ssrc, 8);

            seqNum = (seqNum + 1) % 0x10000;// 序列号加1

            MediaTimeStamp = (uint)((MediaTimeStamp + SamplePerPackage) % 0x100000000);// 时间增加fpp

            return buffer;
        }

    }
}
