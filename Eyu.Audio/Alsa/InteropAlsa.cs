using System;
using System.Runtime.InteropServices;

namespace Eyu.Audio.Alsa
{
    public static class InteropAlsa
    {
        const string AlsaLibrary = "libasound";

        [DllImport(AlsaLibrary)]
        public static extern nint snd_strerror(int errnum);

        [DllImport(AlsaLibrary)]
        internal static extern int snd_pcm_open(ref nint pcm, string name, snd_pcm_stream_t stream, int mode);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_start(nint pcm);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_pause(nint pcm, int enable);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_resume(nint pcm);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_drain(nint pcm);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_drop(nint pcm);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_close(nint pcm);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_recover(nint pcm, int err, int silent);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_writei(nint pcm, nint buffer, ulong size);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_readi(nint pcm, nint buffer, ulong size);

        [DllImport(AlsaLibrary)]
        internal static extern int snd_pcm_set_params(nint pcm, snd_pcm_format_t format, snd_pcm_access_t access, uint channels, uint rate, int soft_resample, uint latency);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_hw_params_malloc(ref nint @params);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_hw_params_any(nint pcm, nint @params);

        [DllImport(AlsaLibrary)]
        internal static extern int snd_pcm_hw_params_set_access(nint pcm, nint @params, snd_pcm_access_t access);

        [DllImport(AlsaLibrary)]
        internal static extern int snd_pcm_hw_params_set_format(nint pcm, nint @params, snd_pcm_format_t val);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_hw_params_set_channels(nint pcm, nint @params, uint val);

        [DllImport(AlsaLibrary)]
        public static extern unsafe int snd_pcm_hw_params_set_rate_near(nint pcm, nint @params, uint* val, int* dir);

        [DllImport(AlsaLibrary)]
        public static extern int snd_pcm_hw_params(nint pcm, nint @params);

        [DllImport(AlsaLibrary)]
        public static extern unsafe int snd_pcm_hw_params_get_period_size(nint @params, ulong* frames, int* dir);

        [DllImport(AlsaLibrary)]
        public static extern unsafe int snd_pcm_hw_params_set_period_size_near(nint pcm, nint @params, ulong* frames, int* dir);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_open(ref nint mixer, int mode);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_close(nint mixer);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_attach(nint mixer, string name);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_load(nint mixer);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_register(nint mixer, nint options, nint classp);

        [DllImport(AlsaLibrary)]
        public static extern nint snd_mixer_first_elem(nint mixer);

        [DllImport(AlsaLibrary)]
        public static extern nint snd_mixer_elem_next(nint elem);

        [DllImport(AlsaLibrary)]
        public static extern string snd_mixer_selem_get_name(nint elem);

        [DllImport(AlsaLibrary)]
        public static extern void snd_mixer_selem_id_alloca(nint ptr);

        [DllImport(AlsaLibrary)]
        internal static extern unsafe int snd_mixer_selem_get_playback_volume(nint elem, snd_mixer_selem_channel_id channel, long* value);

        [DllImport(AlsaLibrary)]
        internal static extern int snd_mixer_selem_set_playback_volume(nint elem, snd_mixer_selem_channel_id channel, long value);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_set_playback_volume_all(nint elem, long value);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_set_playback_switch_all(nint elem, int value);

        [DllImport(AlsaLibrary)]
        public static extern unsafe int snd_mixer_selem_get_playback_volume_range(nint elem, long* min, long* max);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_set_playback_volume_range(nint elem, long min, long max);

        [DllImport(AlsaLibrary)]
        internal static extern unsafe int snd_mixer_selem_get_capture_volume(nint elem, snd_mixer_selem_channel_id channel, long* value);

        [DllImport(AlsaLibrary)]
        internal static extern int snd_mixer_selem_set_capture_volume(nint elem, snd_mixer_selem_channel_id channel, long value);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_set_capture_volume_all(nint elem, long value);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_set_capture_switch_all(nint elem, int value);

        [DllImport(AlsaLibrary)]
        public static extern unsafe int snd_mixer_selem_get_capture_volume_range(nint elem, long* min, long* max);

        [DllImport(AlsaLibrary)]
        public static extern int snd_mixer_selem_set_capture_volume_range(nint elem, long min, long max);
    }
}