using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AnkiHelper
{
    public partial class MainWindow : Window
    {
        // DWM attributes used to strip Windows 11 styling from the hand-drawn Win98 frame.
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwaBorderColor = 34;
        private const int DwmwcpDoNotRound = 1;
        private const int DwmwaColorNone = unchecked((int)0xFFFFFFFE);

        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Square corners and no accent border. Both calls simply fail (ignored) before Windows 11.
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int doNotRound = DwmwcpDoNotRound;
            int noBorder = DwmwaColorNone;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref doNotRound, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref noBorder, sizeof(int));
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.OnLoad();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => SystemCommands.CloseWindow(this);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
