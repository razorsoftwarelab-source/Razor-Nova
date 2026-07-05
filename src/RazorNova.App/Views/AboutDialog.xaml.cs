using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace RazorNova.App.Views;

/// <summary>
/// A simple, self-contained informational dialog — no ViewModel needed
/// (no business logic, nothing to test beyond "does it display the
/// right static facts"). VersionText is read directly from the running
/// assembly so the displayed version can never drift out of sync with
/// what was actually built.
/// </summary>
public partial class AboutDialog : Window
{
    public string VersionText { get; }

    public AboutDialog()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = $"Version {version?.ToString(3) ?? "1.0.0"}";

        InitializeComponent();
        DataContext = this;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Website_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Opens in the user's default browser rather than inside the app
        // — RazorNova has no embedded browser, and shouldn't need one
        // just for this single link.
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}