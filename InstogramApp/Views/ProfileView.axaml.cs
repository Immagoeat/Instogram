using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class ProfileView : UserControl
{
    public ProfileView() => InitializeComponent();
    ProfileViewModel VM => (ProfileViewModel)DataContext!;
    void OnToggleFollow(object? s, RoutedEventArgs e)  => VM.ToggleFollowCommand.Execute(null);
    void OnMessage(object? s, RoutedEventArgs e)       => VM.MessageCommand.Execute(null);
    void OnEditProfile(object? s, RoutedEventArgs e)   => VM.EditProfileCommand.Execute(null);
}
