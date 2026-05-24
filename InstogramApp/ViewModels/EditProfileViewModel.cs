using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class EditProfileViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _displayName   = "";
    [ObservableProperty] private string _bio           = "";
    [ObservableProperty] private string _website       = "";
    [ObservableProperty] private string _email         = "";
    [ObservableProperty] private string _phone         = "";
    [ObservableProperty] private string _selectedColor = "";
    [ObservableProperty] private bool   _notifyDMs;
    [ObservableProperty] private bool   _notifyFollowedPosts;
    [ObservableProperty] private string _status        = "";
    [ObservableProperty] private string _errorMessage  = "";

    // Avatar picture
    [ObservableProperty] private string _avatarPath   = "";
    [ObservableProperty] private bool   _hasAvatar;
    [ObservableProperty] private bool   _hasNoAvatar  = true;

    partial void OnAvatarPathChanged(string value)
    {
        HasAvatar  = !string.IsNullOrEmpty(value);
        HasNoAvatar = !HasAvatar;
    }

    // 20 accent colours
    public List<string> AccentColors { get; } =
    [
        // Purples / violets
        "#8b5cf6", "#7c3aed", "#a855f7", "#6d28d9",
        // Pinks / reds
        "#ec4899", "#f43f5e", "#e11d48", "#be185d",
        // Blues / cyans
        "#06b6d4", "#3b82f6", "#0ea5e9", "#1d4ed8",
        // Greens / teals
        "#10b981", "#14b8a6", "#22c55e", "#16a34a",
        // Warm tones
        "#f59e0b", "#f97316", "#ef4444", "#64748b",
    ];

    public EditProfileViewModel(MainWindowViewModel main)
    {
        _main = main;
        var u = AppState.Instance.CurrentUser!;
        DisplayName         = u.DisplayName;
        Bio                 = u.Bio;
        Website             = u.Website;
        Email               = u.Email;
        Phone               = u.Phone;
        SelectedColor       = u.AccentColor;
        NotifyDMs           = u.NotifyDMs;
        NotifyFollowedPosts = u.NotifyFollowedPosts;
        AvatarPath          = u.AvatarPath;
    }

    [RelayCommand]
    async Task PickAvatar()
    {
        // Get the top-level window to show the dialog
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (topLevel == null) return;

        var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Choose profile picture",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp"],
                    MimeTypes = ["image/*"]
                }
            ]
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(dialog);
        if (files.Count > 0)
        {
            var sourcePath = files[0].Path.LocalPath;
            _main.Navigate(new CropViewModel(_main, sourcePath, croppedPath =>
            {
                AvatarPath = croppedPath;
            }));
        }
    }

    [RelayCommand]
    void ClearAvatar() => AvatarPath = "";

    [RelayCommand]
    async Task Save()
    {
        ErrorMessage = "";
        var dn = DisplayName.Trim();
        if (string.IsNullOrEmpty(dn)) { ErrorMessage = "Display name is required."; return; }

        if (AppState.Instance.IsServerMode)
        {
            // Upload avatar to server if it's a local file (not already a URL)
            var avatarUrl = AvatarPath;
            if (!string.IsNullOrEmpty(AvatarPath) && !AvatarPath.StartsWith("http"))
            {
                var uploaded = await ServerClient.Instance.UploadAvatarAsync(AvatarPath);
                if (uploaded != null)
                    avatarUrl = ServerClient.Instance.BaseUrl + uploaded;
            }

            var ok = await ServerClient.Instance.UpdateProfileAsync(
                dn, Bio.Trim(), Website.Trim(), Email.Trim(),
                Phone.Trim(), string.Empty, SelectedColor, NotifyDMs, NotifyFollowedPosts);

            if (!ok) { ErrorMessage = "Server save failed."; return; }

            AppState.Instance.ServerDisplay = dn;
            AppState.Instance.ServerAccent  = SelectedColor;
            AvatarPath = avatarUrl;
        }

        var u = AppState.Instance.CurrentUser!;
        u.DisplayName         = dn;
        u.Bio                 = Bio.Trim();
        u.Website             = Website.Trim();
        u.Email               = Email.Trim();
        u.Phone               = Phone.Trim();
        u.AccentColor         = SelectedColor;
        u.AvatarPath          = AvatarPath;
        u.NotifyDMs           = NotifyDMs;
        u.NotifyFollowedPosts = NotifyFollowedPosts;

        AppState.Instance.Save();
        _main.LoggedInDisplay = u.DisplayName;
        _main.RefreshSidebarAvatar();
        Status = "Profile saved!";
    }

    [RelayCommand]
    void Back() => _main.Navigate(new ProfileViewModel(_main, AppState.Instance.CurrentUser!));

    [RelayCommand]
    void SelectColor(string color) => SelectedColor = color;

    [RelayCommand]
    void GoThemes() => _main.Navigate(new ThemePickerViewModel(_main));
}
