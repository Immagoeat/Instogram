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
    [ObservableProperty] private bool _showRegister;
    public bool ShowLogin => !ShowRegister;
    [ObservableProperty] private string _formTitle = "Welcome back";
    [ObservableProperty] private bool   _isBusy;

    // ── Register-only ─────────────────────────────────────────────────────
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _email       = "";

    // ── CAPTCHA ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _captchaQuestion = "";
    [ObservableProperty] private string _captchaAnswer   = "";
    private int _captchaExpected;

    // ── Server URL ────────────────────────────────────────────────────────
    [ObservableProperty] private string _serverUrl           = "";
    [ObservableProperty] private bool   _showServerUrl       = true;
    [ObservableProperty] private string _serverUrlToggleLabel = "▲ Server URL";
    [ObservableProperty] private string _serverStatus        = "";

    public AuthViewModel(MainWindowViewModel main)
    {
        _main = main;
        GenerateCaptcha();

        // Pre-fill from saved config, fall back to localhost
        var cfg = ServerConfig.Load();
        ServerUrl = cfg?.ServerUrl ?? "http://localhost:5000";
        ServerStatus = $"Connecting to: {ServerUrl}";
    }

    private string EffectiveUrl => string.IsNullOrWhiteSpace(ServerUrl)
        ? "http://localhost:5000"
        : ServerUrl.Trim().TrimEnd('/');

    partial void OnServerUrlChanged(string value)
    {
        var url = string.IsNullOrWhiteSpace(value) ? "http://localhost:5000" : value.Trim();
        ServerStatus = $"Will connect to: {url}";
    }

    private void GenerateCaptcha()
    {
        var rng  = new Random();
        int kind = rng.Next(0, 6);

        switch (kind)
        {
            case 0: // addition with larger numbers
            {
                int a = rng.Next(12, 60), b = rng.Next(12, 60);
                _captchaExpected = a + b;
                CaptchaQuestion  = $"What is {a} + {b}?";
                break;
            }
            case 1: // subtraction (always positive)
            {
                int a = rng.Next(30, 90), b = rng.Next(5, a);
                _captchaExpected = a - b;
                CaptchaQuestion  = $"What is {a} − {b}?";
                break;
            }
            case 2: // multiplication
            {
                int a = rng.Next(4, 13), b = rng.Next(4, 13);
                _captchaExpected = a * b;
                CaptchaQuestion  = $"What is {a} × {b}?";
                break;
            }
            case 3: // word-number addition
            {
                string[] words = { "zero","one","two","three","four","five","six","seven","eight","nine","ten",
                                   "eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen","twenty" };
                int a = rng.Next(1, 10), b = rng.Next(1, 10);
                _captchaExpected = a + b;
                CaptchaQuestion  = $"Add {words[a]} and {words[b]} — enter the number:";
                break;
            }
            case 4: // missing number in sequence
            {
                int start = rng.Next(2, 20), step = rng.Next(2, 8);
                int pos   = rng.Next(1, 4);          // which term is missing (1-indexed)
                int[] seq = new int[5];
                for (int i = 0; i < 5; i++) seq[i] = start + i * step;
                _captchaExpected = seq[pos];
                var display = new string[5];
                for (int i = 0; i < 5; i++) display[i] = i == pos ? "?" : seq[i].ToString();
                CaptchaQuestion = $"Complete the sequence: {string.Join(", ", display)}";
                break;
            }
            default: // remainder / division check
            {
                int b  = rng.Next(2, 9);
                int q  = rng.Next(3, 12);
                int a  = b * q;
                _captchaExpected = q;
                CaptchaQuestion  = $"How many times does {b} go into {a}?";
                break;
            }
        }

        CaptchaAnswer = "";
    }

    [RelayCommand]
    void ToggleServerUrl()
    {
        ShowServerUrl        = !ShowServerUrl;
        ServerUrlToggleLabel = ShowServerUrl ? "▲ Server URL" : "▼ Server URL";
        if (!ShowServerUrl) ServerStatus = "";
        else OnServerUrlChanged(ServerUrl);
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
            ServerClient.Instance.Configure(EffectiveUrl, "");
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
            ServerClient.Instance.Configure(EffectiveUrl, "");
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
        var url = EffectiveUrl;
        ServerClient.Instance.Configure(url, token);
        await ServerClient.Instance.ConnectHubAsync();

        AppState.Instance.IsServerMode     = true;
        AppState.Instance.ServerUserId     = user.Id.ToString();
        AppState.Instance.ServerUsername   = user.Username;
        AppState.Instance.ServerDisplay    = user.DisplayName;
        AppState.Instance.ServerAccent     = user.AccentColor;
        AppState.Instance.ServerIsVerified = user.IsVerified;
        AppState.Instance.ServerIsMaster   = user.IsMaster;

        ServerConfig.Save(new ServerSettings(url, token,
            user.Username, user.DisplayName, user.AccentColor));

        _main.OnServerLogin(user.Username, user.DisplayName);
    }

    [RelayCommand]
    void ToggleRegister()
    {
        ShowRegister = !ShowRegister;
        OnPropertyChanged(nameof(ShowLogin));
        FormTitle    = ShowRegister ? "Create your account" : "Welcome back";
        ErrorMessage = "";
        GenerateCaptcha();
    }
}
