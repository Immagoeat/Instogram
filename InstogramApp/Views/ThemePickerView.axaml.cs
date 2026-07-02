using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class ThemePickerView : UserControl
{
    public ThemePickerView() => InitializeComponent();
    ThemePickerViewModel VM => (ThemePickerViewModel)DataContext!;

    void OnBack(object? sender, RoutedEventArgs e) => VM.BackCommand.Execute(null);

    void OnSelectTheme(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ThemeOptionViewModel opt)
            VM.SelectThemeCommand.Execute(opt);
    }
}
