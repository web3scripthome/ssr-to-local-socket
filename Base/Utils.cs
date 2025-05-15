using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SimpleV2ray
{
    internal class Utils
    {
        /// <summary>
        /// 获取配置目录路径
        /// </summary>
        /// <returns></returns>
        public static string GetConfigPath()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleV2ray");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// 获取配置文件完整路径
        /// </summary>
        /// <param name="filename">文件名</param>
        /// <returns></returns>
        public static string GetConfigPath(string filename)
        {
            // 如果传入的是完整路径且文件存在，直接返回
            if (File.Exists(filename) && Path.IsPathRooted(filename))
            {
                return filename;
            }
            
            // 确保文件名不包含路径分隔符
            filename = Path.GetFileName(filename);
            return Path.Combine(GetConfigPath(), filename);
        }

        /// <summary>
        /// 获取核心目录路径
        /// </summary>
        /// <returns></returns>
        public static string GetBinPath()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// 获取核心文件完整路径
        /// </summary>
        /// <param name="filename">文件名</param>
        /// <param name="coreType">核心类型</param>
        /// <returns></returns>
        public static string GetBinPath(string filename, string? coreType = null)
        {
            if (string.IsNullOrEmpty(filename))
            {
                if (string.IsNullOrEmpty(coreType))
                {
                    return GetBinPath();
                }
                else
                {
                    string path = Path.Combine(GetBinPath(), coreType);
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    return path;
                }
            }
            
            string binPath = GetBinPath();
            if (!string.IsNullOrEmpty(coreType))
            {
                binPath = Path.Combine(binPath, coreType);
                if (!Directory.Exists(binPath))
                {
                    Directory.CreateDirectory(binPath);
                }
            }
            return Path.Combine(binPath, filename);
        }

        /// <summary>
        /// 获取唯一标识符
        /// </summary>
        /// <returns></returns>
        public static string GetGUID()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 判断字符串是否为空
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 保存日志
        /// </summary>
        /// <param name="strContent">日志内容</param>
        public static void SaveLog(string strContent)
        {
            try
            {
                string logPath = Path.Combine(GetConfigPath(), "logs");
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                string logFile = Path.Combine(logPath, $"runtime_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {strContent}\r\n");
            }
            catch { }
        }
        
        /// <summary>
        /// 从文件中加载JSON或YAML内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容</returns>
        public static string LoadFileContent(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    SaveLog($"文件不存在: {filePath}");
                    return string.Empty;
                }
                
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                SaveLog($"读取文件失败: {ex.Message}");
                return string.Empty;
            }
        }
        
        /// <summary>
        /// 获取MD5哈希值
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>MD5哈希值</returns>
        public static string GetMD5(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        /// <returns></returns>
        public static string GetLogPath()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleV2ray", "logs");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// 获取日志文件完整路径
        /// </summary>
        /// <param name="filename">文件名</param>
        /// <returns></returns>
        public static string GetLogPath(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return GetLogPath();
            }
            return Path.Combine(GetLogPath(), filename);
        }
    }
} 