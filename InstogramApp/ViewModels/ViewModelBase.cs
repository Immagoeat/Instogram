using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InstogramApp.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected static string FormatAge(DateTime utc)
    {
        var age = DateTime.UtcNow - utc;
        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours   < 1) return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalDays    < 1) return $"{(int)age.TotalHours}h ago";
        if (age.TotalDays    < 7) return $"{(int)age.TotalDays}d ago";
        return utc.ToLocalTime().ToString("MMM d");
    }

    protected static string Initial(string name) =>
        name.Length > 0 ? name[0].ToString().ToUpper() : "?";

    protected static string MakeLikeLabel(bool liked, int count) =>
        liked ? $"♥ {count}" : $"♡ {count}";
}
