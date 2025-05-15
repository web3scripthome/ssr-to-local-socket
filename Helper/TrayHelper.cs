using System;
using System.Windows;
using System.Windows.Forms;
using System.ComponentModel;
using SimpleV2ray.Handler;
using SimpleV2ray.Views;
using Application = System.Windows.Application;

namespace SimpleV2ray.Helper
{
    public class TrayHelper : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Window _mainWindow;
        private readonly MainWindow _mainWindowInstance;

        public TrayHelper(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _mainWindowInstance = mainWindow as MainWindow;

            // 创建托盘图标
            _notifyIcon = new NotifyIcon
            {
                Icon = GetIconFromResource(),
                Visible = true,
                Text = "SimpleV2ray 正在运行"
            };

            // 创建托盘右键菜单
            var contextMenu = new ContextMenuStrip();
            
            var showItem = new ToolStripMenuItem("显示窗口");
            showItem.Click += (s, e) => ShowWindow();
            
            var exitItem = new ToolStripMenuItem("退出程序");
            exitItem.Click += (s, e) => ExitApplication();
            
            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(exitItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            // 绑定窗口事件
            _mainWindow.StateChanged += (s, e) => {
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.Hide();
                }
            };
            
            _mainWindow.Closing += MainWindow_Closing;
            
            LogHandler.AddLog("系统托盘已初始化完成");
        }
        
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            _mainWindow.Hide();
            _mainWindow.WindowState = WindowState.Minimized; 
        }

        private System.Drawing.Icon GetIconFromResource()
        {
            try
            {
                return System.Drawing.SystemIcons.Application;
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"加载图标失败: {ex.Message}");
                return System.Drawing.SystemIcons.Application;
            }
        }

        private void ShowWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }
        
        public void ExitApplication()
        {
            try
            {
                bool shouldClose = false;
                
                if (_mainWindowInstance != null)
                {
                    LogHandler.AddLog("安全关闭所有代理服务...");
                    try
                    {
                        ((MainWindow)_mainWindowInstance).CloseAllServices();
                        shouldClose = true;
                    }
                    catch (Exception ex)
                    {
                        LogHandler.AddLog($"关闭代理服务时发生错误: {ex.Message}");
                        System.Windows.MessageBox.Show("关闭代理服务时发生错误，应用程序将强制退出。", "警告", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        shouldClose = true;
                    }
                }
                else
                {
                    LogHandler.AddLog("警告: 主窗口实例为空，无法正常关闭服务。");
                    shouldClose = true;
                }
                
                if (shouldClose)
                {
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"退出应用程序时发生错误: {ex.Message}");
                Application.Current.Shutdown();
            }
        }

        public void Dispose()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"清理托盘图标资源时出错: {ex.Message}");
            }
        }
    }
} 