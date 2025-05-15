using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SimpleV2ray.Mode
{
    /// <summary>
    /// 服务器节点Item
    /// </summary>
    public class ProfileItem : INotifyPropertyChanged
    {
        private bool _isRunning = false;
        
        public string? indexId { get; set; } = Guid.NewGuid().ToString();
        public string? remarks { get; set; } = string.Empty;
        public string? address { get; set; } = string.Empty;
        public int port { get; set; } = 1080;
        public string configType { get; set; } = "custom";
        public string? coreType { get; set; } = string.Empty;
        public int preSocksPort { get; set; } = 0;
        public bool displayLog { get; set; } = true;
        
        public bool isRunning 
        { 
            get { return _isRunning; }
            set 
            { 
                _isRunning = value; 
                OnPropertyChanged(nameof(isRunning));
            }
        }
        
        public ObservableCollection<ClashProxyPort> clashProxyPorts { get; set; } = new ObservableCollection<ClashProxyPort>();
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 配置文件类型
    /// </summary>
    public static class EConfigType
    {
        public const string Custom = "custom";
        public const string Vmess = "vmess";
        public const string Shadowsocks = "shadowsocks";
        public const string Socks = "socks";
        public const string Vless = "vless";
        public const string Trojan = "trojan";
        public const string VlessXtls = "vlessxtls";
    }

    /// <summary>
    /// 核心类型
    /// </summary>
    public static class ECoreType
    {
        public const string v2rayN = "v2rayN";
        public const string v2fly = "v2fly";
        public const string SagerNet = "SagerNet";
        public const string Xray = "Xray";
        public const string v2fly_v5 = "v2fly_v5";
        public const string clash = "clash";
        public const string clash_meta = "clash_meta";
        public const string hysteria = "hysteria";
        public const string naiveproxy = "naiveproxy";
        public const string tuic = "tuic";
        public const string sing_box = "sing_box";
    }

    /// <summary>
    /// Clash代理端口信息
    /// </summary>
    public class ClashProxyPort : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _type = string.Empty;
        private int _port = 0;
        private string _proxy = string.Empty;
        private bool _isActive = false;

        public string name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(name));
            }
        }

        public string type
        {
            get { return _type; }
            set
            {
                _type = value;
                OnPropertyChanged(nameof(type));
            }
        }

        public int port
        {
            get { return _port; }
            set
            {
                _port = value;
                OnPropertyChanged(nameof(port));
            }
        }

        public string proxy
        {
            get { return _proxy; }
            set
            {
                _proxy = value;
                OnPropertyChanged(nameof(proxy));
            }
        }

        public bool isActive
        {
            get { return _isActive; }
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(isActive));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 