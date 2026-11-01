using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using AppBar.Core.Models;
using AppBar.Core.Services;
using AppBar.Core.Utilities;
using AppBar.Infrastructure;
using AppBar.Services;
using AppBar.ViewModels;
using AppBar.Windows;
using Microsoft.Win32;

namespace AppBar
{
    public class AppController : IDisposable
    {
        private readonly Application _application;
        private readonly JsonStateStore _stateStore;
        private readonly ShortcutResolver _shortcutResolver;
        private readonly AppLauncher _appLauncher;
        private readonly StartupService _startupService;
        private readonly IconService _iconService;
        private readonly ScreenService _screenService;
        private readonly ObservableCollection<ShortcutItemViewModel> _items;
        private readonly LauncherState _state;
        private BubbleWindow _bubble;
        private PanelWindow _panel;
        private string _lastSaveError;

        public AppController(Application application)
        {
            _application = application;

            string dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppBar");
            _stateStore = new JsonStateStore(dataDirectory);
            _state = _stateStore.Load();
            _state.Normalize();

            _shortcutResolver = new ShortcutResolver();
            _appLauncher = new AppLauncher();
            _startupService = new StartupService(Assembly.GetEntryAssembly().Location);
            _iconService = new IconService(dataDirectory);
            _screenService = new ScreenService();
            _items = new ObservableCollection<ShortcutItemViewModel>();

            foreach (AppShortcut shortcut in _state.Apps)
            {
                _items.Add(new ShortcutItemViewModel(shortcut, _iconService));
            }
        }

        public void Start()
        {
            _bubble = new BubbleWindow(this);
            _bubble.Show();

            _panel = new PanelWindow(this, _items, _screenService);
            _panel.Owner = _bubble;

            ApplyInitialBubblePosition();
            InitializeStartupSetting();

            try
            {
                SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            }
            catch (InvalidOperationException)
            {
            }

            if (!string.IsNullOrWhiteSpace(_stateStore.LastLoadWarning))
            {
                ShowError(_stateStore.LastLoadWarning);
            }

            SaveState(false);

        }

        public void TogglePanel()
        {
            if (_panel == null)
            {
                return;
            }

            if (_panel.IsOpen)
            {
                _panel.CloseAnimated();
            }
            else
            {
                OpenPanel();
            }
        }

        public void OpenPanel()
        {
            if (_panel == null || _panel.IsOpen || _bubble == null)
            {
                return;
            }

            IntPtr bubbleHandle = ScreenService.GetHandle(_bubble);
            ScreenSnapshot screen = _screenService.GetForWindow(bubbleHandle);
            NativeMethods.RECT bubbleBounds = _screenService.GetWindowBounds(bubbleHandle);
            if (bubbleBounds.Width <= 0 || bubbleBounds.Height <= 0)
            {
                return;
            }

            SizedPlacement panelSize =
                WindowGeometry.CalculatePanelSize(screen.WorkArea, screen.DpiScale);
            ScreenBounds bubble = new ScreenBounds(
                bubbleBounds.Left,
                bubbleBounds.Top,
                bubbleBounds.Width,
                bubbleBounds.Height);
            SizedPlacement placement = WindowGeometry.PlacePanel(
                screen.WorkArea,
                bubble,
                panelSize.Width,
                panelSize.Height,
                screen.DpiScale);

            _panel.Open(placement, screen.DpiScale);
        }

        public void ScheduleAutoClose()
        {
            if (_panel != null)
            {
                _panel.ScheduleAutoClose();
            }
        }

        public void CancelAutoClose()
        {
            if (_panel != null)
            {
                _panel.CancelAutoClose();
            }
        }

        public bool IsPointerInsideAppWindows()
        {
            NativeMethods.POINT cursor;
            if (!_screenService.TryGetCursor(out cursor))
            {
                return true;
            }

            if (_bubble != null && IsPointInsideWindow(cursor, ScreenService.GetHandle(_bubble)))
            {
                return true;
            }

            if (_panel != null &&
                _panel.IsOpen &&
                IsPointInsideWindow(cursor, ScreenService.GetHandle(_panel)))
            {
                return true;
            }

            return false;
        }

        internal NativeMethods.RECT GetBubbleBounds()
        {
            if (_bubble == null)
            {
                return new NativeMethods.RECT();
            }

            return _screenService.GetWindowBounds(ScreenService.GetHandle(_bubble));
        }

