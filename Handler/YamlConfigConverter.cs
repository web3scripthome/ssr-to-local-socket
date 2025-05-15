using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using SimpleV2ray.Mode;

namespace SimpleV2ray.Handler
{
    public class YamlConfigConverter
    {
        /// <summary>
        /// 转换Clash配置文件到SimpleV2ray需要的格式
        /// </summary>
        /// <param name="sourcePath">原始Clash配置文件路径</param>
        /// <param name="destinationPath">目标配置文件路径</param>
        /// <param name="socksPort">SOCKS端口，也将用作listener端口的起始值</param>
        /// <returns>成功返回true，失败返回false</returns>
        public static bool ConvertClashConfig(string sourcePath, string destinationPath, int socksPort)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    LogHandler.AddLog($"源配置文件不存在: {sourcePath}");
                    return false;
                }

                // 读取源YAML文件
                string sourceYaml = File.ReadAllText(sourcePath);
                
                LogHandler.AddLog($"正在转换Clash配置 {sourcePath} 到 {destinationPath}, 使用Socks端口 {socksPort} 作为起始端口");
                
                // 解析YAML
                var input = new StringReader(sourceYaml);
                var yaml = new YamlStream();
                yaml.Load(input);
                
                // 获取根映射节点
                var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
                
                // 创建新的YAML文档
                var newYaml = new YamlStream();
                var newDocument = new YamlDocument(new YamlMappingNode());
                newYaml.Add(newDocument);
                var newRootNode = (YamlMappingNode)newDocument.RootNode;
                
                // 设置基本属性
                newRootNode.Add("allow-lan", new YamlScalarNode("true"));
                
                // 添加DNS配置
                var dnsNode = new YamlMappingNode();
                dnsNode.Add("enable", new YamlScalarNode("true"));
                dnsNode.Add("enhanced-mode", new YamlScalarNode("fake-ip"));
                dnsNode.Add("fake-ip-range", new YamlScalarNode("198.18.0.1/16"));
                
                var defaultNameservers = new YamlSequenceNode();
                defaultNameservers.Add(new YamlScalarNode("114.114.114.114"));
                dnsNode.Add("default-nameserver", defaultNameservers);
                
                var nameservers = new YamlSequenceNode();
                nameservers.Add(new YamlScalarNode("https://doh.pub/dns-query"));
                dnsNode.Add("nameserver", nameservers);
                
                newRootNode.Add("dns", dnsNode);
                
                // 处理proxies节点
                if (!rootNode.Children.ContainsKey(new YamlScalarNode("proxies")))
                {
                    LogHandler.AddLog("警告: 未找到代理配置节点(proxies)");
                    return false;
                }
                
                // 获取原始代理配置
                var originalProxies = (YamlSequenceNode)rootNode.Children[new YamlScalarNode("proxies")];
                
                // 创建新的代理节点列表
                var newProxies = new YamlSequenceNode();
                
                // 创建listeners节点
                var listenersNode = new YamlSequenceNode();
                
                // 处理每个代理节点
                int portCounter = 0;
                List<string> proxyNames = new List<string>();
                
                foreach (var proxyNode in originalProxies)
                {
                    if (proxyNode is YamlMappingNode originalProxy)
                    {
                        // 获取代理名称
                        string proxyName = string.Empty;
                        if (originalProxy.Children.ContainsKey(new YamlScalarNode("name")))
                        {
                            proxyName = originalProxy.Children[new YamlScalarNode("name")].ToString().Trim('"');
                            
                            // 清除国旗符号 (例如 🇺🇸)
                            proxyName = RemoveFlagEmoji(proxyName);
                            
                            // 更新原始节点中的名称
                            originalProxy.Children[new YamlScalarNode("name")] = new YamlScalarNode(proxyName);
                            
                            proxyNames.Add(proxyName);
                        }
                        
                        // 创建新的代理节点
                        var newProxy = new YamlMappingNode();
                        
                        // 从inline格式转换为展开格式
                        foreach (var property in originalProxy.Children)
                        {
                            newProxy.Add(property.Key, property.Value);
                        }
                        
                        // 添加到新的代理列表
                        newProxies.Add(newProxy);
                        
                        // 为每个代理创建一个对应的listener
                        if (!string.IsNullOrEmpty(proxyName))
                        {
                            var listenerNode = new YamlMappingNode();
                            listenerNode.Add("name", new YamlScalarNode($"mixed{portCounter}"));
                            listenerNode.Add("type", new YamlScalarNode("mixed"));
                            // 使用socksPort作为起始端口
                            listenerNode.Add("port", new YamlScalarNode((socksPort + portCounter).ToString()));
                            listenerNode.Add("proxy", new YamlScalarNode(proxyName));
                            
                            listenersNode.Add(listenerNode);
                            portCounter++;
                        }
                    }
                }
                
                // 添加listeners节点
                newRootNode.Add("listeners", listenersNode);
                
                // 添加代理节点
                newRootNode.Add("proxies", newProxies);
                
                // 创建代理组
                var proxyGroups = new YamlSequenceNode();
                var selectGroup = new YamlMappingNode();
                selectGroup.Add("name", new YamlScalarNode("线路选择"));
                selectGroup.Add("type", new YamlScalarNode("select"));
                
                var selectProxies = new YamlSequenceNode();
                foreach (var name in proxyNames)
                {
                    selectProxies.Add(new YamlScalarNode(name));
                }
                
                selectGroup.Add("proxies", selectProxies);
                proxyGroups.Add(selectGroup);
                
                // 添加代理组节点
                newRootNode.Add("proxy-groups", proxyGroups);
                
                // 保存新的YAML文件
                using (var writer = new StreamWriter(destinationPath))
                {
                    newYaml.Save(writer, false);
                }
                
                LogHandler.AddLog($"成功转换Clash配置文件到: {destinationPath}，使用起始端口: {socksPort}");
                return true;
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"转换Clash配置文件失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 处理字符串中的表情符号以确保在UI中正确显示
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>处理后的字符串</returns>
        private static string RemoveFlagEmoji(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            string result = input;
            
            // 将转义序列（如\U0001F1FA\U0001F1F8）转换为实际的Unicode字符
            var escapeRegex = new Regex(@"\\U([0-9A-Fa-f]{8})");
            result = escapeRegex.Replace(result, match => {
                if (int.TryParse(match.Groups[1].Value, 
                    System.Globalization.NumberStyles.HexNumber, 
                    null, out int unicodeValue))
                {
                    return char.ConvertFromUtf32(unicodeValue);
                }
                return match.Value;
            });
            
            // 如果需要保留表情符号但确保正确显示，可以到此结束
            // 如果需要移除表情符号，可以取消下面的注释
            
            /*
            // 移除所有特殊符号类别（包括表情符号）
            var emojiRegex = new Regex(@"\p{So}");
            result = emojiRegex.Replace(result, "");
            */
            
            // 移除连续的空格
            result = Regex.Replace(result, @"\s+", " ");
            
            // 移除前后多余的空格
            result = result.Trim();
            
            return result;
        }
        
        private static void CopyNodeIfExists(YamlMappingNode source, YamlMappingNode destination, string key)
        {
            var yamlKey = new YamlScalarNode(key);
            if (source.Children.ContainsKey(yamlKey))
            {
                destination.Add(yamlKey, source.Children[yamlKey]);
                LogHandler.AddLog($"复制配置节点: {key}");
            }
        }
    }
} 