namespace Eyu.Audio.PTP
{
    public static class FlagsField
    {
        /// <summary>
        /// 0000 0000 0000 0001
        /// </summary>
        public static int PTP_LI_61 = 0X1;
        /// <summary>
        /// 0000 0000 0000 0010
        /// </summary>
        public static int PTP_LI_59 = 0x2;
        /// <summary>
        /// 0000 0000 0000 0100
        /// </summary>
        public static int PTP_UTC_REASONABLE = 0x4;
        /// <summary>
        /// 0000 0000 0000 1000
        /// </summary>
        public static int PTP_TIMESCALE = 0x8;
        /// <summary>
        /// 0000 0000 0001 0000
        /// </summary>
        public static int TIME_TRACEABLE = 0x10;
        /// <summary>
        /// 0000 0000 0010 0000
        /// </summary>
        public static int FREQUENCY_TRACEABLE = 0x20;

        /// <summary>
        /// 如果发送侧端口处于MASTER状态，则为FALSE。
        /// 0000 0001 0000 0000
        /// </summary>
        public static int PTP_ALTERNATE_MASTER = 0x100;
        /// <summary>
        /// byte[0],bit[1] 
        /// <list type="table">
        /// <item>1 Two-step clock</item>
        /// <item>0 One-step clock</item>
        /// </list>
        /// 0000 0010 0000 0000
        /// </summary>
        public static int PTP_TWO_STEP = 0x200;
        /// <summary>
        /// byte[0],bit[2]
        /// <list type="table">
        /// <item>1 此消息发送到的传送层协议地址是一个单播地址</item>
        /// <item>0 此消息发送到的传送层协议地址是一个多播地址</item>
        /// </list>
        /// 0000 0100 0000 0000
        /// </summary>
        public static int PTP_UNICAST = 0x400;
        /// <summary>
        /// 0010 0000 0000 0000
        /// </summary>
        public static int PTP_Profile_Specific_1 = 0x2000;
        /// <summary>
        /// 0100 0000 0000 0000
        /// </summary>
        public static int PTP_Profile_Specific_2 = 0x4000;
        /// <summary>
        /// 1000 0000 0000 0000
        /// </summary>
        public static int PTP_SECURITY = 0x8000;

    }


}