        public void BubbleDragStarted()
        {
            if (_panel != null && _panel.IsOpen)
            {
                _panel.CloseAnimated();
            }
        }

        public void MoveBubbleTo(int x, int y)
        {
            if (_bubble == null)
            {
                return;
            }

            NativeMethods.RECT current = GetBubbleBounds();
            ScreenSnapshot screen = _screenService.GetForPoint(x, y);
            SizedPlacement clamped = WindowGeometry.ClampBubble(
                x,
                y,
                current.Width,
                current.Height,
                screen.WorkArea);

            _screenService.SetBounds(
                ScreenService.GetHandle(_bubble),
                clamped.X,
                clamped.Y,
                clamped.Width,
                clamped.Height,
                false);

            _state.BubbleX = clamped.X;
            _state.BubbleY = clamped.Y;
            _state.BubblePositionInitialized = true;
        }

        public void CommitBubblePosition()
        {
            _state.BubblePositionInitialized = true;
            SaveState(false);
        }

        public void ShowAddDialog()
        {
            if (_bubble == null)
            {
                return;
            }

            bool panelWasOpen = _panel != null && _panel.IsOpen;
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "添加应用快捷方式",
                Filter = "应用快捷方式 (*.lnk;*.exe)|*.lnk;*.exe",
                CheckFileExists = true,
                Multiselect = true
            };

            Window owner = panelWasOpen ? (Window)_panel : _bubble;
            bool? result = dialog.ShowDialog(owner);
            if (result != true)
            {
                return;
            }

            if (_panel != null && _panel.IsOpen)
            {
                _panel.SetAutoCloseSuppressed(true);
            }

            try
            {
                AddFiles(dialog.FileNames);
                if (!panelWasOpen)
                {
                    OpenPanel();
                }
            }
            finally
            {
                if (_panel != null)
                {
                    _panel.SetAutoCloseSuppressed(false);
                }
            }
        }

        public void AddFiles(IEnumerable<string> paths)
        {
            if (paths == null || _panel == null)
            {
                return;
            }

            int added = 0;
            List<string> firstErrors = new List<string>();

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !_shortcutResolver.IsSupported(path))
                {
                    continue;
                }

                ShortcutResolutionResult resolution = _shortcutResolver.Resolve(path);
                if (!resolution.Success)
                {
                    if (firstErrors.Count < 2)
                    {
                        firstErrors.Add(resolution.ErrorMessage);
                    }

                    continue;
                }

                if (ShortcutResolver.ContainsTarget(_state.Apps, resolution.Shortcut.LaunchPath))
                {
                    continue;
                }

