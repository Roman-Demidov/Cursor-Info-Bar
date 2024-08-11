using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Control = System.Windows.Forms.Control;
using Cursors = System.Windows.Input.Cursors;
using TextBox = System.Windows.Controls.TextBox;

namespace CursorInfoBar;

public partial class MainWindow : Window
{
    private const int CELL_WIDTH = 24;
    private const int CELL_HEIGHT = 18;

    private List<MyGridItem> _gridItems;
    
    public MainWindow()
    {
        InitializeComponent();

        //this.Topmost = true;
        
        //CreatePopupWindow();
        
        //InputLanguageManager.Current.InputLanguageChanged += OnInputLanguageChanged;

        //UpdateActiveLanguageView();
        
        
        
        SetupNotifyIcon();
        Loaded += MainWindow_Loaded;
    }

    private NotifyIcon _notifyIcon;
    
    private void SetupNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            Text = "Language Switcher"
        };

        _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    
    
    private const int HOTKEY_ALT_ID = 9000;
    private const int HOTKEY_SHIFT_ID = 9001;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);


    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Hide(); // Приховуємо головне вікно одразу після запуску
        
        IntPtr handle = new WindowInteropHelper(this).Handle;
        HwndSource source = HwndSource.FromHwnd(handle);
        source.AddHook(HwndHook);

        // Реєстрація глобальних гарячих клавіш Alt+Shift для зміни мови
        RegisterHotKey(handle, HOTKEY_ALT_ID, MOD_ALT | MOD_SHIFT, VK_SHIFT); // MOD_ALT | MOD_SHIFT, VK_MENU
        //RegisterHotKey(handle, HOTKEY_SHIFT_ID, 0x0001 | 0x0004, 0x12); // MOD_ALT | MOD_SHIFT, VK_MENU
    }

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_KEYDOWN = 0;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_MENU = 0x10;
    private const uint VK_SHIFT = 0x12;
    
    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_INPUTLANGCHANGE = 0x0051;
        const int WM_HOTKEY = 0x0312;
        handled = false;
        
        if (msg == WM_INPUTLANGCHANGE)
        {
            ShowLanguagePopup();
            return IntPtr.Zero;
        }
        Console.WriteLine("msg = " + msg);
        
        if (msg == 0x0105 && (wParam.ToInt32() == HOTKEY_ALT_ID))
        {
            
            ShowLanguagePopup();
            handled = false;
            
            keybd_event(0x12, 0x38, KEYEVENTF_KEYDOWN, 0);
            keybd_event(0x10, 0x2A, KEYEVENTF_KEYDOWN, 0);
            
            keybd_event(0x12, 0x38, KEYEVENTF_KEYUP, 0);
            keybd_event(0x10, 0x2A, KEYEVENTF_KEYUP, 0);
            Console.Write(" 1_1 ");
        }

        return IntPtr.Zero;
    }
    
    private async void ShowLanguagePopup()
    {
        string language = InputLanguage.CurrentInputLanguage.Culture.TwoLetterISOLanguageName.ToUpper();
        var popup = new LanguagePopup(language);
        popup.Show();
        await Task.Delay(1000); // Показуємо на 1 секунду
        popup.Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HOTKEY_ALT_ID);
        _notifyIcon.Dispose();
        base.OnClosed(e);
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
        this.Left = point.X - this.Width / 2;
        this.Top = point.Y + 10;
    }

    private static Vector2 GetMousePosition()
    {
        Vector2 point = new Vector2(Control.MousePosition.X, Control.MousePosition.Y);
        return point;
    }
}