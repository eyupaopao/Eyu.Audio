using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyu.Audio.Utils;

public enum AudioFormat
{
    Mp3 = 0x01,
    Pcm = 0x02,
    None = 0xFF,
}

public enum AudioExtension
{
    mp3,
    wav,
    aiff,
    none
}