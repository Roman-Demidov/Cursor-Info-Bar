using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Control = System.Windows.Forms.Control;
using Cursors = System.Windows.Input.Cursors;
using TextBox = System.Windows.Controls.TextBox;

namespace CursorInfoBar;

public partial class MainWindow : Window
{
    private const int CELL_WIDTH = 24;
    private const int CELL_HEIGHT = 18;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP  = 0x0105;

    private static IntPtr _hookId = IntPtr.Zero;
    
    private readonly DispatcherTimer _hideTimer;

    private List<MyGridItem> _gridItems;
    private LowLevelKeyboardProc _proc;
    private int _currentLanguageId;

    private NotifyIcon _notifyIcon;
    
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    
    public MainWindow()
    {
        InitializeComponent();

        this.ShowInTaskbar = false;
        Topmost = true;
        CreatePopupWindow();
        InitializeNotifyIcon();
        UpdateActiveLanguageView();
        
        _hideTimer = new DispatcherTimer();
        _hideTimer.Interval = TimeSpan.FromSeconds(2);
        _hideTimer.Tick += HideTimer_Tick;
        
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new NotifyIcon();
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.Visible = true;
        _notifyIcon.Text = "Language Bar";

        // Створюємо контекстне меню для іконки в треї
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("Exit", null, OnExitClick);
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void OnExitClick(object sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        _notifyIcon.Dispose();
    }
    
    
    
    
    
    
    
    
    
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _currentLanguageId = GetKeyboardLayout(GetCurrentThreadId()).ToInt32()  & 0xFFFF;
        _proc = HookCallback;
        _hookId = SetHook(_proc);
            
        this.Hide();
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        UnhookWindowsHookEx(_hookId);
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            int languageId = GetKeyboardLayout(foregroundThreadId).ToInt32() & 0xFFFF;

            if (_currentLanguageId != languageId)
            {
                _currentLanguageId = languageId;
                    
                Application.Current.Dispatcher.Invoke(ShowPopupWindow);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void ShowPopupWindow()
    {
        this.Show();
        UpdateActiveLanguageView();
        UpdateWindowPos();
            
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void HideTimer_Tick(object sender, EventArgs e)
    {
        this.Hide();
        _hideTimer.Stop();
    }

    private void CreatePopupWindow()
    {
        List<string> inputLanguages = InputLanguageManager.Current.AvailableInputLanguages
            .Cast<CultureInfo>()
            .Select(culture => culture.TwoLetterISOLanguageName)
            .ToList();

        this.Width = CELL_WIDTH * inputLanguages.Count;
        this.Height = CELL_HEIGHT;

        _gridItems = new List<MyGridItem>();
        
        for (var i = 0; i < inputLanguages.Count; i++)
        {
            Border border = CreateBorder();
            TextBox textBox = CreateTextBox(inputLanguages[i]);

            if (i == 0)
            {
                border.CornerRadius = new CornerRadius()
                {
                    TopLeft = 5,
                    BottomLeft = 5,
                    BottomRight = border.CornerRadius.BottomRight,
                    TopRight = border.CornerRadius.TopRight
                };
            }
            
            if (i == inputLanguages.Count - 1)
            {
                border.CornerRadius = new CornerRadius()
                {
                    TopLeft = border.CornerRadius.TopLeft,
                    BottomLeft = border.CornerRadius.BottomLeft,
                    BottomRight = 5,
                    TopRight = 5
                };
            }
            
            MyGridItem gridItem = new MyGridItem()
            {
                id = inputLanguages[i].GetHashCode(),
                border = border,
                textBox = textBox
            };
            
            border.Child = textBox;
            
            MainGrid.Children.Add(border);
            
            _gridItems.Add(gridItem);

            MainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(border, i);
        }
    }

    private void UpdateActiveLanguageView()
    {
        int languageId = InputLanguageManager.Current.CurrentInputLanguage.TwoLetterISOLanguageName.GetHashCode();
        foreach (MyGridItem item in _gridItems)
        {
            item.border.Background = item.id == languageId ? (SolidColorBrush)new BrushConverter().ConvertFromString("#017AFF") : Brushes.White;
            item.textBox.Foreground = item.id == languageId ? Brushes.White : Brushes.Black;
        }
        
        _currentLanguageId = languageId;
    }

    private static Border CreateBorder()
    {
        Border border = new Border();
        border.Background = Brushes.White;
        border.BorderBrush = Brushes.White;
        return border;
    }

    private static TextBox CreateTextBox(string text)
    {
        TextBox textBox= new TextBox();
        textBox.Text = text;
        textBox.Background = new SolidColorBrush(Colors.Transparent);
        textBox.BorderBrush = new SolidColorBrush(Colors.Transparent);
        textBox.Foreground = new SolidColorBrush(Colors.Wheat);
        textBox.FontSize = 14;
        textBox.TextAlignment = TextAlignment.Center;
        textBox.Padding = new Thickness(0, -3, 0, 0);
        textBox.IsEnabled = true;
        textBox.IsReadOnly = true;
        textBox.Focusable = false;
        textBox.FontWeight = FontWeights.Bold;
        textBox.Cursor = Cursors.Arrow;
        return textBox;
    }

    private void OnInputLanguageChanged(object sender, InputLanguageEventArgs e)
    {
        UpdateWindowPos();
        UpdateActiveLanguageView();
    }

    private void UpdateWindowPos()
    {
        Vector2 point = GetMousePosition();
        Left = point.X - Width / 2;
        Top = point.Y + 10;
    }

    private static Vector2 GetMousePosition()
    {
        Vector2 point = new Vector2(Control.MousePosition.X, Control.MousePosition.Y);
        return point;
    }
}