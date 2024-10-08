﻿using Eyu.Audio.Provider;
using Eyu.Audio.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample;

[TestClass]
internal class ReSampleDemo
{
    [TestMethod]
    public void Test()
    {
        Console.WriteLine("Hello, World!");

        var demo = new DemoProvider();
        var sampleConverter = new SampleWaveFormatConversionProvider(new WaveFormat(16000, 16, 2), demo);
        var pcmProvider = new SampleToWaveProvider16(sampleConverter);
        var samples = new float[1024];
        var buffer = new byte[2048];

        var len = sampleConverter.Read(samples, 0, samples.Length);
        Array.Reverse(samples, 0, len);

        for (int i = 0; i < samples.Length; i += 2)
        {
            Console.WriteLine($"sample{i}: l: {samples[i]},r:{samples[i + 1]}");
        }

        Console.WriteLine();

        len = pcmProvider.Read(buffer, 0, buffer.Length);
        Array.Resize(ref buffer, len);
        var result = $"[{string.Join(',', buffer)}]";
        Console.WriteLine(result);

    }
}

public class DemoProvider : IWaveProvider, ISampleProvider
{
    public DemoProvider()
    {
        this.WaveFormat = new WaveFormat(48000, 16, 2);
        samples = data.Wave16ToSample(0, data.Length);
    }
    /// <summary>
    /// 测试数据
    /// </summary>
    static byte[] data = {
    0x14, 0x00, 0x0B, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF5, 0xFF, 0xF5, 0xFF, 0xE9, 0xFF,
    0xE9, 0xFF, 0xDD, 0xFF, 0xDD, 0xFF, 0xD1, 0xFF, 0xD1, 0xFF, 0xC7, 0xFF, 0xC7, 0xFF, 0xBE, 0xFF,
    0xBE, 0xFF, 0xB7, 0xFF, 0xB7, 0xFF, 0xB3, 0xFF, 0xB3, 0xFF, 0xB2, 0xFF, 0xB2, 0xFF, 0xB4, 0xFF,
    0xB4, 0xFF, 0xB9, 0xFF, 0xB9, 0xFF, 0xBE, 0xFF, 0xBE, 0xFF, 0xC2, 0xFF, 0xC2, 0xFF, 0xC7, 0xFF,
    0xC7, 0xFF, 0xCA, 0xFF, 0xCA, 0xFF, 0xCD, 0xFF, 0xCD, 0xFF, 0xD1, 0xFF, 0xD1, 0xFF, 0xD3, 0xFF,
    0xD3, 0xFF, 0xD7, 0xFF, 0xD7, 0xFF, 0xDB, 0xFF, 0xDB, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0xE4, 0xFF,
    0xE4, 0xFF, 0xEB, 0xFF, 0xEB, 0xFF, 0xF2, 0xFF, 0xF2, 0xFF, 0xF8, 0xFF, 0xF8, 0xFF, 0xFB, 0xFF,
    0xFB, 0xFF, 0xFB, 0xFF, 0xFB, 0xFF, 0xF8, 0xFF, 0xF8, 0xFF, 0xF3, 0xFF, 0xF3, 0xFF, 0xEE, 0xFF,
    0xEE, 0xFF, 0xE8, 0xFF, 0xE8, 0xFF, 0xE3, 0xFF, 0xE3, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0xDD, 0xFF,
    0xDD, 0xFF, 0xDB, 0xFF, 0xDB, 0xFF, 0xDB, 0xFF, 0xDB, 0xFF, 0xDB, 0xFF, 0xDB, 0xFF, 0xDD, 0xFF,
    0xDD, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0xE2, 0xFF, 0xE2, 0xFF, 0xE4, 0xFF, 0xE4, 0xFF, 0xE7, 0xFF,
    0xE7, 0xFF, 0xE9, 0xFF, 0xE9, 0xFF, 0xED, 0xFF, 0xED, 0xFF, 0xF2, 0xFF, 0xF2, 0xFF, 0xF8, 0xFF,
    0xF8, 0xFF, 0xFE, 0xFF, 0xFE, 0xFF, 0x05, 0x00, 0x05, 0x00, 0x0D, 0x00, 0x0D, 0x00, 0x15, 0x00,
    0x15, 0x00, 0x1E, 0x00, 0x1E, 0x00, 0x27, 0x00, 0x27, 0x00, 0x30, 0x00, 0x30, 0x00, 0x38, 0x00,
    0x38, 0x00, 0x3E, 0x00, 0x3E, 0x00, 0x41, 0x00, 0x41, 0x00, 0x43, 0x00, 0x43, 0x00, 0x43, 0x00,
    0x43, 0x00, 0x42, 0x00, 0x42, 0x00, 0x40, 0x00, 0x40, 0x00, 0x3C, 0x00, 0x3C, 0x00, 0x37, 0x00,
    0x37, 0x00, 0x32, 0x00, 0x32, 0x00, 0x2D, 0x00, 0x2D, 0x00, 0x29, 0x00, 0x29, 0x00, 0x25, 0x00,
    0x25, 0x00, 0x21, 0x00, 0x21, 0x00, 0x1C, 0x00, 0x1C, 0x00, 0x18, 0x00, 0x18, 0x00, 0x13, 0x00,
    0x13, 0x00, 0x0F, 0x00, 0x0F, 0x00, 0x0B, 0x00, 0x0B, 0x00, 0x06, 0x00, 0x06, 0x00, 0x01, 0x00,
    0x01, 0x00, 0xFB, 0xFF, 0xFB, 0xFF, 0xF5, 0xFF, 0xF5, 0xFF, 0xF1, 0xFF, 0xF1, 0xFF, 0xF0, 0xFF,
    0xF0, 0xFF, 0xF1, 0xFF, 0xF1, 0xFF, 0xF5, 0xFF, 0xF5, 0xFF, 0xFA, 0xFF, 0xFA, 0xFF, 0xFF, 0xFF,
    0xFF, 0xFF, 0x04, 0x00, 0x04, 0x00, 0x08, 0x00, 0x08, 0x00, 0x0B, 0x00, 0x0B, 0x00, 0x0D, 0x00,
    0x0D, 0x00, 0x0D, 0x00, 0x0D, 0x00, 0x0C, 0x00, 0x0C, 0x00, 0x0A, 0x00, 0x0A, 0x00, 0x07, 0x00,
    0x07, 0x00, 0x06, 0x00, 0x06, 0x00, 0x05, 0x00, 0x05, 0x00, 0x06, 0x00, 0x06, 0x00, 0x08, 0x00,
    0x08, 0x00, 0x08, 0x00, 0x08, 0x00, 0x08, 0x00, 0x08, 0x00, 0x08, 0x00, 0x08, 0x00, 0x06, 0x00,
    0x06, 0x00, 0x06, 0x00, 0x06, 0x00, 0x04, 0x00, 0x04, 0x00, 0x02, 0x00, 0x02, 0x00, 0xFE, 0xFF,
    0xFE, 0xFF, 0xF8, 0xFF, 0xF8, 0xFF, 0xF2, 0xFF, 0xF2, 0xFF, 0xED, 0xFF, 0xED, 0xFF, 0xEA, 0xFF,
    0xEA, 0xFF, 0xEA, 0xFF, 0xEA, 0xFF, 0xED, 0xFF, 0xED, 0xFF, 0xF2, 0xFF, 0xF2, 0xFF, 0xF8, 0xFF,
    0xF8, 0xFF, 0xFC, 0xFF, 0xFC, 0xFF, 0x01, 0x00, 0x01, 0x00, 0x03, 0x00, 0x03, 0x00, 0x04, 0x00,
    0x04, 0x00, 0x05, 0x00, 0x05, 0x00, 0x05, 0x00, 0x05, 0x00, 0x06, 0x00, 0x06, 0x00, 0x08, 0x00,
    0x08, 0x00, 0x0C, 0x00, 0x0C, 0x00, 0x10, 0x00, 0x10, 0x00, 0x13, 0x00, 0x13, 0x00, 0x15, 0x00,
    0x15, 0x00, 0x16, 0x00, 0x16, 0x00, 0x16, 0x00, 0x16, 0x00, 0x16, 0x00, 0x16, 0x00, 0x16, 0x00,
    0x16, 0x00, 0x15, 0x00, 0x15, 0x00, 0x14, 0x00, 0x14, 0x00, 0x12, 0x00, 0x12, 0x00, 0x10, 0x00,
    0x10, 0x00, 0x10, 0x00, 0x10, 0x00, 0x15, 0x00, 0x15, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x2F, 0x00,
    0x2F, 0x00, 0x43, 0x00, 0x43, 0x00, 0x58, 0x00, 0x58, 0x00, 0x6C, 0x00, 0x6C, 0x00, 0x7E, 0x00,
    0x7E, 0x00, 0x8D, 0x00, 0x8D, 0x00, 0x9C, 0x00, 0x9C, 0x00, 0xA8, 0x00, 0xA8, 0x00, 0xB5, 0x00,
    0xB5, 0x00, 0xC1, 0x00, 0xC1, 0x00, 0xCE, 0x00, 0xCE, 0x00, 0xDC, 0x00, 0xDC, 0x00, 0xE9, 0x00,
    0xE9, 0x00, 0xF5, 0x00, 0xF5, 0x00, 0xFC, 0x00, 0xFC, 0x00, 0x00, 0x01, 0x00, 0x01, 0xFF, 0x00,
    0xFF, 0x00, 0xF8, 0x00, 0xF8, 0x00, 0xED, 0x00, 0xED, 0x00, 0xDF, 0x00, 0xDF, 0x00, 0xCF, 0x00,
    0xCF, 0x00, 0xBE, 0x00, 0xBE, 0x00, 0xA9, 0x00, 0xA9, 0x00, 0x92, 0x00, 0x92, 0x00, 0x7A, 0x00,
    0x7A, 0x00, 0x62, 0x00, 0x62, 0x00, 0x49, 0x00, 0x49, 0x00, 0x32, 0x00, 0x32, 0x00, 0x1E, 0x00,
    0x1E, 0x00, 0x0C, 0x00, 0x0C, 0x00, 0xFD, 0xFF, 0xFD, 0xFF, 0xF0, 0xFF, 0xF0, 0xFF, 0xE4, 0xFF,
    0xE4, 0xFF, 0xDA, 0xFF, 0xDA, 0xFF, 0xD1, 0xFF, 0xD1, 0xFF, 0xCA, 0xFF, 0xCA, 0xFF, 0xC4, 0xFF,
    0xC4, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xBF, 0xFF, 0xBF, 0xFF, 0xC0, 0xFF, 0xC0, 0xFF, 0xC2, 0xFF,
    0xC2, 0xFF, 0xC6, 0xFF, 0xC6, 0xFF, 0xCC, 0xFF, 0xCC, 0xFF, 0xD2, 0xFF, 0xD2, 0xFF, 0xD9, 0xFF,
    0xD9, 0xFF, 0xDF, 0xFF, 0xDF, 0xFF, 0xE5, 0xFF, 0xE5, 0xFF, 0xE9, 0xFF, 0xE9, 0xFF, 0xEC, 0xFF,
    0xEC, 0xFF, 0xED, 0xFF, 0xED, 0xFF, 0xEB, 0xFF, 0xEB, 0xFF, 0xE6, 0xFF, 0xE6, 0xFF, 0xDF, 0xFF,
    0xDF, 0xFF, 0xD6, 0xFF, 0xD6, 0xFF, 0xCF, 0xFF, 0xCF, 0xFF, 0xC7, 0xFF, 0xC7, 0xFF, 0xC2, 0xFF,
    0xC2, 0xFF, 0xBF, 0xFF, 0xBF, 0xFF, 0xBC, 0xFF, 0xBC, 0xFF, 0xBA, 0xFF, 0xBA, 0xFF, 0xB7, 0xFF,
    0xB7, 0xFF, 0xB4, 0xFF, 0xB4, 0xFF, 0xB1, 0xFF, 0xB1, 0xFF, 0xAE, 0xFF, 0xAE, 0xFF, 0xAC, 0xFF,
    0xAC, 0xFF, 0xAB, 0xFF, 0xAB, 0xFF, 0xAD, 0xFF, 0xAD, 0xFF, 0xB2, 0xFF, 0xB2, 0xFF, 0xB9, 0xFF,
    0xB9, 0xFF, 0xC1, 0xFF, 0xC1, 0xFF, 0xCC, 0xFF, 0xCC, 0xFF, 0xD8, 0xFF, 0xD8, 0xFF, 0xE5, 0xFF,
    0xE5, 0xFF, 0xF3, 0xFF, 0xF3, 0xFF, 0x01, 0x00, 0x01, 0x00, 0x0F, 0x00, 0x0F, 0x00, 0x1A, 0x00,
    0x1A, 0x00, 0x23, 0x00, 0x23, 0x00, 0x28, 0x00, 0x28, 0x00, 0x2D, 0x00, 0x2D, 0x00, 0x2F, 0x00,
    0x2F, 0x00, 0x31, 0x00, 0x31, 0x00, 0x33, 0x00, 0x33, 0x00, 0x34, 0x00, 0x34, 0x00, 0x33, 0x00,
    0x33, 0x00, 0x32, 0x00, 0x32, 0x00, 0x30, 0x00, 0x30, 0x00, 0x2D, 0x00, 0x2D, 0x00, 0x28, 0x00,
    0x28, 0x00, 0x20, 0x00, 0x20, 0x00, 0x15, 0x00, 0x15, 0x00, 0x0A, 0x00, 0x0A, 0x00, 0xFE, 0xFF,
    0xFE, 0xFF, 0xF2, 0xFF, 0xF2, 0xFF, 0xE7, 0xFF, 0xE7, 0xFF, 0xDC, 0xFF, 0xDC, 0xFF, 0xD3, 0xFF,
    0xD3, 0xFF, 0xCC, 0xFF, 0xCC, 0xFF, 0xC7, 0xFF, 0xC7, 0xFF, 0xC5, 0xFF, 0xC5, 0xFF, 0xC5, 0xFF,
    0xC5, 0xFF, 0xC7, 0xFF, 0xC7, 0xFF, 0xCA, 0xFF, 0xCA, 0xFF, 0xCC, 0xFF, 0xCC, 0xFF, 0xCE, 0xFF,
    0xCE, 0xFF, 0xD0, 0xFF, 0xD0, 0xFF, 0xD2, 0xFF, 0xD2, 0xFF, 0xD5, 0xFF, 0xD5, 0xFF, 0xD8, 0xFF,
    0xD8, 0xFF, 0xDD, 0xFF, 0xDD, 0xFF, 0xE2, 0xFF, 0xE2, 0xFF, 0xE7, 0xFF, 0xE7, 0xFF, 0xEE, 0xFF,
    0xEE, 0xFF, 0xF4, 0xFF, 0xF4, 0xFF, 0xFB, 0xFF, 0xFB, 0xFF, 0x01, 0x00, 0x01, 0x00, 0x07, 0x00,
};
    /// <summary>
    /// 测试数据转为float
    /// </summary>
    static float[] samples;

    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    /// 读取buffer
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public int Read(byte[] buffer, int offset, int count)
    {
        if (count < data.Length)
        {
            Array.Copy(data, 0, buffer, 0, count);
            return count;
        }
        Array.Copy(data, 0, buffer, 0, data.Length);
        return data.Length;
    }
    /// <summary>
    /// 读取float sample
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public int Read(float[] buffer, int offset, int count)
    {

        if (count < samples.Length)
        {
            Array.Copy(samples, 0, buffer, 0, count);
            return count;
        }
        Array.Copy(samples, 0, buffer, 0, samples.Length);
        return samples.Length;
    }
}
