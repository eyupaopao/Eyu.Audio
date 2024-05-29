using NAudio.Lame;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NLayer;
using System.IO;

namespace Eyu.Audio.Utils
{
    public static class MediaConverter
    {


        public static bool MediaToMp3(WaveStream reader, string outPutFilePath)
        {
            try
            {
                MediaFoundationApi.Startup();
                MediaFoundationEncoder.EncodeToMp3(reader, outPutFilePath);
                MediaFoundationApi.Shutdown();
                return true;
            }
            catch { return false; }

        }
        public static bool ConvertWavToMp3(WaveStream reader, string mp3FilePath)
        {

            try
            {
                using (var writer = new LameMP3FileWriter(mp3FilePath, reader.WaveFormat, LAMEPreset.ABR_128))
                {
                    reader.CopyTo(writer);
                    return true;
                }

            }
            catch { return false; }
        }
    }
}
