using NAudio.Wave;
using System;

namespace Eyu.Audio.Provider;


public class BufferSampleProvider : ISampleProvider
{
    private CircularBuffer circularBuffer;

    private readonly WaveFormat waveFormat;

    public bool ReadFully { get; set; }

    public int BufferLength { get; set; }

    public TimeSpan BufferDuration
    {
        get
        {
            return TimeSpan.FromSeconds((double)BufferLength / (double)WaveFormat.AverageBytesPerSecond);
        }
        set
        {
            BufferLength = (int)(value.TotalSeconds * (double)WaveFormat.AverageBytesPerSecond);
        }
    }

    public bool DiscardOnBufferOverflow { get; set; }

    public int BufferedSamples
    {
        get
        {
            if (circularBuffer != null)
            {
                return circularBuffer.Count;
            }

            return 0;
        }
    }

    public TimeSpan BufferedDuration => TimeSpan.FromSeconds((double)BufferedSamples / (double)WaveFormat.AverageBytesPerSecond);

    public WaveFormat WaveFormat => waveFormat;

    public BufferSampleProvider(WaveFormat waveFormat)
    {
        this.waveFormat = waveFormat;
        BufferLength = waveFormat.AverageBytesPerSecond * 5;
        ReadFully = true;
    }

    public void AddSamples(float[] buffer, int offset, int count)
    {
        if (circularBuffer == null)
        {
            circularBuffer = new CircularBuffer(BufferLength);
        }

        if (circularBuffer.Write(buffer, offset, count) < count && !DiscardOnBufferOverflow)
        {
            throw new InvalidOperationException("Buffer full");
        }
    }

    public void ClearBuffer()
    {
        if (circularBuffer != null)
        {
            circularBuffer.Reset();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int num = 0;
        if (circularBuffer != null)
        {
            num = circularBuffer.Read(buffer, offset, count);
        }

        if (ReadFully && num < count)
        {
            Array.Clear(buffer, offset + num, count - num);
            num = count;
        }

        return num;
    }
}

//
// 摘要:
//     A very basic circular buffer implementation
internal class CircularBuffer
{
    private readonly float[] buffer;

    private readonly object lockObject;

    private int writePosition;

    private int readPosition;

    private int byteCount;

    //
    // 摘要:
    //     Maximum length of this circular buffer
    public int MaxLength => buffer.Length;

    //
    // 摘要:
    //     Number of bytes currently stored in the circular buffer
    public int Count
    {
        get
        {
            lock (lockObject)
            {
                return byteCount;
            }
        }
    }

    //
    // 摘要:
    //     Create a new circular buffer
    //
    // 参数:
    //   size:
    //     Max buffer size in bytes
    public CircularBuffer(int size)
    {
        buffer = new float[size];
        lockObject = new object();
    }

    //
    // 摘要:
    //     Write data to the buffer
    //
    // 参数:
    //   data:
    //     Data to write
    //
    //   offset:
    //     Offset into data
    //
    //   count:
    //     Number of bytes to write
    //
    // 返回结果:
    //     number of bytes written
    public int Write(float[] data, int offset, int count)
    {
        lock (lockObject)
        {
            int num = 0;
            if (count > buffer.Length - byteCount)
            {
                count = buffer.Length - byteCount;
            }

            int num2 = Math.Min(buffer.Length - writePosition, count);
            Array.Copy(data, offset, buffer, writePosition, num2);
            writePosition += num2;
            writePosition %= buffer.Length;
            num += num2;
            if (num < count)
            {
                Array.Copy(data, offset + num, buffer, writePosition, count - num);
                writePosition += count - num;
                num = count;
            }

            byteCount += num;
            return num;
        }
    }

    //
    // 摘要:
    //     Read from the buffer
    //
    // 参数:
    //   data:
    //     Buffer to read into
    //
    //   offset:
    //     Offset into read buffer
    //
    //   count:
    //     Bytes to read
    //
    // 返回结果:
    //     Number of bytes actually read
    public int Read(float[] data, int offset, int count)
    {
        lock (lockObject)
        {
            if (count > byteCount)
            {
                count = byteCount;
            }

            int num = 0;
            int num2 = Math.Min(buffer.Length - readPosition, count);
            Array.Copy(buffer, readPosition, data, offset, num2);
            num += num2;
            readPosition += num2;
            readPosition %= buffer.Length;
            if (num < count)
            {
                Array.Copy(buffer, readPosition, data, offset + num, count - num);
                readPosition += count - num;
                num = count;
            }

            byteCount -= num;
            return num;
        }
    }

    //
    // 摘要:
    //     Resets the buffer
    public void Reset()
    {
        lock (lockObject)
        {
            ResetInner();
        }
    }

    private void ResetInner()
    {
        byteCount = 0;
        readPosition = 0;
        writePosition = 0;
    }

    //
    // 摘要:
    //     Advances the buffer, discarding bytes
    //
    // 参数:
    //   count:
    //     Bytes to advance
    public void Advance(int count)
    {
        lock (lockObject)
        {
            if (count >= byteCount)
            {
                ResetInner();
                return;
            }

            byteCount -= count;
            readPosition += count;
            readPosition %= MaxLength;
        }
    }
}