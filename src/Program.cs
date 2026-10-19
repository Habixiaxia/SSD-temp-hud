using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SsdTempHud
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\SsdTempHud", out createdNew))
            {
                if (!createdNew)
                {
                    StartupLog.Write("another instance is already running; exit");
                    return;
                }

                try
                {
                    StartupLog.Write("boot");
                    System.Windows.Forms.Application.EnableVisualStyles();

                    Application app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    AppController controller = new AppController();

                    app.Startup += delegate
                    {
                        try
                        {
                            controller.Start();
                        }
                        catch (Exception ex)
                        {
                            StartupLog.Write("startup failed: " + ex);
                            MessageBox.Show("启动失败：\n\n" + ex,
                                "SsdTempHud", MessageBoxButton.OK, MessageBoxImage.Error);
                            app.Shutdown(1);
                        }
                    };

                    app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                    {
                        StartupLog.Write("unhandled: " + e.Exception);
                        MessageBox.Show("运行出错：\n\n" + e.Exception,
                            "SsdTempHud", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Handled = true;
                    };

                    app.Exit += delegate { controller.Stop(); };

                    app.Run();
                    StartupLog.Write("run loop exited");
                }
                catch (Exception ex)
                {
                    StartupLog.Write("fatal: " + ex);
                    MessageBox.Show("程序无法启动：\n\n" + ex,
                        "SsdTempHud", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
