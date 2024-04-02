using NAudio.MediaFoundation;
using NAudio.Wave;
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
    }
}
