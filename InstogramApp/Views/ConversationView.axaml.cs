using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class ConversationView : UserControl
{
    public ConversationView() => InitializeComponent();

    ConversationViewModel VM => (ConversationViewModel)DataContext!;

    void OnBack(object? s, RoutedEventArgs e)        => VM.BackCommand.Execute(null);
    void OnViewProfile(object? s, RoutedEventArgs e) => VM.ViewProfileCommand.Execute(null);
    void OnSend(object? s, RoutedEventArgs e)        => VM.SendCommand.Execute(null);
    void OnCall(object? s, RoutedEventArgs e)        => VM.StartCallCommand.Execute(null);
}
