using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class ThemeOptionViewModel : ViewModelBase
{
    private readonly ThemeService.Theme _theme;
    public ThemeOptionViewModel(ThemeService.Theme theme, string label)
    {
        _theme = theme;
        Theme  = theme;
        Label  = label;
    }
    public ThemeService.Theme Theme { get; }
    public string Label { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isNotSelected = true;

    partial void OnIsSelectedChanged(bool value) => IsNotSelected = !value;

    public string PreviewBg => _theme switch
    {
        ThemeService.Theme.OledBlack   => "#000000",
        ThemeService.Theme.Dark        => "#1a1a1a",
        ThemeService.Theme.Light       => "#ffffff",
        ThemeService.Theme.PurpleNight => "#0a0414",
        ThemeService.Theme.OceanBlue   => "#040e1a",
        ThemeService.Theme.Sunset      => "#140a0a",
        _                              => "#1a1a1a"
    };
    public string PreviewText => _theme == ThemeService.Theme.Light ? "#111111" : "#f0f0f0";
    public string PreviewAccent => _theme switch
    {
        ThemeService.Theme.Light       => "#7c3aed",
        ThemeService.Theme.PurpleNight => "#a855f7",
        ThemeService.Theme.OceanBlue   => "#38bdf8",
        ThemeService.Theme.Sunset      => "#f97316",
        _                              => "#8b5cf6"
    };
    public string PreviewAccent2 => _theme switch
    {
        ThemeService.Theme.OceanBlue => "#0ea5e9",
        ThemeService.Theme.Sunset    => "#ec4899",
        _                            => "#ec4899"
    };
    public string PreviewBorderColor => IsSelected ? PreviewAccent : "#2a2a2a";
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
