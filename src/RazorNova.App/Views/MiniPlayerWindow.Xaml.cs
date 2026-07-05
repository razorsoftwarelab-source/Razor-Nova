using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RazorNova.App.ViewModels;

namespace RazorNova.App.Views;

public partial class MiniPlayerWindow : Window
{
    public event EventHandler? RestoreRequested;

    public MiniPlayerWindow()
    {
        InitializeComponent();
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e) =>
        RestoreRequested?.Invoke(this, EventArgs.Empty);

    private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider && DataContext is PlayerControlsViewModel viewModel)
            viewModel.SeekCommand.Execute(slider.Value);
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Slider slider || DataContext is not PlayerControlsViewModel viewModel)
            return;

        if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb)
            return;

        if (slider.Template.FindName("PART_Track", slider) is not System.Windows.Controls.Primitives.Track track)
            return;

        double mouseX = e.GetPosition(track).X;
        double trackWidth = track.ActualWidth;
        if (trackWidth <= 0) return;

        double newValue = (mouseX / trackWidth) * (slider.Maximum - slider.Minimum) + slider.Minimum;
        viewModel.SeekCommand.Execute(newValue);
    }
}