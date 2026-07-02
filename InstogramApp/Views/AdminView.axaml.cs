using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class AdminView : UserControl
{
    public AdminView() => InitializeComponent();
    AdminViewModel VM => (AdminViewModel)DataContext!;

    void OnBack(object? s, RoutedEventArgs e)          => VM.BackCommand.Execute(null);
    void OnClaimMaster(object? s, RoutedEventArgs e)   => VM.ClaimMasterCommand.Execute(null);
    void OnRefreshFlags(object? s, RoutedEventArgs e)  => VM.LoadFlagsCommand.Execute(null);
    void OnAddWord(object? s, RoutedEventArgs e)       => VM.AddWordCommand.Execute(null);
    void OnSearchUsers(object? s, RoutedEventArgs e)   => VM.SearchUsersCommand.Execute(null);
    void OnSearchPosts(object? s, RoutedEventArgs e)   => VM.SearchPostsCommand.Execute(null);
    void OnRefreshReports(object? s, RoutedEventArgs e)=> VM.LoadReportsCommand.Execute(null);

    void OnDismissFlag(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FlagRowViewModel row)
            row.DismissCommand.Execute(null);
    }

    void OnDeleteFlag(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FlagRowViewModel row)
            row.DeleteCommand.Execute(null);
    }

    void OnRemoveWord(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is BannedWordRowViewModel row)
            row.RemoveCommand.Execute(null);
    }

    void OnBanUser(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminUserRowViewModel row)
            row.BanCommand.Execute(null);
    }

    void OnUnbanUser(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminUserRowViewModel row)
            row.UnbanCommand.Execute(null);
    }

    void OnPromoteUser(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminUserRowViewModel row)
            row.PromoteCommand.Execute(null);
    }

    void OnDemoteUser(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminUserRowViewModel row)
            row.DemoteCommand.Execute(null);
    }

    void OnDeletePost(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminPostRowViewModel row)
            row.DeletePostCommand.Execute(null);
    }

    void OnToggleComments(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminPostRowViewModel row)
            row.ToggleCommentsCommand.Execute(null);
    }

    void OnDeleteComment(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is AdminCommentRowViewModel row)
            row.DeleteCommand.Execute(null);
    }
}
