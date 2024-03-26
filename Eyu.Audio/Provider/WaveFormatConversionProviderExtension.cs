using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Provider
{
    public static class WaveFormatConversionProviderExtension
    {
        public static WaveFormatConversionProvider Rune(this WaveFormatConversionProvider conversionProvider)
        {
            return conversionProvider;
        }
    }
}
