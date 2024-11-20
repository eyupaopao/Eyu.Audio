﻿using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace Eyu.Audio.Reader;

public class AudioFileReader : WaveStream, ISampleProvider, IWaveProvider
{
    private WaveStream readerStream; // the waveStream which we will use for all positioning
    private readonly SampleChannel sampleChannel; // sample provider that gives us most stuff we need
    private readonly int destBytesPerSample;
    private readonly int sourceBytesPerSample;
    private readonly long length;
    private readonly object lockObject;

    /// <summary>
    /// Initializes a new instance of AudioFileReader
    /// </summary>
    /// <param name="fileName">The file to open</param>
    public AudioFileReader(string fileName)
    {
        lockObject = new object();
        FileName = fileName;
        _stream = File.OpenRead(fileName);
        CreateReaderStream();
        sourceBytesPerSample = readerStream.WaveFormat.BitsPerSample / 8 * readerStream.WaveFormat.Channels;
        sampleChannel = new SampleChannel(readerStream, false);
        destBytesPerSample = 4 * sampleChannel.WaveFormat.Channels;
        length = SourceToDest(readerStream.Length);
    }
    public AudioFileReader(string fileName, Stream stream)
    {
        lockObject = new object();
        FileName = fileName;
        _stream = stream;
        CreateReaderStream();
        sourceBytesPerSample = readerStream.WaveFormat.BitsPerSample / 8 * readerStream.WaveFormat.Channels;
        sampleChannel = new SampleChannel(readerStream, false);
        destBytesPerSample = 4 * sampleChannel.WaveFormat.Channels;
        length = SourceToDest(readerStream.Length);
    }
    /// <summary>
    /// Creates the reader stream, supporting all filetypes in the core NAudio library,
    /// and ensuring we are in PCM format
    /// </summary>
    /// <param name="fileName">File Name</param>
    private void CreateReaderStream()
    {
        if (FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            readerStream = new WaveFileReader(_stream);
            if (readerStream.WaveFormat.Encoding != WaveFormatEncoding.Pcm && readerStream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                readerStream = WaveFormatConversionStream.CreatePcmStream(readerStream);
                readerStream = new BlockAlignReductionStream(readerStream);
            }
        }
        else if (FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {

            mp3FileReader = new Mp3Reader(_stream);
            readerStream = mp3FileReader;
        }
        else if (FileName.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase) || FileName.EndsWith(".aif", StringComparison.OrdinalIgnoreCase))
        {
            readerStream = new AiffFileReader(_stream);
        }
        else
        {
            // fall back to media foundation reader, see if that can play it
            readerStream = new MediaFoundationReader(FileName);
        }
    }

    Stream _stream;

    /// <summary>
    /// File Name
    /// </summary>
    public string FileName
    {
        get;
    }

    /// <summary>
    /// WaveFormat of this stream
    /// </summary>
    public override WaveFormat WaveFormat => sampleChannel.WaveFormat;

    /// <summary>
    /// Length of this stream (in bytes)
    /// </summary>
    public override long Length => length;

    /// <summary>
    /// Position of this stream (in bytes)
    /// </summary>
    public override long Position
    {
        get
        {
            return SourceToDest(readerStream.Position);
        }
        set
        {
            lock (lockObject) { readerStream.Position = DestToSource(value); }
        }
    }

    /// <summary>
    /// Reads from this wave stream
    /// </summary>
    /// <param name="buffer">Audio buffer</param>
    /// <param name="offset">Offset into buffer</param>
    /// <param name="count">Number of bytes required</param>
    /// <returns>Number of bytes read</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        var waveBuffer = new WaveBuffer(buffer);
        int samplesRequired = count / 4;
        int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
        return samplesRead * 4;
    }

    public Mp3Frame ReadMp3Frame()
    {
        return mp3FileReader.ReadNextFrame();
    }

    /// <summary>
    /// Reads audio from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="count">Number of samples required</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (lockObject)
        {
            return sampleChannel.Read(buffer, offset, count);
        }
    }

    /// <summary>
    /// Gets or Sets the Volume of this AudioFileReader. 1.0f is full volume
    /// </summary>
    public float Volume
    {
        get
        {
            return sampleChannel.Volume;
        }
        set
        {
            sampleChannel.Volume = value;
        }
    }

    /// <summary>
    /// Helper to convert source to dest bytes
    /// </summary>
    private long SourceToDest(long sourceBytes)
    {
        return destBytesPerSample * (sourceBytes / sourceBytesPerSample);
    }

    /// <summary>
    /// Helper to convert dest to source bytes
    /// </summary>
    private long DestToSource(long destBytes)
    {
        return sourceBytesPerSample * (destBytes / destBytesPerSample);
    }

    /// <summary>
    /// Disposes this AudioFileReader
    /// </summary>
    /// <param name="disposing">True if called from Dispose</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (readerStream != null)
            {
                readerStream.Dispose();
                readerStream = null;
            }
        }
        base.Dispose(disposing);
    }

    Mp3FileReaderBase mp3FileReader;

    //public Action<byte[], int> OnMp3FrameReaded
    //{
    //    get => mp3FileReader?.OnMp3FrameReaded;
    //    set
    //    {
    //        if (mp3FileReader is not null)
    //        {
    //            mp3FileReader.OnMp3FrameReaded += value;
    //        }
    //    }
    //}
}



