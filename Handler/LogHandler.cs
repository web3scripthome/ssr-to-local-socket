using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleV2ray.Handler
{
    /// <summary>
    /// 日志事件参数类
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        public string content { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 日志处理类
    /// </summary>
    public static class LogHandler
    {
        public delegate void LogReceivedEventHandler(string log);
        public static event LogReceivedEventHandler? OnLogReceived;

        private static readonly string _logPath = Utils.GetLogPath();
        public static event EventHandler<LogEventArgs> UpdateFunc;

        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="content"></param>
        public static void AddLog(string content)
        {
            try
            {
                // 处理特殊字符，如国旗表情等
                content = ProcessLogContent(content);
                
                if (!Directory.Exists(_logPath))
                {
                    Directory.CreateDirectory(_logPath);
                }

                string filePath = $"{_logPath}\\{DateTime.Now:yyyy-MM-dd}.txt";
                string time = DateTime.Now.ToString("HH:mm:ss");
                string log = $"{time} {content}{Environment.NewLine}";
                File.AppendAllText(filePath, log);

                TriggerUpdateFunc(content);
            }
            catch { }
        }

        /// <summary>
        /// 处理日志内容，替换乱码的特殊字符
        /// </summary>
        /// <param name="content">原始日志内容</param>
        /// <returns>处理后的日志内容</returns>
        private static string ProcessLogContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;

            // 替换常见的国旗表情等特殊字符的乱码
            // 美国国旗 🇺🇸
            content = content.Replace("\\xF0\\x9F\\x87\\xBA\\xF0\\x9F\\x87\\xB8", "🇺🇸");
            content = content.Replace("\xF0\x9F\x87\xBA\xF0\x9F\x87\xB8", "🇺🇸");
            
            // 日本国旗 🇯🇵
            content = content.Replace("\\xF0\\x9F\\x87\\xAF\\xF0\\x9F\\x87\\xB5", "🇯🇵");
            content = content.Replace("\xF0\x9F\x87\xAF\xF0\x9F\x87\xB5", "🇯🇵");
            
            // 新加坡国旗 🇸🇬
            content = content.Replace("\\xF0\\x9F\\x87\\xB8\\xF0\\x9F\\x87\\xAC", "🇸🇬");
            content = content.Replace("\xF0\x9F\x87\xB8\xF0\x9F\x87\xAC", "🇸🇬");
            
            // 香港国旗 🇭🇰
            content = content.Replace("\\xF0\\x9F\\x87\\xAD\\xF0\\x9F\\x87\\xB0", "🇭🇰");
            content = content.Replace("\xF0\x9F\x87\xAD\xF0\x9F\x87\xB0", "🇭🇰");
            
            // 台湾国旗 🇹🇼
            content = content.Replace("\\xF0\\x9F\\x87\\xB9\\xF0\\x9F\\x87\\xBC", "🇹🇼");
            content = content.Replace("\xF0\x9F\x87\xB9\xF0\x9F\x87\xBC", "🇹🇼");
            
            // 英国国旗 🇬🇧
            content = content.Replace("\\xF0\\x9F\\x87\\xAC\\xF0\\x9F\\x87\\xA7", "🇬🇧");
            content = content.Replace("\xF0\x9F\x87\xAC\xF0\x9F\x87\xA7", "🇬🇧");
            
            // 替换其他可能出现的乱码字符
            // ...
            
            return content;
        }

        private static void TriggerUpdateFunc(string content)
        {
            try
            {
                if (UpdateFunc != null)
                {
                    // 处理特殊字符
                    content = ProcessLogContent(content);
                    LogEventArgs args = new LogEventArgs
                    {
                        content = content,
                    };
                    UpdateFunc(null, args);
                }
            }
            catch { }
        }
    }
} 