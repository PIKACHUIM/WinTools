using System.Windows.Controls;

namespace WinTools.App.Controls;

public partial class Spinner : UserControl
{
    public Spinner()
    {
        InitializeComponent();
        SetResourceReference(ForegroundProperty, "App.Accent");
    }
}
