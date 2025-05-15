using SimpleV2ray.Handler;
using System.IO;
using System.Windows;
using System.Windows.Resources;

public class TrayHelper : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Window _mainWindow;
    private readonly CoreHandler _coreHandler;
    private bool _firstHide = true;

    public TrayHelper(Window mainWindow, CoreHandler coreHandler)
    {
        _mainWindow = mainWindow;
        _coreHandler = coreHandler;
        // 获取资源流
        var iconUri = new Uri("pack://application:,,,/Resources/app.ico", UriKind.RelativeOrAbsolute);
        StreamResourceInfo sri = System.Windows.Application.GetResourceStream(iconUri);

        if (sri == null)
            throw new FileNotFoundException($"找不到图标资源"); 
        using var iconStream = sri.Stream;
        // 创建托盘图标
        _notifyIcon = new NotifyIcon
        {   
            Icon = new System.Drawing.Icon(iconStream),
            Visible = true,
            Text = "SimpleV2ray 正在运行"
        };

        // 创建托盘右键菜单
        var contextMenu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("显示窗口");
        showItem.Click += (s, e) => { ShowWindow(); };

        var exitItem = new ToolStripMenuItem("退出程序");
        exitItem.Click += (s, e) => { ExitApplication(); };

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => { ShowWindow(); };

        // 绑定窗口事件
        _mainWindow.StateChanged += (s, e) => {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.Hide();
            }
        };

        _mainWindow.Closing += (s, e) => {
            e.Cancel = true;
            _mainWindow.Hide();

            if (_firstHide)
            {
                _firstHide = false;
               
            }
        };
    }

    private void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ExitApplication()
    {
        try
        {
            // 在退出前停止代理服务
            _coreHandler.CoreStop();
        }
        catch (Exception)
        {

          
        }
      

        // 清理托盘图标资源
        Dispose();

        // 退出应用程序
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}