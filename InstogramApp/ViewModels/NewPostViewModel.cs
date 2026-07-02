using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class TagChipViewModel(string tag, NewPostViewModel parent) : ViewModelBase
{
    public string Tag { get; } = tag;
    [ObservableProperty] private bool   _isSelected;
    [ObservableProperty] private string _chipBackground = "#1a1a2e";
    [ObservableProperty] private string _chipForeground = "#7c6aab";

    partial void OnIsSelectedChanged(bool value)
    {
        ChipBackground = value ? "#3b1fa8" : "#1a1a2e";
        ChipForeground = value ? "#e0d0ff" : "#7c6aab";
    }

    [RelayCommand]
    void Toggle()
    {
        IsSelected = !IsSelected;
        parent.OnTagToggled(Tag, IsSelected);
    }
}

public partial class NewPostViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _caption   = "";
    [ObservableProperty] private string _status    = "";
    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _imagePath = "";
    [ObservableProperty] private bool   _hasImage;
    [ObservableProperty] private string _videoPath = "";
    [ObservableProperty] private bool   _hasVideo;
    partial void OnVideoPathChanged(string value) { HasVideo = !string.IsNullOrEmpty(value); OnPropertyChanged(nameof(CanPickMedia)); }

    public ObservableCollection<TagChipViewModel> PremadeTags   { get; } = new();
    public ObservableCollection<TagChipViewModel> SuggestedTags { get; } = new();
    [ObservableProperty] private bool _hasSuggestedTags;

    private static readonly string[] _premade =
    [
        "photography", "art", "nature", "travel", "food",
        "fitness", "music", "gaming", "fashion", "tech",
        "animals", "sports", "selfie", "sunset", "coding",
        "design", "books", "film", "coffee", "architecture"
    ];

    public NewPostViewModel(MainWindowViewModel main)
    {
        _main = main;
        foreach (var t in _premade)
            PremadeTags.Add(new TagChipViewModel(t, this));
    }

    partial void OnCaptionChanged(string value) => RefreshSuggestedTags(value);

    private void RefreshSuggestedTags(string text)
    {
        var typed = Regex.Matches(text, @"#(\w+)")
            .Select(m => m.Groups[1].Value.ToLower())
            .Distinct()
            .ToHashSet();

        SuggestedTags.Clear();
        foreach (var tag in typed)
        {
            var vm = new TagChipViewModel(tag, this);
            vm.IsSelected = true;
            SuggestedTags.Add(vm);
        }
        HasSuggestedTags = SuggestedTags.Count > 0;

        foreach (var chip in PremadeTags)
            chip.IsSelected = typed.Contains(chip.Tag);
    }

    public void OnTagToggled(string tag, bool selected)
    {
        if (selected)
        {
            if (!Caption.Contains($"#{tag}"))
                Caption = Caption.TrimEnd() + (Caption.Length > 0 ? " " : "") + $"#{tag}";
        }
        else
        {
            Caption = Regex.Replace(Caption, $@"\s?#{Regex.Escape(tag)}\b", "").Trim();
        }
    }

    partial void OnImagePathChanged(string value) { HasImage = !string.IsNullOrEmpty(value); OnPropertyChanged(nameof(CanPickMedia)); }
    public bool CanPickMedia => !HasImage && !HasVideo;

    [RelayCommand]
    async Task PickImage()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Images")
                    {
                        Patterns  = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"],
                        MimeTypes = ["image/*"]
                    }
                ]
            });
        if (files.Count > 0)
            ImagePath = files[0].Path.LocalPath;
    }

    [RelayCommand] void RemoveImage() => ImagePath = "";

    [RelayCommand]
    async Task PickVideo()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow : null;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose video",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Videos")
                    {
                        Patterns  = ["*.mp4", "*.webm", "*.mov", "*.avi", "*.mkv"],
                        MimeTypes = ["video/*"]
                    }
                ]
            });
        if (files.Count > 0)
        {
            VideoPath = files[0].Path.LocalPath;
            ImagePath = ""; // clear image if video chosen
        }
    }

    [RelayCommand] void RemoveVideo() => VideoPath = "";

    [RelayCommand]
    async Task Submit()
    {
        var text = Caption.Trim();
        if (string.IsNullOrEmpty(text) && !HasImage && !HasVideo)
        { Status = "Add a caption, image, or video first."; return; }

        var tags = string.Join(",", Regex.Matches(text, @"#(\w+)")
            .Select(m => m.Groups[1].Value.ToLower())
            .Distinct());

        IsBusy = true;
        Status = "";
        try
        {
            var post = await ServerClient.Instance.CreatePostAsync(text, tags);
            if (post == null) { Status = "Failed to post — try again."; return; }

            if (HasImage)
            {
                Status = "Uploading image…";
                await ServerClient.Instance.UploadPostImageAsync(post.Id, ImagePath);
            }
            else if (HasVideo)
            {
                Status = "Uploading video…";
                await ServerClient.Instance.UploadPostVideoAsync(post.Id, VideoPath);
            }

            Caption   = "";
            ImagePath = "";
            VideoPath = "";
            _main.Navigate(new FeedViewModel(_main));
        }
        catch { Status = "Could not reach server."; }
        finally { IsBusy = false; }
    }

    [RelayCommand] void Cancel() => _main.Navigate(new FeedViewModel(_main));
}
