using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    // ── Shared ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _username     = "";
    [ObservableProperty] private string _password     = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool   _showRegister;
    [ObservableProperty] private bool   _showLogin = true;
    [ObservableProperty] private string _formTitle = "Welcome back";
    [ObservableProperty] private bool   _isBusy;

    // ── Register-only ─────────────────────────────────────────────────────
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _email       = "";

    // ── CAPTCHA ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _captchaQuestion = "";
    [ObservableProperty] private string _captchaAnswer   = "";
    private int _captchaExpected;

    // Hardcoded server — change this when deployed
    private const string ServerUrl = "http://localhost:5000";

    public AuthViewModel(MainWindowViewModel main)
    {
        _main = main;
        GenerateCaptcha();
    }

    private void GenerateCaptcha()
    {
        var rng = new Random();
        int a = rng.Next(1, 15);
        int b = rng.Next(1, 15);
        _captchaExpected = a + b;
        CaptchaQuestion  = $"What is {a} + {b}?";
        CaptchaAnswer    = "";
    }

    [RelayCommand]
    async Task Login()
    {
        ErrorMessage = "";
        var u = Username.Trim();
        if (string.IsNullOrEmpty(u))        { ErrorMessage = "Username is required."; return; }
        if (string.IsNullOrEmpty(Password)) { ErrorMessage = "Password is required."; return; }

        IsBusy = true;
        try
        {
            ServerClient.Instance.Configure(ServerUrl, "");
            var (user, token) = await ServerClient.Instance.LoginAsync(u, Password);
            if (user == null) { ErrorMessage = "Invalid username or password."; return; }

            await FinishLogin(user, token);
        }
        catch (Exception ex) { ErrorMessage = $"Could not reach server: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task Register()
    {
        ErrorMessage = "";
        var u  = Username.Trim();
        var dn = DisplayName.Trim();
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(dn))
        { ErrorMessage = "Username and display name are required."; return; }
        if (string.IsNullOrEmpty(Password))
        { ErrorMessage = "Password is required."; return; }
        if (Password.Length < 6)
        { ErrorMessage = "Password must be at least 6 characters."; return; }
        if (!int.TryParse(CaptchaAnswer.Trim(), out int ans) || ans != _captchaExpected)
        { ErrorMessage = "Incorrect answer — try again."; GenerateCaptcha(); return; }

        IsBusy = true;
        try
        {
            ServerClient.Instance.Configure(ServerUrl, "");
            var (user, token) = await ServerClient.Instance.RegisterAsync(
                u, dn, Password, email: Email.Trim());
            if (user == null) { ErrorMessage = "Registration failed — username may already be taken."; return; }

            await FinishLogin(user, token);
        }
        catch (Exception ex) { ErrorMessage = $"Could not reach server: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task FinishLogin(UserDto user, string token)
    {
        ServerClient.Instance.Configure(ServerUrl, token);
        await ServerClient.Instance.ConnectHubAsync();

        AppState.Instance.IsServerMode  = true;
        AppState.Instance.ServerUserId  = user.Id.ToString();
        AppState.Instance.ServerUsername = user.Username;
        AppState.Instance.ServerDisplay  = user.DisplayName;
        AppState.Instance.ServerAccent   = user.AccentColor;

        ServerConfig.Save(new ServerSettings(ServerUrl, token,
            user.Username, user.DisplayName, user.AccentColor));

        _main.OnServerLogin(user.Username, user.DisplayName);
    }

    [RelayCommand]
    void ToggleRegister()
    {
        ShowRegister = !ShowRegister;
        ShowLogin    = !ShowRegister;
        FormTitle    = ShowRegister ? "Create your account" : "Welcome back";
        ErrorMessage = "";
        GenerateCaptcha();
    }
}
