namespace SimpleV2ray
{
    internal class Global
    {
        // 常量
        public const string ConfigFileName = "guiNConfig.json";
        public const string coreConfigFileName = "config.json";
        public const string Loopback = "127.0.0.1";
        public const string userEMail = "t@t.tt";
        public const string MyRegPath = "Software\\SimpleV2ray";
        public const string CustomIconName = "v2ray.ico";

        // 核心类型
        public static readonly List<string> coreTypes = new() { "v2fly", "SagerNet", "Xray", "v2fly_v5" };
    }
} 