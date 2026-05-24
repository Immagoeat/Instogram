using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class ThemeOptionViewModel(ThemeService.Theme theme, string label) : ViewModelBase
{
    public ThemeService.Theme Theme { get; } = theme;
    public string Label { get; } = label;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isNotSelected = true;

    partial void OnIsSelectedChanged(bool value) => IsNotSelected = !value;
}

public partial class ThemePickerViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<ThemeOptionViewModel> Themes { get; } = [];

    public ThemePickerViewModel(MainWindowViewModel main)
    {
        _main = main;
        foreach (var (t, label) in ThemeService.All())
            Themes.Add(new ThemeOptionViewModel(t, label) { IsSelected = t == ThemeService.Current });
    }

    [RelayCommand]
    void SelectTheme(ThemeOptionViewModel opt)
    {
        ThemeService.Apply(opt.Theme);
        foreach (var t in Themes) t.IsSelected = t == opt;
    }

    [RelayCommand]
    void Back() => _main.Navigate(new EditProfileViewModel(_main));
}
