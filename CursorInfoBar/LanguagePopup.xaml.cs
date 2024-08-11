using System.Windows;

namespace CursorInfoBar;

public partial class LanguagePopup : Window
{
    public LanguagePopup()
    {
        InitializeComponent();
    }
    
    public LanguagePopup(string language)
    {
        InitializeComponent();
        LanguageLabel.Text = language;
        PositionPopup();
    }

    private void PositionPopup()
    {
        var mousePos = System.Windows.Forms.Cursor.Position;
        Left = mousePos.X + 10; // Трохи зміщуємо від позиції миші
        Top = mousePos.Y + 10;
    }
}