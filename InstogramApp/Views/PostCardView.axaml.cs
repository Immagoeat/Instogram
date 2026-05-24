using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class PostCardView : UserControl
{
    public PostCardView() => InitializeComponent();
    PostCardViewModel VM => (PostCardViewModel)DataContext!;
    void OnViewProfile(object? s, RoutedEventArgs e) => VM.ViewAuthorProfileCommand.Execute(null);
    void OnLike(object? s, RoutedEventArgs e)        => VM.ToggleLikeCommand.Execute(null);
    void OnAddComment(object? s, RoutedEventArgs e)  => VM.AddCommentCommand.Execute(null);
}
