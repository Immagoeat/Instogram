using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;
using InstogramDependencies;

namespace InstogramApp.Views;

public partial class DMUserSearchView : UserControl
{
    public DMUserSearchView() => InitializeComponent();

    DMUserSearchViewModel VM => (DMUserSearchViewModel)DataContext!;

    void OnBack(object? s, RoutedEventArgs e)   => VM.BackCommand.Execute(null);
    void OnSearch(object? s, RoutedEventArgs e) => VM.SearchCommand.Execute(null);
    void OnStartConversation(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is User user)
            VM.StartConversationCommand.Execute(user);
    }
}
