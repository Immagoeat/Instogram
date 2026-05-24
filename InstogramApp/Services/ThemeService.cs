using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace InstogramApp.Services;

public static class ThemeService
{
    public enum Theme { OledBlack, Dark, Light, PurpleNight, OceanBlue, Sunset }

    private static readonly Dictionary<Theme, string> _uris = new()
    {
        [Theme.OledBlack]  = "avares://InstogramApp/Themes/OledBlack.axaml",
        [Theme.Dark]       = "avares://InstogramApp/Themes/Dark.axaml",
        [Theme.Light]      = "avares://InstogramApp/Themes/Light.axaml",
        [Theme.PurpleNight]= "avares://InstogramApp/Themes/PurpleNight.axaml",
        [Theme.OceanBlue]  = "avares://InstogramApp/Themes/OceanBlue.axaml",
        [Theme.Sunset]     = "avares://InstogramApp/Themes/Sunset.axaml",
    };

    public static Theme Current { get; private set; } = Theme.Dark;

    private static Avalonia.Controls.ResourceDictionary? _currentDict;

    public static void Apply(Theme theme)
    {
        var app = Application.Current ?? throw new InvalidOperationException("No Application");
        var uri = new Uri(_uris[theme]);
        var dict = (Avalonia.Controls.ResourceDictionary)AvaloniaXamlLoader.Load(uri);

        var merged = app.Resources.MergedDictionaries;
        if (_currentDict != null) merged.Remove(_currentDict);
        merged.Add(dict);
        _currentDict = dict;
        Current = theme;
    }

    public static IEnumerable<(Theme theme, string label)> All() =>
    [
        (Theme.OledBlack,   "OLED Black"),
        (Theme.Dark,        "Dark"),
        (Theme.Light,       "Light"),
        (Theme.PurpleNight, "Purple Night"),
        (Theme.OceanBlue,   "Ocean Blue"),
        (Theme.Sunset,      "Sunset"),
    ];
}
