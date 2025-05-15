using Newtonsoft.Json;
using SimpleV2ray.Mode;
using System.IO;

namespace SimpleV2ray.Handler
{
    internal class ConfigHandler
    {
        private static string configRes = Global.ConfigFileName;
        private static readonly object objLock = new();

        public static int LoadConfig(ref Config config)
        {
            // 如果配置文件不存在，创建默认配置
            if (!File.Exists(Utils.GetConfigPath(configRes)))
            {
                config = new Config();
                ToJsonFile(config);
                return 0;
            }

            try
            {
                // 从文件加载配置
                string configContent = File.ReadAllText(Utils.GetConfigPath(configRes));
                config = JsonConvert.DeserializeObject<Config>(configContent) ?? new Config();
                
                return 0;
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Loading config failed: " + ex.Message);
                return -1;
            }
        }

        public static int SaveConfig(ref Config config)
        {
            try
            {
                ToJsonFile(config);
                return 0;
            }
            catch (Exception ex)
            {
                Utils.SaveLog("Saving config failed: " + ex.Message);
                return -1;
            }
        }

        private static void ToJsonFile(Config config)
        {
            try
            {
                // 确保配置目录存在
                var configPath = Utils.GetConfigPath();
                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }

                // 保存为JSON文件
                string jsonStr = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(Utils.GetConfigPath(configRes), jsonStr);
            }
            catch (Exception ex)
            {
                Utils.SaveLog("ToJsonFile failed: " + ex.Message);
            }
        }

        public static List<ProfileItem> GetProfiles()
        {
            Config config = new Config();
            LoadConfig(ref config);
            return config.profileItems ?? new List<ProfileItem>();
        }

        public static int AddCustomServer(ref Config config, ProfileItem profileItem, bool blDelete)
        {
            var fileName = profileItem.address;
            if (!File.Exists(fileName))
            {
                Utils.SaveLog($"AddCustomServer: 文件不存在 {fileName}");
                return -1;
            }

            Utils.SaveLog($"AddCustomServer: 处理文件 {fileName}, 扩展名 {Path.GetExtension(fileName)}");
            
            // 检查是否处理已转换的文件(位于Config目录下的转换后文件)
            bool isConvertedFile = profileItem.configType == "converted_yaml";
            
            string newFileName;
            
            if (isConvertedFile)
            {
                // 如果是已转换的文件，保持完整路径不变
                Utils.SaveLog($"AddCustomServer: 检测到已转换的配置文件，不再复制文件，保留完整路径");
                newFileName = fileName; // 保留完整路径
                
                // 重置configType以便正常使用
                profileItem.configType = "custom";
            }
            else
            {
                // 正常处理流程 - 复制文件到Config目录
                var ext = Path.GetExtension(fileName);
                newFileName = $"{Utils.GetGUID()}{ext}";
                string configPath = Utils.GetConfigPath();
                string fullDestPath = Path.Combine(configPath, newFileName);
                
                Utils.SaveLog($"AddCustomServer: 配置目录路径: {configPath}");
                Utils.SaveLog($"AddCustomServer: 新文件完整路径: {fullDestPath}");

                try
                {
                    // 确保配置目录存在
                    if (!Directory.Exists(configPath))
                    {
                        Directory.CreateDirectory(configPath);
                        Utils.SaveLog($"AddCustomServer: 创建配置目录: {configPath}");
                    }

                    Utils.SaveLog($"AddCustomServer: 复制文件 {fileName} 到 {fullDestPath}");
                    File.Copy(fileName, fullDestPath, true); // 允许覆盖已有文件
                    
                    if (blDelete)
                    {
                        File.Delete(fileName);
                        Utils.SaveLog($"AddCustomServer: 删除原文件 {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    Utils.SaveLog($"AddCustomServer failed: {ex.Message}, StackTrace: {ex.StackTrace}");
                    return -1;
                }
            }

            // 更新配置项
            if (!isConvertedFile)
            {
                // 只有未转换的文件才设置为简单文件名
                profileItem.address = newFileName;
            }
            // 否则保留已经设置好的完整路径，即 profileItem.address = finalConfigPath
            
            // 无论是否是转换后的文件，都设置为custom类型
            profileItem.configType = "custom";

            if (string.IsNullOrEmpty(profileItem.remarks))
            {
                profileItem.remarks = $"import custom@{DateTime.Now.ToShortDateString()}";
            }
            
            Utils.SaveLog($"AddCustomServer: 设置配置类型为 {profileItem.configType}, 文件名为 {profileItem.address}");
            
            try 
            {
                // 添加到配置文件
                int result = AddServerCommon(ref config, profileItem);
                Utils.SaveLog($"AddCustomServer: AddServerCommon结果 {result}");
                return result;
            }
            catch (Exception ex)
            {
                Utils.SaveLog($"AddCustomServer: 保存配置时出错: {ex.Message}");
                return -1;
            }
        }

        public static int EditCustomServer(ref Config config, ProfileItem profileItem)
        {
            Utils.SaveLog($"EditCustomServer: 开始编辑服务器 ID={profileItem.indexId}, 类型={profileItem.configType}");
            // 在实际应用中这里需要更新数据库
            // 这里简化为直接更新配置文件中的项
            if (config.profileItems == null)
            {
                config.profileItems = new List<ProfileItem>();
                Utils.SaveLog("EditCustomServer: 配置项列表为空，创建新列表");
            }

            var index = config.profileItems.FindIndex(p => p.indexId == profileItem.indexId);
            Utils.SaveLog($"EditCustomServer: 查找服务器索引结果 {index}");
            
            if (index >= 0)
            {
                config.profileItems[index] = profileItem;
                SaveConfig(ref config);
                Utils.SaveLog($"EditCustomServer: 更新服务器成功 {profileItem.remarks}");
                return 0;
            }
            
            Utils.SaveLog($"EditCustomServer: 未找到要编辑的服务器 ID={profileItem.indexId}");
            return -1;
        }

        public static int AddServerCommon(ref Config config, ProfileItem profileItem)
        {
            if (config.profileItems == null)
            {
                config.profileItems = new List<ProfileItem>();
            }

            if (string.IsNullOrEmpty(profileItem.indexId))
            {
                profileItem.indexId = Utils.GetGUID();
            }

            var index = config.profileItems.FindIndex(p => p.indexId == profileItem.indexId);
            if (index >= 0)
            {
                config.profileItems[index] = profileItem;
            }
            else
            {
                config.profileItems.Add(profileItem);
            }

            SaveConfig(ref config);
            return 0;
        }

        public static int SetDefaultServerIndex(ref Config config, string indexId)
        {
            if (string.IsNullOrEmpty(indexId))
            {
                return -1;
            }

            config.indexId = indexId;
            SaveConfig(ref config);
            return 0;
        }

        public static ProfileItem? GetDefaultServer(ref Config config)
        {
            if (string.IsNullOrEmpty(config.indexId) || config.profileItems == null || config.profileItems.Count == 0)
            {
                return null;
            }

            string targetId = config.indexId;
            return config.profileItems.FirstOrDefault(p => p.indexId == targetId);
        }
    }
} 