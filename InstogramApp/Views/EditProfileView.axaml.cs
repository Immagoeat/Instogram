using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class EditProfileView : UserControl
{
    public EditProfileView() => InitializeComponent();

    EditProfileViewModel VM => (EditProfileViewModel)DataContext!;

    void OnBack(object? s, RoutedEventArgs e)        => VM.BackCommand.Execute(null);
    void OnSave(object? s, RoutedEventArgs e)        => VM.SaveCommand.Execute(null);
    void OnGoThemes(object? s, RoutedEventArgs e)    => VM.GoThemesCommand.Execute(null);
    void OnPickAvatar(object? s, RoutedEventArgs e)  => VM.PickAvatarCommand.Execute(null);
    void OnClearAvatar(object? s, RoutedEventArgs e) => VM.ClearAvatarCommand.Execute(null);

    void OnSelectColor(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string color)
            VM.SelectColorCommand.Execute(color);
    }
}
