using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SimpleV2ray.Mode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace SimpleV2ray.Handler
{
    /// <summary>
    /// Core进程处理类
    /// </summary>
    public class CoreHandler
    {
        private static string _coreCConfigRes = Global.coreConfigFileName;
        private int _processId = 0;
        private List<int> _additionalProcessIds = new List<int>();
        private Process? _process;
        private Action<bool, string>? _updateFunc;
        private string _tempConfigFile = string.Empty; // 保存临时配置文件路径
        private List<string> _tempFiles = new List<string>();
        private readonly string _binPath;
        private readonly string _logPath;
        private string _currentCoreExe = string.Empty; // 当前正在运行的核心可执行文件名
        private ProfileItem _currentNode = null;
        private string _configString = string.Empty;
        
        // 添加日志输出事件
        public event EventHandler<LogEventArgs> OutputDataReceived;

        // 添加一个静态字典来跟踪每个端口的检测状态
        private static Dictionary<int, bool> _portCheckingStatus = new Dictionary<int, bool>();
        private static readonly object _portCheckLock = new object();

        public CoreHandler(Action<bool, string> update)
        {
            _updateFunc = update;
            _binPath = Utils.GetBinPath();
            _logPath = Utils.GetLogPath();

            Environment.SetEnvironmentVariable("v2ray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("xray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        }
        
        // 添加无参构造函数
        public CoreHandler()
        {
            _binPath = Utils.GetBinPath();
            _logPath = Utils.GetLogPath();

            Environment.SetEnvironmentVariable("v2ray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("xray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        }
        
        // 触发日志事件的方法，但不通过LogHandler.AddLog记录，避免循环
        private void RaiseOutputDataReceived(string content)
        {
            // 只触发事件，不记录日志，避免循环
            OutputDataReceived?.Invoke(this, new LogEventArgs { content = content });
        }

        public void LoadCore(Config config)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    CoreStop();
                }
                
                if (config.profileItems == null || config.profileItems.Count == 0)
                {
                    if (_updateFunc != null)
                        _updateFunc(false, "没有可用的服务器配置");
                    LogHandler.AddLog("配置文件中没有服务器配置");
                    return;
                }
                
                var guid = string.IsNullOrEmpty(config.indexId) ? string.Empty : config.indexId;
                LogHandler.AddLog($"尝试加载服务器配置，indexId: {guid}, 配置中有 {config.profileItems.Count} 个服务器");
                
                // 服务器ID为空时，默认使用第一个服务器
                if (string.IsNullOrEmpty(guid) && config.profileItems.Count > 0)
                {
                    guid = config.profileItems[0].indexId;
                    LogHandler.AddLog($"没有指定服务器ID，使用第一个服务器: {guid}");
                }
                
                // 查找对应的服务器配置
                var node = config.profileItems.FirstOrDefault(t => t.indexId == guid);
                if (node == null)
                {
                    // 如果找不到指定ID的服务器，尝试使用第一个服务器
                    if (config.profileItems.Count > 0)
                    {
                        node = config.profileItems[0];
                        LogHandler.AddLog($"未找到指定ID的服务器，使用第一个服务器: {node.indexId}, 名称: {node.remarks}");
                    }
                    else
                    {
                        if (_updateFunc != null)
                            _updateFunc(false, "未找到对应的服务器配置");
                        LogHandler.AddLog("未找到对应的服务器配置，无法启动");
                        return;
                    }
                }
                else
                {
                    LogHandler.AddLog($"找到服务器配置: {node.indexId}, 名称: {node.remarks}, 地址: {node.address}");
                }
                
                // 使用配置类型区分处理方法
                switch (node.configType)
                {
                    case "custom":
                        LogHandler.AddLog($"启动自定义配置: {node.remarks}, 核心类型: {node.coreType}");
                        StartWithCustomConfig(node);
                        break;
                    
                    default:
                        if (_updateFunc != null)
                            _updateFunc(false, $"不支持的配置类型：{node.configType}");
                        LogHandler.AddLog($"不支持的配置类型：{node.configType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_updateFunc != null)
                    _updateFunc(false, $"启动时出错：{ex.Message}");
                LogHandler.AddLog($"启动异常：{ex}");
            }
        }

        private void StartWithCustomConfig(ProfileItem node)
        {
            try
            {
                if (string.IsNullOrEmpty(node.address))
                {
                    if (_updateFunc != null)
                        _updateFunc(false, "配置文件路径为空");
                    LogHandler.AddLog("配置文件路径为空");
                    return;
                }
                
                // 获取配置文件路径
                string filePath = node.address;
                
                // 检查是否为绝对路径或相对路径
                if (!Path.IsPathRooted(filePath))
                {
                    // 如果是相对路径，则尝试从配置目录中加载
                    filePath = Utils.GetConfigPath(filePath);
                    LogHandler.AddLog($"相对路径转换为: {filePath}");
                }
                
                LogHandler.AddLog($"尝试加载配置文件: {filePath}");
                
                if (!File.Exists(filePath))
                {
                    if (_updateFunc != null)
                        _updateFunc(false, $"配置文件不存在：{filePath}");
                    LogHandler.AddLog($"配置文件不存在：{filePath}");
                    return;
                }
                
                if (string.IsNullOrEmpty(node.coreType))
                {
                    node.coreType = "v2fly";
                    LogHandler.AddLog($"未指定核心类型，默认使用: {node.coreType}");
                }
                
                // 检查核心文件是否存在
                string coreExe = DetermineCoreExe(node.coreType.ToLower());
                if (coreExe == null)
                {
                    if (_updateFunc != null)
                        _updateFunc(false, $"找不到核心文件，请下载对应的核心文件到bin目录");
                    LogHandler.AddLog($"找不到核心文件，请下载{node.coreType}核心文件到bin目录");
                    return;
                }
                
                LogHandler.AddLog($"使用核心文件: {coreExe}");
                
                if (node.coreType.ToLower().Contains("clash"))
                {
                    LogHandler.AddLog($"检测到Clash核心，使用Clash专用处理逻辑");
                    StartWithClashConfig(node, filePath, coreExe);
                    return;
                }
                
                // 对于V2/Xray等核心，直接生成Socks配置并启动
                if (node.preSocksPort <= 0)
                {
                    node.preSocksPort = 1080; // 默认使用1080端口
                    LogHandler.AddLog($"未指定Socks端口，使用默认端口: {node.preSocksPort}");
                }
                
                // 检查端口是否可用
                if (!IsPortAvailable(node.preSocksPort))
                {
                    if (_updateFunc != null)
                        _updateFunc(false, $"端口 {node.preSocksPort} 已被占用，请尝试其他端口");
                    LogHandler.AddLog($"端口 {node.preSocksPort} 已被占用，请尝试其他端口");
                    return;
                }
                
                string tmpConfig = Path.Combine(
                    Path.GetTempPath(), 
                    "SimpleV2ray", 
                    $"v2ray_config_{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json"
                );
                
                // 确保临时目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(tmpConfig));
                LogHandler.AddLog($"创建临时配置文件: {tmpConfig}");
                
                // 确定配置文件路径
                var socksConfig = GenerateSocksConfig(node, filePath);
                
                // 写入临时配置文件
                File.WriteAllText(tmpConfig, socksConfig);
                LogHandler.AddLog($"写入临时配置文件成功，长度: {socksConfig.Length} 字符");
                
                // 启动核心
                CoreStartViaConfig(coreExe, tmpConfig, node);
            }
            catch (Exception ex)
            {
                if (_updateFunc != null)
                    _updateFunc(false, $"启动自定义配置时出错：{ex.Message}");
                LogHandler.AddLog($"启动自定义配置异常：{ex}");
            }
        }

        private void StartWithClashConfig(ProfileItem node, string configPath, string coreExe)
        {
            try
            {
                LogHandler.AddLog($"开始启动Clash配置: {node.remarks}, 文件路径: {configPath}");
                
                if (node.preSocksPort <= 0)
                {
                    node.preSocksPort = 1080; // 默认使用1080端口
                    LogHandler.AddLog($"未指定Socks端口，使用默认端口: {node.preSocksPort}");
                }
                
                // 检查端口是否可用
                if (!IsPortAvailable(node.preSocksPort))
                {
                    if (_updateFunc != null)
                        _updateFunc(false, $"端口 {node.preSocksPort} 已被占用，请尝试其他端口");
                    LogHandler.AddLog($"端口 {node.preSocksPort} 已被占用，请尝试其他端口");
                    return;
                }
                
                string tmpConfig = Path.Combine(
                    Path.GetTempPath(), 
                    "SimpleV2ray", 
                    $"clash_config_{Path.GetFileNameWithoutExtension(configPath)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.yaml"
                );
                
                // 确保临时目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(tmpConfig));
                LogHandler.AddLog($"创建临时配置文件: {tmpConfig}");
                
                // 修改Clash配置
                ModifyClashConfig(configPath, tmpConfig, node);
                LogHandler.AddLog($"已修改配置并保存至临时文件: {tmpConfig}");
                
                // 设置Clash专用启动参数
                var arguments = $"-f \"{tmpConfig}\"";
                LogHandler.AddLog($"Clash启动参数: {arguments}");
                
                // 将所有代理端口预先标为未激活
                LogHandler.AddLog($"代理端口数量: {node.clashProxyPorts.Count}");
                foreach (var port in node.clashProxyPorts)
                {
                    port.isActive = false;
                    LogHandler.AddLog($"预设端口 {port.port} 为未激活状态");
                }
                
                // 启动Clash核心
                LogHandler.AddLog($"正在启动Clash代理 ({node.coreType})，端口: {node.preSocksPort}");
                CoreStartViaString(coreExe, arguments, node);
            }
            catch (Exception ex)
            {
                if (_updateFunc != null)
                    _updateFunc(false, $"启动Clash配置时出错：{ex.Message}");
                LogHandler.AddLog($"启动Clash配置异常：{ex}");
            }
        }

        private void ModifyClashConfig(string source, string dest, ProfileItem node)
        {
            try
            {
                // 清空现有端口配置
                node.clashProxyPorts.Clear();
                
                string fileExt = Path.GetExtension(source).ToLower();
                if (fileExt == ".json")
                {
                    string originalJson = File.ReadAllText(source);
                    var config = JObject.Parse(originalJson);
                    
                    // 提取listeners信息
                    if (config["listeners"] is JArray listeners && listeners.Count > 0)
                    {
                        LogHandler.AddLog($"从JSON配置文件中发现 {listeners.Count} 个监听器");
                        
                        foreach (JObject listener in listeners)
                        {
                            try
                            {
                                string name = listener["name"]?.ToString() ?? "未命名";
                                string type = listener["type"]?.ToString() ?? "未知";
                                int port = listener["port"]?.Value<int>() ?? 0;
                                string proxy = listener["proxy"]?.ToString() ?? "全局";
                                
                                if (port > 0)
                                {
                                    var proxyPort = new ClashProxyPort
                                    {
                                        name = name,
                                        type = type,
                                        port = port,
                                        proxy = proxy,
                                        isActive = false
                                    };
                                    node.clashProxyPorts.Add(proxyPort);
                                    LogHandler.AddLog($"添加监听器: {name}, 类型: {type}, 端口: {port}, 代理: {proxy}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"解析监听器信息失败: {ex.Message}");
                            }
                        }
                    }
                    
                    // 设置日志级别
                    config["log-level"] = "info";
                    
                    // 保存修改后的配置
                    string modifiedJson = config.ToString();
                    File.WriteAllText(dest, modifiedJson);
                }
                else if (fileExt == ".yaml" || fileExt == ".yml")
                {
                    // 读取YAML文件
                    string yamlContent = File.ReadAllText(source);
                    
                    // 使用正则表达式提取listeners信息
                    var listenerMatches = Regex.Matches(yamlContent, @"listeners:(?:\s*-\s*name:\s*([^\n]*)\s*type:\s*([^\n]*)\s*port:\s*(\d+)(?:\s*proxy:\s*([^\n]*))?)+", RegexOptions.Singleline);
                    
                    if (listenerMatches.Count > 0)
                    {
                        // 更详细的匹配单个listener
                        var singleListenerMatches = Regex.Matches(yamlContent, @"-\s*name:\s*([^\n]*)\s*type:\s*([^\n]*)\s*port:\s*(\d+)(?:\s*proxy:\s*([^\n]*))?", RegexOptions.Multiline);
                        
                        LogHandler.AddLog($"从YAML配置文件中发现 {singleListenerMatches.Count} 个监听器");
                        
                        foreach (Match match in singleListenerMatches)
                        {
                            try
                            {
                                if (match.Groups.Count >= 4)
                                {
                                    string name = match.Groups[1].Value.Trim();
                                    string type = match.Groups[2].Value.Trim();
                                    int port = int.Parse(match.Groups[3].Value.Trim());
                                    string proxy = match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value) 
                                        ? match.Groups[4].Value.Trim() 
                                        : "全局";
                                    
                                    if (port > 0)
                                    {
                                        var proxyPort = new ClashProxyPort
                                        {
                                            name = name,
                                            type = type,
                                            port = port,
                                            proxy = proxy,
                                            isActive = false
                                        };
                                        node.clashProxyPorts.Add(proxyPort);
                                        LogHandler.AddLog($"添加监听器: {name}, 类型: {type}, 端口: {port}, 代理: {proxy}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"解析监听器信息失败: {ex.Message}");
                            }
                        }
                    }
                    
                    // 如果没有找到监听器配置，尝试检查传统的端口配置
                    if (node.clashProxyPorts.Count == 0)
                    {
                        // 尝试匹配port, socks-port, mixed-port
                        var portMatch = Regex.Match(yamlContent, @"port:\s*(\d+)", RegexOptions.Multiline);
                        var socksPortMatch = Regex.Match(yamlContent, @"socks-port:\s*(\d+)", RegexOptions.Multiline);
                        var mixedPortMatch = Regex.Match(yamlContent, @"mixed-port:\s*(\d+)", RegexOptions.Multiline);
                        
                        if (portMatch.Success && portMatch.Groups.Count > 1)
                        {
                            int port = int.Parse(portMatch.Groups[1].Value);
                            node.clashProxyPorts.Add(new ClashProxyPort
                            {
                                name = "HTTP代理",
                                type = "http",
                                port = port,
                                proxy = "全局",
                                isActive = false
                            });
                            LogHandler.AddLog($"添加HTTP代理端口: {port}");
                        }
                        
                        if (socksPortMatch.Success && socksPortMatch.Groups.Count > 1)
                        {
                            int port = int.Parse(socksPortMatch.Groups[1].Value);
                            node.clashProxyPorts.Add(new ClashProxyPort
                            {
                                name = "SOCKS代理",
                                type = "socks",
                                port = port,
                                proxy = "全局",
                                isActive = false
                            });
                            LogHandler.AddLog($"添加SOCKS代理端口: {port}");
                        }
                        
                        if (mixedPortMatch.Success && mixedPortMatch.Groups.Count > 1)
                        {
                            int port = int.Parse(mixedPortMatch.Groups[1].Value);
                            node.clashProxyPorts.Add(new ClashProxyPort
                            {
                                name = "混合代理",
                                type = "mixed",
                                port = port,
                                proxy = "全局",
                                isActive = false
                            });
                            LogHandler.AddLog($"添加混合代理端口: {port}");
                        }
                    }
                    
                    // 设置preSocksPort为第一个端口（如果存在）
                    if (node.clashProxyPorts.Count > 0)
                    {
                        node.preSocksPort = node.clashProxyPorts[0].port;
                    }
                    
                    // 直接复制原文件，保留所有配置
                    File.Copy(source, dest, true);
                }
                
                // 显示配置的端口信息
                if (node.clashProxyPorts.Count > 0)
                {
                    LogHandler.AddLog($"配置了 {node.clashProxyPorts.Count} 个监听端口");
                }
                else
                {
                    LogHandler.AddLog("警告：未检测到监听端口配置");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"修改Clash配置文件失败: {ex.Message}");
                // 出错时复制原文件
                File.Copy(source, dest, true);
            }
        }

        public void CoreStartViaConfig(string coreExe, string configPath, ProfileItem node)
        {
            try
            {
                // 检查核心文件是否存在
                if (!File.Exists(coreExe))
                {
                    LogHandler.AddLog($"未找到核心文件: {coreExe}");
                    if (_updateFunc != null)
                        _updateFunc(false, $"未找到核心文件: {coreExe}");
                    return;
                }
                
                LogHandler.AddLog($"开始启动服务: {node.remarks}");
                LogHandler.AddLog($"使用核心: {coreExe}");
                LogHandler.AddLog($"配置文件: {configPath}");
                
                // 保存当前使用的核心可执行文件名
                _currentCoreExe = Path.GetFileName(coreExe);
                // 保存当前节点
                _currentNode = node;
                // 保存配置文件内容
                try {
                    _configString = File.ReadAllText(configPath);
                } catch {
                    _configString = string.Empty;
                }
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = coreExe,
                        Arguments = $"-c \"{configPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(coreExe),
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                
                _process = process;
                bool started = _process.Start();
                
                if (started)
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    
                    LogHandler.AddLog($"服务已启动，进程ID: {_process.Id}");
                    
                    // 检查_updateFunc是否为null
                    if (_updateFunc != null)
                        _updateFunc(true, $"服务启动成功: {node.remarks}");
                    else
                        RaiseOutputDataReceived($"服务启动成功: {node.remarks}");
                    
                    // 异步检查端口监听就绪
                    WaitForPortListening(node.preSocksPort);
                }
                else
                {
                    LogHandler.AddLog("服务启动失败");
                    if (_updateFunc != null)
                        _updateFunc(false, "服务启动失败");
                    else
                        RaiseOutputDataReceived("服务启动失败");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"启动服务异常: {ex}");
                if (_updateFunc != null)
                    _updateFunc(false, $"启动服务异常: {ex.Message}");
                else
                    RaiseOutputDataReceived($"启动服务异常: {ex.Message}");
            }
        }

        public void CoreStartViaString(string coreExe, string arguments, ProfileItem node)
        {
            try
            {
                // 检查核心文件是否存在
                if (!File.Exists(coreExe))
                {
                    LogHandler.AddLog($"未找到核心文件: {coreExe}");
                    if (_updateFunc != null)
                        _updateFunc(false, $"未找到核心文件: {coreExe}");
                    return;
                }
                
                LogHandler.AddLog($"开始启动服务: {node.remarks}");
                LogHandler.AddLog($"使用核心: {coreExe}");
                LogHandler.AddLog($"参数: {arguments}");
                
                // 保存当前使用的核心可执行文件名
                _currentCoreExe = Path.GetFileName(coreExe);
                _currentNode = node;
                _configString = arguments;
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = coreExe,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(coreExe),
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                
                process.OutputDataReceived += Process_ClashOutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                
                _process = process;
                bool started = _process.Start();
                
                if (started)
                {
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    
                    LogHandler.AddLog($"服务已启动，进程ID: {_process.Id}");
                    
                    // 检查_updateFunc是否为null
                    if (_updateFunc != null)
                        _updateFunc(true, $"服务启动成功: {node.remarks}");
                    else
                        RaiseOutputDataReceived($"服务启动成功: {node.remarks}");
                    
                    // 异步检查各端口监听
                    foreach (var port in node.clashProxyPorts)
                    {
                        WaitForPortListening(port.port);
                    }
                }
                else
                {
                    LogHandler.AddLog("服务启动失败");
                    if (_updateFunc != null)
                        _updateFunc(false, "服务启动失败");
                    else
                        RaiseOutputDataReceived("服务启动失败");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"启动服务异常: {ex}");
                if (_updateFunc != null)
                    _updateFunc(false, $"启动服务异常: {ex.Message}");
                else
                    RaiseOutputDataReceived($"启动服务异常: {ex.Message}");
            }
        }

        private void Process_ClashOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                string log = $"[{Path.GetFileNameWithoutExtension(_currentCoreExe)}] {e.Data}";
                LogHandler.AddLog(log);
                RaiseOutputDataReceived(log);

                // 检查端口是否在监听
                if (e.Data.Contains("listening"))
                {
                    if (_currentNode != null)
                    {
                        // 尝试提取端口号
                        try {
                            // 匹配类似 "proxy listening at: [::]:42012" 的消息
                            var match = Regex.Match(e.Data, @"listening.*:(\d+)");
                            if (match.Success && match.Groups.Count > 1)
                            {
                                int port = int.Parse(match.Groups[1].Value);
                                _currentNode.port = port;
                                _currentNode.isRunning = true;
                                LogHandler.AddLog($"端口 {port} 已激活");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHandler.AddLog($"提取端口信息时出错: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog(ex.ToString());
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                string log = $"[{Path.GetFileNameWithoutExtension(_currentCoreExe)}] {e.Data}";
                LogHandler.AddLog(log);
                RaiseOutputDataReceived(log);
            }
            catch (Exception ex)
            {
                LogHandler.AddLog(ex.ToString());
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                string log = $"[{Path.GetFileNameWithoutExtension(_currentCoreExe)}] [Error] {e.Data}";
                LogHandler.AddLog(log);
                RaiseOutputDataReceived(log);
            }
            catch (Exception ex)
            {
                LogHandler.AddLog(ex.ToString());
            }
        }

        private void WaitForPortListening(int port)
        {
            // 检查是否已经在检测此端口
            lock (_portCheckLock)
            {
                if (_portCheckingStatus.ContainsKey(port) && _portCheckingStatus[port])
                {
                    LogHandler.AddLog($"端口 {port} 已在监控中，跳过重复监控");
                    return;
                }
                _portCheckingStatus[port] = true;
            }

            try
            {
                // 启动异步任务来监控端口
                Task.Run(async () =>
                {
                    try
                    {
                        LogHandler.AddLog($"正在监控端口 {port} 启动状态...");
                        
                        // 等待端口变为可用状态，最多等待10秒
                        int maxRetries = 10;
                        bool isActive = false;
                        
                        for (int i = 0; i < maxRetries; i++)
                        {
                            try
                            {
                                using (var client = new TcpClient())
                                {
                                    // 设置短连接超时时间
                                    var connectTask = client.ConnectAsync("127.0.0.1", port);
                                    // 等待连接完成，但设置2秒超时
                                    if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                                    {
                                        isActive = client.Connected;
                                        
                                        if (isActive)
                                        {
                                            LogHandler.AddLog($"端口 {port} 已激活");
                                            break;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // 连接失败，端口可能还未就绪
                            }
                            
                            // 等待1秒再尝试
                            await Task.Delay(1000);
                        }
                        
                        if (isActive)
                        {
                            // 尝试查找和更新对应端口的状态
                            // 创建一个独立的配置副本
                            Config config = new Config();
                            ConfigHandler.LoadConfig(ref config);
                            
                            if (config.profileItems != null && !string.IsNullOrEmpty(config.indexId))
                            {
                                var server = config.profileItems.FirstOrDefault(p => p.indexId == config.indexId);
                                if (server != null)
                                {
                                    // 找到对应的端口并标记为活跃
                                    var portItem = server.clashProxyPorts.FirstOrDefault(p => p.port == port);
                                    if (portItem != null)
                                    {
                                        portItem.isActive = true;
                                        LogHandler.AddLog($"已将端口 {port} 标记为活跃状态");
                                    }
                                    // 标记服务器为运行状态
                                    server.isRunning = true;
                                    ConfigHandler.SaveConfig(ref config);
                                    
                                    // 主动更新UI状态
                                    _updateFunc?.Invoke(true, $"服务已成功启动，端口 {port} 已激活");
                                }
                            }
                        }
                        else
                        {
                            LogHandler.AddLog($"端口 {port} 启动超时，可能存在问题");
                        }
                    }
                    finally
                    {
                        // 完成检测后，无论成功失败，都重置状态标志
                        lock (_portCheckLock)
                        {
                            _portCheckingStatus[port] = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"监控端口 {port} 时出错: {ex.Message}");
                // 出错时也要重置状态标志
                lock (_portCheckLock)
                {
                    _portCheckingStatus[port] = false;
                }
            }
        }

        public void CoreStop()
        {
            try
            {
                LogHandler.AddLog("正在停止所有服务...");
                
                // 清空端口检测状态
                lock (_portCheckLock)
                {
                    _portCheckingStatus.Clear();
                }
                
                // 停止主进程
                if (_process != null && !_process.HasExited)
                {
                    try
                    {
                        LogHandler.AddLog($"正在停止主进程: PID={_process.Id}");
                        _process.Kill();
                        
                        // 增加等待超时，确保进程有足够时间退出
                        if (!_process.WaitForExit(3000))
                        {
                            LogHandler.AddLog("主进程未能在3秒内退出，将强制终止");
                        }
                        
                        _process.Close();
                        _process.Dispose();
                        _process = null;
                        LogHandler.AddLog("主进程已停止");
                    }
                    catch (Exception ex)
                    {
                        LogHandler.AddLog($"停止主进程时出错: {ex.Message}");
                        // 确保进程变量被清理
                        _process = null;
                    }
                }
                
                // 停止其他进程
                if (_additionalProcessIds.Count > 0)
                {
                    LogHandler.AddLog($"正在停止 {_additionalProcessIds.Count} 个额外进程...");
                    foreach (int pid in _additionalProcessIds)
                    {
                        try
                        {
                            LogHandler.AddLog($"正在停止附加进程: PID={pid}");
                            Process _p = Process.GetProcessById(pid);
                            KillProcess(_p);
                        }
                        catch (Exception ex)
                        {
                            LogHandler.AddLog($"停止进程失败(PID: {pid}): {ex.Message}");
                        }
                    }
                    _additionalProcessIds.Clear();
                    LogHandler.AddLog("所有附加进程已停止");
                }

               // 检查是否有lingering的进程，使用更全面的名称列表
                KillProcessesByName(new string[] {
                    "v2ray", "xray", "clash", "clash-meta",
                    "Clash.Meta", "Clash.Meta-windows-amd64", "Clash.Meta-windows-amd64-compatible"
                });

                // 清理临时配置文件
                if (_tempFiles.Count > 0)
                {
                    LogHandler.AddLog($"正在清理 {_tempFiles.Count} 个临时文件...");
                    foreach (string tempFile in _tempFiles)
                    {
                        if (File.Exists(tempFile))
                        {
                            try 
                            { 
                                File.Delete(tempFile);
                                LogHandler.AddLog($"已删除临时文件: {Path.GetFileName(tempFile)}");
                            } 
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"清理临时文件失败: {ex.Message}");
                            }
                        }
                    }
                    _tempFiles.Clear();
                }
                
                // 更新所有服务器的运行状态
                try
                {
                    var config = new Config();
                    ConfigHandler.LoadConfig(ref config);
                    
                    if (config.profileItems != null)
                    {
                        bool updated = false;
                        foreach (var item in config.profileItems)
                        {
                            if (item.isRunning)
                            {
                                item.isRunning = false;
                                updated = true;
                            }
                            
                            // 重置端口活跃状态
                            foreach (var port in item.clashProxyPorts)
                            {
                                port.isActive = false;
                            }
                        }
                        
                        if (updated)
                        {
                            ConfigHandler.SaveConfig(ref config);
                            LogHandler.AddLog("已更新所有服务器的运行状态");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHandler.AddLog($"更新服务器状态时出错: {ex.Message}");
                }
                
                // 额外检查网络端口释放
               // CheckAndReleaseCommonPorts();
                
                if (_updateFunc != null)
                    _updateFunc(true, "所有服务已停止");
                else
                    RaiseOutputDataReceived("所有服务已停止");
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"停止服务时出错：{ex.Message}");
                if (_updateFunc != null)
                    _updateFunc(false, $"停止服务时出错：{ex.Message}");
                else
                    RaiseOutputDataReceived($"停止服务时出错：{ex.Message}");
            }
        }

        // 检查并释放常用端口的方法
        private void CheckAndReleaseCommonPorts()
        {
            try
            {
                // 检查常用的Clash端口
                int[] commonPorts = new int[] { 1080, 7890, 7891, 9090 };
                
                foreach (var port in commonPorts)
                {
                    if (!IsPortAvailable(port))
                    {
                        LogHandler.AddLog($"检测到端口 {port} 仍在使用中，尝试释放...");
                        // 此处可以添加额外的端口释放逻辑，如使用netsh命令强制关闭端口
                        // 由于需要管理员权限，这里只记录日志
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"检查网络端口时出错: {ex.Message}");
            }
        }

        private void KillProcessesByName(string[] processNames)
        {
            try
            {
                foreach (var processName in processNames)
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        LogHandler.AddLog($"发现 {processes.Length} 个遗留的 {processName} 进程，正在停止...");
                        
                        foreach (var process in processes)
                        {
                            try
                            {
                                process.Kill();
                                process.WaitForExit(500);
                                LogHandler.AddLog($"已停止进程：{processName}, PID={process.Id}");
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"停止进程 {processName} 时出错：{ex.Message}");
                            }
                            finally
                            {
                                process.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"查找进程时出错：{ex.Message}");
            }
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
                
                if (tcpConnInfoArray.Any(endpoint => endpoint.Port == port))
                {
                    return false;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateSocksConfig(ProfileItem node, string configPath)
        {
            var inbounds = new[] {
                new {
                    tag = "socks-in",
                    port = node.preSocksPort,
                    protocol = "socks",
                    settings = new {
                        udp = true,
                        auth = "noauth",
                        userLevel = 8
                    },
                    sniffing = new {
                        enabled = true,
                        destOverride = new[] { "http", "tls" }
                    },
                    listen = "127.0.0.1"
                }
            };
            
            var outbounds = new[] {
                new {
                    tag = "proxy",
                    protocol = "freedom",
                    settings = new {},
                    streamSettings = new {
                        sockopt = new {
                            mark = 255
                        }
                    },
                    proxySettings = new {
                        tag = "direct"
                    }
                }
            };
            
            var routing = new {
                domainStrategy = "AsIs",
                rules = new[] {
                    new {
                        type = "field",
                        inboundTag = new[] { "socks-in" },
                        outboundTag = "proxy"
                    }
                }
            };
            
            // 构建配置并将其序列化为JSON字符串
            var config = new {
                log = new {
                    access = Path.Combine(_logPath, "access.log"),
                    error = Path.Combine(_logPath, "error.log"),
                    loglevel = "warning"
                },
                inbounds,
                outbounds,
                routing,
                others = new {
                    v2raya_extras = new {
                        original_config = configPath
                    }
                }
            };
            
            return JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private string? DetermineCoreExe(string coreType)
        {
            if (string.IsNullOrEmpty(coreType))
            {
                LogHandler.AddLog("未指定核心类型，默认使用xray");
                coreType = "xray";
            }

            // 根据coreType确定可执行文件
            string exeName = coreType.ToLower();
            
            // 获取可能的可执行文件名称列表
            List<string> possibleExeNames = new List<string>();
            
            switch (exeName)
            {
                case "v2fly":
                    possibleExeNames.Add("v2ray.exe");
                    possibleExeNames.Add("wv2ray.exe");
                    break;
                    
                case "xray":
                    possibleExeNames.Add("xray.exe");
                    possibleExeNames.Add("wxray.exe");
                    break;
                    
                case "clash_meta":
                    // 按照v2rayN的匹配顺序
                    possibleExeNames.Add("Clash.Meta-windows-amd64-compatible.exe");
                    possibleExeNames.Add("Clash.Meta-windows-amd64.exe");
                    possibleExeNames.Add("Clash.Meta-windows-386.exe");
                    possibleExeNames.Add("Clash.Meta.exe");
                    possibleExeNames.Add("clash-meta.exe");  // 我们自己添加的名称
                    possibleExeNames.Add("clash.exe");  // 兼容方案，最后尝试
                    break;
                    
                case "clash":
                    possibleExeNames.Add("clash-windows-amd64-v3.exe");
                    possibleExeNames.Add("clash-windows-amd64.exe");
                    possibleExeNames.Add("clash-windows-386.exe");
                    possibleExeNames.Add("clash.exe");
                    break;
                    
                default:
                    // 默认只添加与coreType同名的exe
                    possibleExeNames.Add($"{exeName}.exe");
                    break;
            }
            
            // 尝试查找所有可能的可执行文件
            foreach (string exeFileName in possibleExeNames)
            {
                string exePath = Utils.GetBinPath(exeFileName);
                if (File.Exists(exePath))
                {
                    LogHandler.AddLog($"找到核心文件: {exePath}");
                    return exePath;
                }
            }
            
            // 没有找到任何匹配的可执行文件
            LogHandler.AddLog($"找不到核心可执行文件: {string.Join(", ", possibleExeNames)}");
            LogHandler.AddLog($"请下载并放置合适的核心文件到: {Utils.GetBinPath()}");
            return null;
        }

        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            // 先触发UI事件
            RaiseOutputDataReceived(msg);
            
            // 兼容旧的回调方式
            _updateFunc?.Invoke(updateToTrayTooltip, msg);
            
            // 写入日志文件，但不触发事件
            try
            {
                string formattedLog = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} - {msg}";
                string logFile = Path.Combine(Utils.GetLogPath(), $"SimpleV2ray_{DateTime.Now.ToString("yyyyMMdd")}.log");
                File.AppendAllText(logFile, formattedLog + Environment.NewLine);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        private void KillProcess(Process p)
        {
            try
            {
                ShowMsg(false, $"正在终止进程: {p.Id}");
                p.CloseMainWindow();
                if (!p.HasExited)
                {
                    p.Kill();
                    p.WaitForExit(1000); // 等待进程退出
                    ShowMsg(false, $"进程已终止: {p.Id}");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog("KillProcess: " + ex.Message);
                ShowMsg(false, $"终止进程失败: {ex.Message}");
            }
        }
    }
} 