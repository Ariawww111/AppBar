using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AppBar
{
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;
        private bool _ownsMutex;
        private AppController _controller;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _singleInstanceMutex = new Mutex(true, @"Local\AppBar.SingleInstance.8D52094B", out createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                Shutdown(0);
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            try
            {
                _controller = new AppController(this);
                _controller.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "应用栏启动失败：\n" + ex.Message,
                    "应用栏",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_controller != null)
            {
                _controller.Dispose();
            }

            if (_ownsMutex && _singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }
            }

            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.Dispose();
            }

            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "应用栏遇到未处理错误：\n" + e.Exception.Message,
                "应用栏",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}

