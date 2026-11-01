using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AppBar.Infrastructure;
using AppBar.Services;

namespace AppBar.Windows
{
    public partial class BubbleWindow : Window
    {
        private readonly AppController _controller;
        private bool _pressed;
        private bool _dragging;
        private NativeMethods.POINT _dragStartCursor;
        private int _dragStartLeft;
        private int _dragStartTop;

        public BubbleWindow(AppController controller)
        {
            _controller = controller;
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            Topmost = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _pressed = true;
            _dragging = false;
            NativeMethods.POINT cursor;
            NativeMethods.GetCursorPos(out cursor);
            _dragStartCursor = cursor;

            NativeMethods.RECT bounds = _controller.GetBubbleBounds();
            _dragStartLeft = bounds.Left;
            _dragStartTop = bounds.Top;
            CaptureMouse();
            e.Handled = true;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_pressed || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            NativeMethods.POINT cursor;
            NativeMethods.GetCursorPos(out cursor);
            int deltaX = cursor.X - _dragStartCursor.X;
            int deltaY = cursor.Y - _dragStartCursor.Y;

            if (!_dragging)
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                double threshold = 4.0 * dpi.DpiScaleX;
                if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) < threshold)
                {
                    return;
                }

                _dragging = true;
                _controller.BubbleDragStarted();
            }

            _controller.MoveBubbleTo(
                _dragStartLeft + deltaX,
                _dragStartTop + deltaY);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            ReleaseMouseCapture();
            if (_dragging)
            {
                _dragging = false;
                _controller.CommitBubblePosition();
            }
            else
            {
                _controller.TogglePanel();
            }

            e.Handled = true;
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _controller.CancelAutoClose();
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_pressed)
            {
                _controller.ScheduleAutoClose();
            }
        }

        private void LauncherMenu_Opened(object sender, RoutedEventArgs e)
        {
            StartupMenuItem.IsChecked = _controller.IsStartupEnabled();
        }

        private void AddAppMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _controller.ShowAddDialog();
        }

        private void StartupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = StartupMenuItem.IsChecked;
            StartupMenuItem.IsChecked = _controller.SetStartupEnabled(enabled);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _controller.ExitApplication();
        }
    }
}

