using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class PostCardView : UserControl
{
    public PostCardView() => InitializeComponent();
    ServerPostCardViewModel VM => (ServerPostCardViewModel)DataContext!;
    void OnLike(object? s, RoutedEventArgs e)         => VM.ToggleLikeCommand.Execute(null);
    void OnAddComment(object? s, RoutedEventArgs e)   => VM.AddCommentCommand.Execute(null);
    void OnOpenAuthor(object? s, RoutedEventArgs e)   => VM.OpenAuthorProfileCommand.Execute(null);
}
