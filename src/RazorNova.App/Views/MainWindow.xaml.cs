using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;
using RazorNova.App.ViewModels;
using RazorNova.App.Views;
using RazorNova.Core.Enums;
using TrackModel = RazorNova.Core.Models.Track;

namespace RazorNova.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        CommandBindings.Add(new CommandBinding(
            SystemCommands.MinimizeWindowCommand,
            (_, _) => SystemCommands.MinimizeWindow(this)));

        CommandBindings.Add(new CommandBinding(
            SystemCommands.CloseWindowCommand,
            (_, _) => SystemCommands.CloseWindow(this)));
    }

    private void ToggleMaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void AboutButton_Click(object sender, RoutedEventArgs e) =>
        new AboutDialog { Owner = this }.ShowDialog();

    private void CycleTheme_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.SelectedTheme = vm.SelectedTheme switch
        {
            ThemeMode.Night => ThemeMode.Day,
            ThemeMode.Day => ThemeMode.System,
            _ => ThemeMode.Night
        };
    }

    private void LibraryTrackListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: TrackModel track } && DataContext is MainViewModel viewModel)
            viewModel.Library.PlayTrackCommand.Execute(track);
    }

    private void PlaylistTrackListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: TrackModel track } && DataContext is MainViewModel viewModel)
            viewModel.Playlist.PlayTrackInPlaylistCommand.Execute(track);
    }

    private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider && DataContext is MainViewModel viewModel)
            viewModel.PlayerControls.SeekCommand.Execute(slider.Value);
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Slider slider || DataContext is not MainViewModel viewModel)
            return;

        if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb)
            return;

        if (slider.Template.FindName("PART_Track", slider) is not System.Windows.Controls.Primitives.Track track)
            return;

        double mouseX = e.GetPosition(track).X;
        double trackWidth = track.ActualWidth;
        if (trackWidth <= 0) return;

        double newValue = (mouseX / trackWidth) * (slider.Maximum - slider.Minimum) + slider.Minimum;
        viewModel.PlayerControls.SeekCommand.Execute(newValue);
    }
}