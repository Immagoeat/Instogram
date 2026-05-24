using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class ExploreViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public ObservableCollection<PostCardViewModel> Posts { get; } = new();

    public ExploreViewModel(MainWindowViewModel main)
    {
        _main = main;
        Load();
    }

    void Load()
    {
        Posts.Clear();
        foreach (var p in AppState.Instance.Feed.GetExploreFeed())
            Posts.Add(new PostCardViewModel(p, _main));
    }

    [RelayCommand] void Refresh() => Load();
}