                ShortcutItemViewModel item =
                    new ShortcutItemViewModel(resolution.Shortcut, _iconService);
                _state.Apps.Add(resolution.Shortcut);
                _items.Add(item);
                added++;
            }

            if (added > 0)
            {
                SaveState(false);
            }

            if (added == 0 && firstErrors.Count > 0)
            {
                ShowError(firstErrors[0]);
            }
        }

        public void LaunchShortcut(ShortcutItemViewModel item)
        {
            if (item == null)
            {
                return;
            }

            AppLaunchResult result = _appLauncher.Launch(item.Shortcut);
            if (result.Success)
            {
                if (_panel != null)
                {
                    _panel.CloseAnimated();
                }

                return;
            }

            ShowError(result.ErrorMessage);
        }

        public void PersistShortcutOrder()
        {
            _state.Apps.Clear();
            foreach (ShortcutItemViewModel item in _items)
            {
                _state.Apps.Add(item.Shortcut);
            }

            SaveState(false);
        }

        public void RemoveShortcut(ShortcutItemViewModel item, Window owner)
        {
            if (item == null)
            {
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                owner,
                "从应用栏移除“" + item.DisplayName + "”？\n\n"
                + "只会移除这个应用栏条目，不会删除原始快捷方式或程序。",
                "应用栏",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            _items.Remove(item);
            PersistShortcutOrder();
        }

        public bool IsStartupEnabled()
        {
            try
            {
                return _startupService.IsEnabled();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SetStartupEnabled(bool enabled)
        {
            try
            {
                _startupService.SetEnabled(enabled);
                _state.StartupConfigured = true;
                SaveState(false);
                return _startupService.IsEnabled();
            }
            catch (Exception ex)
            {
                ShowError("无法更新开机启动设置：" + ex.Message);
                return IsStartupEnabled();
            }
        }

        public void ExitApplication()
        {
            SaveState(false);
            Dispose();
            _application.Shutdown(0);
        }

        public void Dispose()
        {
            try
            {
                SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            }
            catch (InvalidOperationException)
            {
            }

            if (_panel != null)
            {
                _panel.CloseImmediate();
            }

            if (_bubble != null)
            {
                _bubble.Close();
            }
        }

        private void ApplyInitialBubblePosition()
        {
            IntPtr handle = ScreenService.GetHandle(_bubble);
            ScreenSnapshot screen = _screenService.GetForWindow(handle);
            NativeMethods.RECT current = _screenService.GetWindowBounds(handle);

            int x;
            int y;
            if (_state.BubblePositionInitialized)
            {
                x = (int)Math.Round(_state.BubbleX);
                y = (int)Math.Round(_state.BubbleY);
            }
            else
            {
                x = (int)Math.Round(24 * screen.DpiScale);
                y = (int)Math.Round(24 * screen.DpiScale);
            }

            SizedPlacement clamped = WindowGeometry.ClampBubble(
                x,
                y,
                current.Width,
                current.Height,
                screen.WorkArea);

            _screenService.SetBounds(
                handle,
                clamped.X,
                clamped.Y,
                clamped.Width,
                clamped.Height,
                false);

            _state.BubbleX = clamped.X;
            _state.BubbleY = clamped.Y;
            _state.BubblePositionInitialized = true;
        }

        private void InitializeStartupSetting()
        {
            if (_state.StartupConfigured)
            {
                return;
            }

            try
            {
                _startupService.SetEnabled(true);
            }
            catch (Exception ex)
            {
                ShowError("无法启用开机启动：" + ex.Message);
            }
            finally
            {
                _state.StartupConfigured = true;
            }

            SaveState(false);
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            if (_application == null)
            {
                return;
            }

            _application.Dispatcher.BeginInvoke(
                new Action(EnsureBubbleVisible),
                DispatcherPriority.Background);
        }

        private void EnsureBubbleVisible()
        {
            if (_bubble == null)
            {
                return;
            }

            IntPtr handle = ScreenService.GetHandle(_bubble);
            NativeMethods.RECT current = _screenService.GetWindowBounds(handle);
            ScreenSnapshot screen = _screenService.GetForPoint(
                (int)Math.Round(_state.BubbleX + (current.Width / 2.0)),
                (int)Math.Round(_state.BubbleY + (current.Height / 2.0)));
            SizedPlacement clamped = WindowGeometry.ClampBubble(
                (int)Math.Round(_state.BubbleX),
                (int)Math.Round(_state.BubbleY),
                current.Width,
                current.Height,
                screen.WorkArea);

            _screenService.SetBounds(
                handle,
                clamped.X,
                clamped.Y,
                clamped.Width,
                clamped.Height,
                false);
            _state.BubbleX = clamped.X;
            _state.BubbleY = clamped.Y;
            SaveState(false);
        }

        private void SaveState(bool showFailure)
        {
            try
            {
                _stateStore.Save(_state);
                _lastSaveError = string.Empty;
            }
            catch (Exception ex)
            {
                if (showFailure || !string.Equals(_lastSaveError, ex.Message, StringComparison.Ordinal))
                {
                    _lastSaveError = ex.Message;
                    ShowError("无法保存应用栏设置：" + ex.Message);
                }
            }
        }

        private bool IsPointInsideWindow(NativeMethods.POINT point, IntPtr hwnd)
        {
            NativeMethods.RECT bounds = _screenService.GetWindowBounds(hwnd);
            return point.X >= bounds.Left &&
                   point.X <= bounds.Right &&
                   point.Y >= bounds.Top &&
                   point.Y <= bounds.Bottom &&
                   bounds.Width > 0 &&
                   bounds.Height > 0;
        }

        private void ShowError(string message)
        {
            Window owner = _panel != null && _panel.IsVisible ? (Window)_panel : _bubble;
            if (owner != null)
            {
                MessageBox.Show(
                    owner,
                    message,
                    "应用栏",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    message,
                    "应用栏",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

    }
}
