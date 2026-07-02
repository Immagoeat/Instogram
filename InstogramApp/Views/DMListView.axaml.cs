using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class DMListView : UserControl
{
    public DMListView() => InitializeComponent();

    DMListViewModel VM => (DMListViewModel)DataContext!;

    void OnNewMessage(object? s, RoutedEventArgs e)       => VM.NewMessageCommand.Execute(null);
    void OnOpenConversation(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is ConversationRowViewModel row)
            VM.OpenConversationCommand.Execute(row);
    }
}
