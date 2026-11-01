using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AppBar.Core.Services;
using AppBar.Core.Utilities;
using AppBar.Infrastructure;
using AppBar.Services;
using AppBar.ViewModels;

namespace AppBar.Windows
{
    public partial class PanelWindow : Window
    {
        private const string ShortcutItemFormat = "AppBar.ShortcutItem";

        private readonly AppController _controller;
        private readonly ObservableCollection<ShortcutItemViewModel> _items;
        private readonly ScreenService _screenService;
        private readonly DispatcherTimer _autoCloseTimer;
        private ShortcutItemViewModel _dragSource;
        private Point _dragStartPoint;
        private bool _dragArmed;
        private bool _suppressClick;
        private bool _autoCloseSuppressed;
        private bool _closing;
        private IntPtr _handle;

        public PanelWindow(
            AppController controller,
            ObservableCollection<ShortcutItemViewModel> items,
            ScreenService screenService)
        {
            _controller = controller;
            _items = items;
            _screenService = screenService;

            InitializeComponent();
            DataContext = _items;

            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;
        }

        public bool IsOpen { get; private set; }

        public void Open(SizedPlacement placement, double dpiScale)
        {
            if (dpiScale <= 0)
            {
                dpiScale = 1.0;
            }

            _closing = false;
            _autoCloseTimer.Stop();
            BeginAnimation(OpacityProperty, null);
            PanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);

            Width = placement.Width / dpiScale;
            Height = placement.Height / dpiScale;
            Opacity = 0;
            PanelTranslate.Y = -10;
            IsOpen = true;

            if (!IsVisible)
            {
                Show();
            }

            _handle = ScreenService.GetHandle(this);
            _screenService.SetBounds(
                _handle,
                placement.X,
                placement.Y,
                placement.Width,
                placement.Height,
                true);
            Topmost = true;
            Activate();
            BeginOpenAnimation();
        }

        public void CloseAnimated()
        {
            if (!IsOpen || _closing)
            {
                return;
            }

            _closing = true;
            _autoCloseTimer.Stop();

            Duration duration = new Duration(TimeSpan.FromMilliseconds(140));
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            DoubleAnimation fade = new DoubleAnimation
            {
                To = 0,
                Duration = duration,
                EasingFunction = ease
            };
            DoubleAnimation slide = new DoubleAnimation
            {
                To = -8,
                Duration = duration,
                EasingFunction = ease
            };

            fade.Completed += delegate
            {
                if (_closing)
                {
                    Hide();
                    IsOpen = false;
                    _closing = false;
                }
            };

            BeginAnimation(OpacityProperty, fade);
            PanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                slide);
        }

        public void CloseImmediate()
        {
            _autoCloseTimer.Stop();
            BeginAnimation(OpacityProperty, null);
            PanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);
            Opacity = 0;
            Hide();
            IsOpen = false;
            _closing = false;
        }

        public void ScheduleAutoClose()
        {
            if (!IsOpen || _autoCloseSuppressed)
            {
                return;
            }

            _autoCloseTimer.Stop();
            _autoCloseTimer.Start();
        }

        public void CancelAutoClose()
        {
            _autoCloseTimer.Stop();
        }

        public void SetAutoCloseSuppressed(bool suppressed)
        {
            _autoCloseSuppressed = suppressed;
            if (suppressed)
            {
                _autoCloseTimer.Stop();
            }
        }

        private void BeginOpenAnimation()
        {
            Duration duration = new Duration(TimeSpan.FromMilliseconds(180));
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            BeginAnimation(
                OpacityProperty,
                new DoubleAnimation
                {
                    To = 1,
                    Duration = duration,
                    EasingFunction = ease
                });
            PanelTranslate.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation
                {
                    To = 0,
                    Duration = duration,
                    EasingFunction = ease
                });
        }

        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            _autoCloseTimer.Stop();
            if (!_autoCloseSuppressed && !_controller.IsPointerInsideAppWindows())
            {
                CloseAnimated();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseAnimated();
                e.Handled = true;
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            ScheduleAutoClose();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            _controller.ShowAddDialog();
        }

        private void AppTile_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressClick)
            {
                return;
            }

            ShortcutItemViewModel item =
                ((FrameworkElement)sender).DataContext as ShortcutItemViewModel;
            if (item != null)
            {
                _controller.LaunchShortcut(item);
            }
        }

        private void AppTile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSource = ((FrameworkElement)sender).DataContext as ShortcutItemViewModel;
            _dragStartPoint = e.GetPosition(this);
            _dragArmed = _dragSource != null;
        }

        private void AppTile_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragArmed || _dragSource == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point current = e.GetPosition(this);
            if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _dragArmed = false;
            _suppressClick = true;
            DataObject data = new DataObject(ShortcutItemFormat, _dragSource);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
            _dragSource = null;
            Dispatcher.BeginInvoke(
                new Action(delegate { _suppressClick = false; }),
                DispatcherPriority.ApplicationIdle);
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            SetAutoCloseSuppressed(true);
            _controller.CancelAutoClose();
            e.Effects = GetDropEffect(e.Data);
            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = GetDropEffect(e.Data);
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            SetAutoCloseSuppressed(false);
            _controller.ScheduleAutoClose();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            SetAutoCloseSuppressed(false);

            if (e.Data.GetDataPresent(ShortcutItemFormat))
            {
                ShortcutItemViewModel source =
                    e.Data.GetData(ShortcutItemFormat) as ShortcutItemViewModel;
                ShortcutItemViewModel target = FindItemAt(e.OriginalSource as DependencyObject);
                int oldIndex = source == null ? -1 : _items.IndexOf(source);
                int newIndex = target == null ? _items.Count - 1 : _items.IndexOf(target);

                if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
                {
                    OrderedList.Move(_items, oldIndex, newIndex);
                    _controller.PersistShortcutOrder();
                }

                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null)
                {
                    _controller.AddFiles(paths);
                }

                e.Handled = true;
            }
        }

        private void TileContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            SetAutoCloseSuppressed(true);
            _controller.CancelAutoClose();
        }

        private void TileContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            SetAutoCloseSuppressed(false);
            _controller.ScheduleAutoClose();
        }

        private void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            ShortcutItemViewModel item =
                menuItem == null ? null : menuItem.DataContext as ShortcutItemViewModel;
            if (item != null)
            {
                _controller.RemoveShortcut(item, this);
            }
        }

        private static DragDropEffects GetDropEffect(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                return DragDropEffects.Copy;
            }

            if (data.GetDataPresent(ShortcutItemFormat))
            {
                return DragDropEffects.Move;
            }

            return DragDropEffects.None;
        }

        private static ShortcutItemViewModel FindItemAt(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                FrameworkElement element = current as FrameworkElement;
                if (element != null)
                {
                    ShortcutItemViewModel item =
                        element.DataContext as ShortcutItemViewModel;
                    if (item != null)
                    {
                        return item;
                    }
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
