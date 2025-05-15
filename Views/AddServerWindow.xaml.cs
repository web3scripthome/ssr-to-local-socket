using Microsoft.Win32;
using SimpleV2ray.Handler;
using SimpleV2ray.Mode;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.Linq;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace SimpleV2ray.Views
{
    public partial class AddServerWindow : Window
    {
        private ProfileItem _profileItem;
        private Config _config = new Config();

        public AddServerWindow(ProfileItem profileItem)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            
            _profileItem = profileItem;
            
            // 加载Core类型 
            cmbCoreType.Items.Add("clash_meta"); 
            
            // 如果是编辑模式，加载已有的数据
            if (!string.IsNullOrEmpty(_profileItem.indexId))
            {
                txtRemarks.Text = _profileItem.remarks;
                txtAddress.Text = _profileItem.address;
                cmbCoreType.SelectedItem = _profileItem.coreType;
                chkDisplayLog.IsChecked = _profileItem.displayLog;
                txtPreSocksPort.Text = _profileItem.preSocksPort.ToString();
            }
            else
            {
                // 默认值
                chkDisplayLog.IsChecked = true;
                txtPreSocksPort.Text = "1080";  // 设置默认端口为1080
            }
            
            ConfigHandler.LoadConfig(ref _config);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtRemarks.Text))
                {
                    MessageBox.Show("请填写别名");
                    return;
                }

                if (string.IsNullOrEmpty(txtAddress.Text))
                {
                    MessageBox.Show("请选择配置文件地址");
                    return;
                }

                // 确认文件存在
                bool fileExists = File.Exists(txtAddress.Text);
                LogHandler.AddLog($"检查文件是否存在: {txtAddress.Text}, 结果: {fileExists}");
                if (!fileExists)
                {
                    MessageBox.Show($"文件不存在: {txtAddress.Text}");
                    return;
                }

                // 处理Socks端口
                int socksPort = 1080; // 默认值
                if (int.TryParse(txtPreSocksPort.Text, out int port))
                {
                    if (port < 0 || port > 65535)
                    {
                        MessageBox.Show("端口范围无效，请输入0-65535之间的值");
                        return;
                    }
                    socksPort = port;
                    _profileItem.preSocksPort = port;
                    _profileItem.port = port;  // 同时设置port属性，确保两者保持一致
                    
                    LogHandler.AddLog($"设置Socks端口为 {port}");
                }
                else if (!string.IsNullOrEmpty(txtPreSocksPort.Text))
                {
                    MessageBox.Show("请输入有效的端口号");
                    return;
                }

                // 检查是否为YAML文件，尝试自动转换为Clash配置
                string extension = Path.GetExtension(txtAddress.Text).ToLowerInvariant();
                string fileContent = Utils.LoadFileContent(txtAddress.Text);
                bool isYamlFile = (extension == ".yml" || extension == ".yaml");
                
                string finalConfigPath = txtAddress.Text;
                
                // 如果是YAML文件，检查是否需要转换
                if (isYamlFile && !string.IsNullOrEmpty(fileContent))
                {
                    bool needsConversion = false;
                    
                    // 检查是否缺少listeners或其他必要的Clash配置
                    if (!fileContent.Contains("listeners:"))
                    {
                        needsConversion = true;
                        LogHandler.AddLog("检测到YAML文件需要转换为Clash配置");
                    }
                    
                    if (needsConversion)
                    {
                        try
                        {
                            // 确保Config目录存在
                            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                            if (!Directory.Exists(configDir))
                            {
                                Directory.CreateDirectory(configDir);
                                LogHandler.AddLog($"创建配置目录: {configDir}");
                            }
                            
                            // 生成转换后文件的唯一名称（使用GUID确保唯一性）
                            string fileName = $"{Path.GetFileNameWithoutExtension(txtAddress.Text)}_converted_{Guid.NewGuid().ToString("N").Substring(0, 8)}.yaml";
                            string convertedFilePath = Path.Combine(configDir, fileName);
                            
                            // 执行转换
                            bool conversionResult = YamlConfigConverter.ConvertClashConfig(txtAddress.Text, convertedFilePath, socksPort);
                            
                            if (conversionResult)
                            {
                                // 直接使用转换后的文件，而不是原始文件
                                finalConfigPath = convertedFilePath;
                                LogHandler.AddLog($"Clash配置文件已成功转换并保存到: {finalConfigPath}");
                                
                                // 自动选择合适的核心类型
                                string detectedContent = Utils.LoadFileContent(convertedFilePath);
                                if (!string.IsNullOrEmpty(detectedContent))
                                {
                                    bool isClashMeta = detectedContent.Contains("premium-providers:") 
                                        || detectedContent.Contains("geosite:") 
                                        || detectedContent.Contains("geodata-mode:")
                                        || detectedContent.Contains("rule-providers:");
                                    
                                    cmbCoreType.SelectedItem = isClashMeta ? "clash_meta" : "clash";
                                    LogHandler.AddLog($"自动设置核心类型为: {cmbCoreType.SelectedItem}");
                                }
                                
                                // 创建一个标记，告诉ConfigHandler不要再复制此文件
                                _profileItem.configType = "converted_yaml"; // 临时标记
                            }
                            else
                            {
                                // 转换失败但继续使用原始文件
                                LogHandler.AddLog("配置文件转换失败，将使用原始文件");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHandler.AddLog($"转换配置文件时出错: {ex.Message}");
                            // 转换出错，继续使用原始文件
                        }
                    }
                }
                
                // 更新ProfileItem的address为最终路径
                _profileItem.address = finalConfigPath;
                
                // 更新UI显示，确保用户能看到转换后的文件路径
                txtAddress.Text = finalConfigPath;
                
                // 添加额外日志确认地址已更新
                LogHandler.AddLog($"已更新服务器配置文件路径: {finalConfigPath}");
                
                // 更新ProfileItem的其他属性
                _profileItem.remarks = txtRemarks.Text;
                _profileItem.coreType = cmbCoreType.SelectedItem?.ToString() ?? "";
                _profileItem.displayLog = chkDisplayLog.IsChecked ?? false;
                
                // 输出当前配置项的详细信息
                LogHandler.AddLog($"保存服务器信息: ID={_profileItem.indexId}, 类型={_profileItem.configType}, 核心={_profileItem.coreType}, 地址={_profileItem.address}, 端口={_profileItem.port}");
                
                // 确保配置类型正确
                if (string.IsNullOrEmpty(_profileItem.configType))
                {
                    _profileItem.configType = "custom";
                    LogHandler.AddLog("配置类型为空，已设置为custom");
                }

                // 检查服务器ID是否存在于配置中
                bool serverExists = false;
                if (!string.IsNullOrEmpty(_profileItem.indexId))
                {
                    // 检查配置中是否存在此ID
                    if (_config.profileItems != null)
                    {
                        serverExists = _config.profileItems.Any(p => p.indexId == _profileItem.indexId);
                        LogHandler.AddLog($"检查服务器ID是否存在: {_profileItem.indexId}, 结果: {serverExists}");
                    }
                }

                // 如果ID为空或者服务器不存在，则走添加流程
                if (string.IsNullOrEmpty(_profileItem.indexId) || !serverExists)
                {
                    // 新建模式
                    _profileItem.indexId = Guid.NewGuid().ToString();
                    LogHandler.AddLog($"新建服务器，生成ID: {_profileItem.indexId}");
                    
                    int result = ConfigHandler.AddCustomServer(ref _config, _profileItem, false);
                    LogHandler.AddLog($"添加服务器结果: {result} (0=成功, -1=失败)");
                    
                    if (result == 0)
                    {
                        LogHandler.AddLog($"添加服务器成功：{_profileItem.remarks}");
                        this.DialogResult = true;
                    }
                    else
                    {
                        LogHandler.AddLog($"添加服务器失败，检查文件路径: {_profileItem.address}");
                        MessageBox.Show("添加服务器失败");
                    }
                }
                else
                {
                    // 确认是编辑模式，且ID确实存在于配置中
                    LogHandler.AddLog($"编辑服务器: ID={_profileItem.indexId}");
                    
                    int result = ConfigHandler.EditCustomServer(ref _config, _profileItem);
                    LogHandler.AddLog($"编辑服务器结果: {result} (0=成功, -1=失败)");
                    
                    if (result == 0)
                    {
                        LogHandler.AddLog($"编辑服务器成功：{_profileItem.remarks}");
                        this.DialogResult = true;
                    }
                    else
                    {
                        LogHandler.AddLog($"编辑服务器失败，检查服务器ID: {_profileItem.indexId}");
                        MessageBox.Show("编辑服务器失败");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"保存服务器时异常: {ex.Message}");
                MessageBox.Show($"保存服务器时出错: {ex.Message}");
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog fileDialog = new OpenFileDialog
                {
                    Multiselect = false,
                    Filter = "Config|*.yaml;*.yml|All|*.*"
                };
                
                if (fileDialog.ShowDialog() == true)
                {
                    // 获取完整的文件路径
                    string fullPath = fileDialog.FileName;
                    LogHandler.AddLog($"选择的文件: {fullPath}");

                    // 检查文件是否存在
                    if (!File.Exists(fullPath))
                    {
                        LogHandler.AddLog($"警告: 选择的文件不存在: {fullPath}");
                        MessageBox.Show($"选择的文件不存在: {fullPath}", "文件错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 设置地址文本框
                    txtAddress.Text = fullPath;
                    
                    // 如果别名为空，自动使用文件名作为别名
                    if (string.IsNullOrEmpty(txtRemarks.Text))
                    {
                        txtRemarks.Text = Path.GetFileNameWithoutExtension(fullPath);
                        LogHandler.AddLog($"自动设置别名: {txtRemarks.Text}");
                    }
                    
                    // 检测文件类型，自动设置核心类型
                    string extension = Path.GetExtension(fullPath).ToLower();
                    LogHandler.AddLog($"文件扩展名: {extension}");
                    
                    // 读取文件内容
                    string fileContent = Utils.LoadFileContent(fullPath);
                    if (string.IsNullOrEmpty(fileContent))
                    {
                        LogHandler.AddLog("警告: 文件内容为空");
                        MessageBox.Show("文件内容为空或读取失败", "文件错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    LogHandler.AddLog($"成功读取文件内容，长度: {fileContent.Length} 字符");
                    
                    // 清空现有的代理端口列表
                    _profileItem.clashProxyPorts.Clear();
                    LogHandler.AddLog("已清空现有代理端口列表");
                    
                    if (!string.IsNullOrEmpty(fileContent))
                    {
                        if (extension == ".yaml" || extension == ".yml")
                        {
                            // 检测YAML是否为Clash配置
                            if (fileContent.Contains("proxies:") || fileContent.Contains("listeners:"))
                            {
                                bool isClashMeta = fileContent.Contains("premium-providers:") 
                                    || fileContent.Contains("geosite:") 
                                    || fileContent.Contains("geodata-mode:")
                                    || fileContent.Contains("rule-providers:");
                                
                                cmbCoreType.SelectedItem = isClashMeta ? "clash_meta" : "clash";
                                LogHandler.AddLog($"检测到Clash{(isClashMeta ? ".Meta" : "")}配置文件");
                                
                                // 检测端口配置
                                List<int> detectedPorts = new List<int>();
                                
                                // 首先检查listeners部分（优先级最高）
                                var listenerMatches = Regex.Matches(fileContent, @"- name: (.+?)\s+type: (.+?)\s+port: (\d+)", RegexOptions.Multiline);
                                if (listenerMatches.Count > 0)
                                {
                                    LogHandler.AddLog($"检测到 {listenerMatches.Count} 个监听器配置");
                                    foreach (Match match in listenerMatches)
                                    {
                                        if (match.Groups.Count >= 4)
                                        {
                                            string name = match.Groups[1].Value.Trim();
                                            string type = match.Groups[2].Value.Trim();
                                            int port = int.Parse(match.Groups[3].Value.Trim());
                                            
                                            if (!detectedPorts.Contains(port))
                                            {
                                                detectedPorts.Add(port);
                                                LogHandler.AddLog($"检测到监听器: {name}, 类型: {type}, 端口: {port}");
                                                
                                                // 创建并添加代理端口信息
                                                var proxyPort = new ClashProxyPort
                                                {
                                                    name = name,
                                                    type = type,
                                                    port = port,
                                                    proxy = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "全局",
                                                    isActive = false
                                                };
                                                _profileItem.clashProxyPorts.Add(proxyPort);
                                            }
                                        }
                                    }
                                }
                                
                                // 提取mixed-port（统一端口）
                                Match mixedPortMatch = Regex.Match(fileContent, @"mixed-port:\s*(\d+)");
                                if (mixedPortMatch.Success)
                                {
                                    int port = int.Parse(mixedPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到mixed-port: {port}");
                                        
                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "混合代理",
                                            type = "mixed",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }
                                
                                // 提取socks-port
                                Match socksPortMatch = Regex.Match(fileContent, @"socks-port:\s*(\d+)");
                                if (socksPortMatch.Success)
                                {
                                    int port = int.Parse(socksPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到socks-port: {port}");
                                        
                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "SOCKS代理",
                                            type = "socks",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }
                                
                                // 提取port（HTTP端口）
                                Match portMatch = Regex.Match(fileContent, @"^port:\s*(\d+)", RegexOptions.Multiline);
                                if (portMatch.Success)
                                {
                                    int port = int.Parse(portMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到HTTP port: {port}");
                                        
                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "HTTP代理",
                                            type = "http",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }
                                
                                // 设置默认端口为第一个检测到的端口，或者使用默认值
                                if (detectedPorts.Count > 0)
                                {
                                    txtPreSocksPort.Text = detectedPorts[0].ToString();
                                    
                                    // 如果是编辑模式，更新ProfileItem的端口设置
                                    _profileItem.preSocksPort = detectedPorts[0];
                                }
                                else
                                {
                                    txtPreSocksPort.Text = "7890";  // Clash默认端口
                                    _profileItem.preSocksPort = 7890;
                                }
                                
                                LogHandler.AddLog($"已设置主端口: {_profileItem.preSocksPort}, 共检测到 {_profileItem.clashProxyPorts.Count} 个端口");
                            }
                        }
                        else if (extension == ".json")
                        {
                            // 检测JSON是否为V2Ray配置
                            if (fileContent.Contains("\"inbounds\"") && fileContent.Contains("\"outbounds\""))
                            {
                                cmbCoreType.SelectedItem = "v2fly";
                                LogHandler.AddLog("检测到V2Ray配置文件");
                                
                                // 尝试从配置中提取预设端口
                                Match inboundMatch = Regex.Match(fileContent, @"""port"":\s*(\d+)");
                                if (inboundMatch.Success)
                                {
                                    int port = int.Parse(inboundMatch.Groups[1].Value);
                                    txtPreSocksPort.Text = port.ToString();
                                    _profileItem.preSocksPort = port;
                                    LogHandler.AddLog($"检测到端口: {port}");
                                }
                            }
                            // 检测是否为Clash JSON配置
                            else if (fileContent.Contains("\"proxies\"") || fileContent.Contains("\"Proxy\"") || fileContent.Contains("\"listeners\""))
                            {
                                cmbCoreType.SelectedItem = "clash";
                                LogHandler.AddLog("检测到Clash JSON配置文件");
                                
                                // 检测端口配置
                                List<int> detectedPorts = new List<int>();
                                
                                // 首先检查listeners部分
                                try
                                {
                                    var listenerRegex = Regex.Match(fileContent, @"""listeners""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                                    if (listenerRegex.Success && listenerRegex.Groups.Count > 1)
                                    {
                                        string listenersSection = listenerRegex.Groups[1].Value;
                                        var listenerMatches = Regex.Matches(listenersSection, @"""name""\s*:\s*""(.*?)""\s*,.*?""type""\s*:\s*""(.*?)""\s*,.*?""port""\s*:\s*(\d+)", RegexOptions.Singleline);
                                        
                                        LogHandler.AddLog($"检测到 {listenerMatches.Count} 个监听器配置");
                                        
                                        foreach (Match match in listenerMatches)
                                        {
                                            if (match.Groups.Count >= 4)
                                            {
                                                string name = match.Groups[1].Value;
                                                string type = match.Groups[2].Value;
                                                int port = int.Parse(match.Groups[3].Value);
                                                
                                                if (!detectedPorts.Contains(port))
                                                {
                                                    detectedPorts.Add(port);
                                                    LogHandler.AddLog($"检测到监听器: {name}, 类型: {type}, 端口: {port}");
                                                    
                                                    // 提取proxy值（如果有）
                                                    string proxy = "全局";
                                                    var proxyMatch = Regex.Match(match.Value, @"""proxy""\s*:\s*""(.*?)""");
                                                    if (proxyMatch.Success)
                                                    {
                                                        proxy = proxyMatch.Groups[1].Value;
                                                    }
                                                    
                                                    // 创建并添加代理端口信息
                                                    var proxyPort = new ClashProxyPort
                                                    {
                                                        name = name,
                                                        type = type,
                                                        port = port,
                                                        proxy = proxy,
                                                        isActive = false
                                                    };
                                                    _profileItem.clashProxyPorts.Add(proxyPort);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHandler.AddLog($"解析JSON监听器配置失败: {ex.Message}");
                                }
                                
                                // 尝试从配置中提取mixed-port
                                Match mixedPortMatch = Regex.Match(fileContent, @"""mixed-port"":\s*(\d+)");
                                if (mixedPortMatch.Success)
                                {
                                    int port = int.Parse(mixedPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到混合端口: {port}");
                                        
                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "混合代理",
                                            type = "mixed",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }
                                
                                // 尝试提取socks-port
                                Match socksPortMatch = Regex.Match(fileContent, @"""socks-port"":\s*(\d+)");
                                if (socksPortMatch.Success)
                                {
                                    int port = int.Parse(socksPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到Socks端口: {port}");
                                        
                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "SOCKS代理",
                                            type = "socks",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }
                                
                                // 尝试提取http端口
                                Match httpPortMatch = Regex.Match(fileContent, @"""port"":\s*(\d+)");
                                if (httpPortMatch.Success)
                                {
                                    int port = int.Parse(httpPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到HTTP端口: {port}");
                                        
                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "HTTP代理",
                                            type = "http",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }
                                
                                // 设置默认端口为第一个检测到的端口，或者使用默认值
                                if (detectedPorts.Count > 0)
                                {
                                    txtPreSocksPort.Text = detectedPorts[0].ToString();
                                    _profileItem.preSocksPort = detectedPorts[0];
                                }
                                else
                                {
                                    txtPreSocksPort.Text = "7890";  // Clash默认端口
                                    _profileItem.preSocksPort = 7890;
                                }
                                
                                LogHandler.AddLog($"已设置主端口: {_profileItem.preSocksPort}, 共检测到 {_profileItem.clashProxyPorts.Count} 个端口");
                            }
                        }
                    }
                    LogHandler.AddLog($"文件处理完成: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"选择文件时出错: {ex.Message}, 堆栈: {ex.StackTrace}");
                MessageBox.Show($"选择文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtAddress.Text))
            {
                MessageBox.Show("请先选择配置文件");
                return;
            }

            if (File.Exists(txtAddress.Text))
            {
                try
                {
                    System.Diagnostics.Process.Start("notepad.exe", txtAddress.Text);
                    LogHandler.AddLog($"正在编辑配置文件: {txtAddress.Text}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开配置文件失败: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("配置文件不存在");
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void btnImportClash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtAddress.Text) || !File.Exists(txtAddress.Text))
                {
                    MessageBox.Show("请先选择一个原始的Clash配置文件", "配置缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string sourceFile = txtAddress.Text;
                string fileExt = Path.GetExtension(sourceFile).ToLowerInvariant();
                
                if (fileExt != ".yml" && fileExt != ".yaml")
                {
                    MessageBox.Show("选择的文件不是YAML格式", "格式错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取要保存的目标文件名称
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "YAML|*.yaml;*.yml",
                    Title = "保存转换后的Clash配置文件",
                    FileName = Path.GetFileNameWithoutExtension(sourceFile) + "_converted.yaml"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return;
                }

                // 解析Socks端口
                int socksPort = 1080; // 默认值
                if (int.TryParse(txtPreSocksPort.Text, out int port))
                {
                    socksPort = port;
                }

                // 转换配置文件
                bool result = YamlConfigConverter.ConvertClashConfig(sourceFile, saveDialog.FileName, socksPort);
                
                if (result)
                {
                    // 更新UI
                    txtAddress.Text = saveDialog.FileName;
                    cmbCoreType.SelectedItem = "clash"; // 默认选择clash
                    
                    // 如果别名为空，则设置别名
                    if (string.IsNullOrEmpty(txtRemarks.Text))
                    {
                        txtRemarks.Text = Path.GetFileNameWithoutExtension(saveDialog.FileName);
                    }
                    
                    MessageBox.Show("Clash配置文件已成功转换并保存", "转换成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("无法转换Clash配置文件，请查看日志了解详情", "转换失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"导入Clash配置时发生异常: {ex.Message}");
                MessageBox.Show($"导入Clash配置时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 