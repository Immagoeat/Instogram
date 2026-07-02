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

    // CAPTCHA answer is a string — most questions expect a word answer, not just a number
    private string _captchaExpectedStr = "";

    private void GenerateCaptcha()
    {
        var rng  = new Random();
        int kind = rng.Next(0, 12);

        switch (kind)
        {
            // ── Category: what doesn't belong ────────────────────────────────
            case 0:
            {
                var groups = new (string question, string answer)[]
                {
                    ("Cat, Dog, Hammer, Horse — which one does not belong?", "hammer"),
                    ("Red, Blue, Banana, Green — which one is not a colour?", "banana"),
                    ("Piano, Guitar, Trumpet, Chair — which is not a musical instrument?", "chair"),
                    ("Apple, Carrot, Banana, Grape — which one is a vegetable?", "carrot"),
                    ("London, Paris, Football, Tokyo — which is not a city?", "football"),
                    ("Earth, Moon, Jupiter, Ocean — which is not in space?", "ocean"),
                    ("Rose, Daisy, Tiger, Tulip — which is not a flower?", "tiger"),
                    ("Swimming, Running, Sleeping, Cycling — which is not exercise?", "sleeping"),
                    ("Shark, Eagle, Salmon, Tuna — which does not live in water?", "eagle"),
                    ("January, March, Monday, July — which is not a month?", "monday"),
                };
                var pick = groups[rng.Next(groups.Length)];
                _captchaExpectedStr = pick.answer;
                CaptchaQuestion = $"Type the answer in lowercase.\n{pick.question}";
                break;
            }
            // ── Category: word analogy ────────────────────────────────────────
            case 1:
            {
                var analogies = new (string q, string a)[]
                {
                    ("Puppy is to Dog as Kitten is to ___?", "cat"),
                    ("Hot is to Cold as Day is to ___?", "night"),
                    ("Hand is to Glove as Foot is to ___?", "shoe"),
                    ("Bird is to Nest as Human is to ___?", "house"),
                    ("Car is to Road as Boat is to ___?", "water"),
                    ("Doctor is to Hospital as Teacher is to ___?", "school"),
                    ("Book is to Library as Painting is to ___?", "museum"),
                    ("Pen is to Write as Knife is to ___?", "cut"),
                    ("Eyes are to See as Ears are to ___?", "hear"),
                    ("Chapter is to Book as Episode is to ___?", "series"),
                };
                var pick = analogies[rng.Next(analogies.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = $"Complete the analogy (type one word).\n{pick.q}";
                break;
            }
            // ── Category: logic / riddle ──────────────────────────────────────
            case 2:
            {
                var riddles = new (string q, string a)[]
                {
                    ("I have hands but I cannot clap. What am I?", "clock"),
                    ("I have keys but no locks. I have space but no room. What am I?", "keyboard"),
                    ("The more you take, the more you leave behind. What am I?", "footsteps"),
                    ("I speak without a mouth and hear without ears. What am I?", "echo"),
                    ("I get wetter as I dry. What am I?", "towel"),
                    ("What has cities but no houses, rivers but no water, and forests but no trees?", "map"),
                    ("I have a neck but no head. What am I?", "bottle"),
                    ("What can run but never walks, has a mouth but never talks?", "river"),
                    ("I have teeth but cannot eat. What am I?", "comb"),
                    ("What has one eye but cannot see?", "needle"),
                };
                var pick = riddles[rng.Next(riddles.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = $"Answer the riddle (type one word).\n\"{pick.q}\"";
                break;
            }
            // ── Category: common knowledge ────────────────────────────────────
            case 3:
            {
                var facts = new (string q, string a)[]
                {
                    ("How many days are in a week?", "7"),
                    ("How many months are in a year?", "12"),
                    ("How many sides does a triangle have?", "3"),
                    ("How many hours are in a day?", "24"),
                    ("How many letters are in the English alphabet?", "26"),
                    ("How many planets are in our solar system?", "8"),
                    ("How many legs does a spider have?", "8"),
                    ("How many seasons are there in a year?", "4"),
                    ("How many minutes are in an hour?", "60"),
                    ("How many continents are there on Earth?", "7"),
                };
                var pick = facts[rng.Next(facts.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = pick.q;
                break;
            }
            // ── Category: number sequence with rule ───────────────────────────
            case 4:
            {
                int start = rng.Next(1, 15), step = rng.Next(2, 9);
                int pos = rng.Next(1, 4);
                int[] seq = new int[5];
                for (int i = 0; i < 5; i++) seq[i] = start + i * step;
                _captchaExpectedStr = seq[pos].ToString();
                var display = new string[5];
                for (int i = 0; i < 5; i++) display[i] = i == pos ? "?" : seq[i].ToString();
                CaptchaQuestion = $"Find the missing number:\n{string.Join(", ", display)}";
                break;
            }
            // ── Category: what comes next (letter pattern) ────────────────────
            case 5:
            {
                var patterns = new (string q, string a)[]
                {
                    ("A, C, E, G, ___?  (every other letter)", "i"),
                    ("Z, Y, X, W, ___?  (letters backwards)", "v"),
                    ("B, D, F, H, ___?  (every other letter)", "j"),
                    ("A, B, D, E, G, H, ___?  (skip every third letter)", "j"),
                };
                var pick = patterns[rng.Next(patterns.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = $"Type the next letter (lowercase).\n{pick.q}";
                break;
            }
            // ── Category: true/false reasoning ───────────────────────────────
            case 6:
            {
                var tfs = new (string q, string a)[]
                {
                    ("A square has 4 equal sides. True or false?", "true"),
                    ("The Sun is a planet. True or false?", "false"),
                    ("All birds can fly. True or false?", "false"),
                    ("Water freezes at 0°C at sea level. True or false?", "true"),
                    ("A dozen means 12. True or false?", "true"),
                    ("Spiders are insects. True or false?", "false"),
                    ("The Pacific Ocean is the largest ocean. True or false?", "true"),
                    ("There are 100 centimetres in a metre. True or false?", "true"),
                    ("A human has 206 bones. True or false?", "true"),
                    ("Sound travels faster than light. True or false?", "false"),
                };
                var pick = tfs[rng.Next(tfs.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = $"Answer 'true' or 'false'.\n{pick.q}";
                break;
            }
            // ── Category: counting / spatial ─────────────────────────────────
            case 7:
            {
                var spatial = new (string q, string a)[]
                {
                    ("How many corners does a cube have?", "8"),
                    ("How many edges does a triangle have?", "3"),
                    ("How many faces does a cube have?", "6"),
                    ("How many sides does a pentagon have?", "5"),
                    ("How many sides does a hexagon have?", "6"),
                    ("How many wheels does a tricycle have?", "3"),
                    ("How many strings does a standard guitar have?", "6"),
                    ("How many players are on a football team?", "11"),
                    ("How many cards are in a standard deck (no jokers)?", "52"),
                    ("How many zeros are in one million?", "6"),
                };
                var pick = spatial[rng.Next(spatial.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = pick.q;
                break;
            }
            // ── Category: word scramble ───────────────────────────────────────
            case 8:
            {
                var scrambles = new (string q, string a)[]
                {
                    ("Unscramble: T-A-C", "cat"),
                    ("Unscramble: G-O-D", "dog"),
                    ("Unscramble: U-S-N", "sun"),
                    ("Unscramble: R-A-S-T", "star"),
                    ("Unscramble: E-T-R-E", "tree"),
                    ("Unscramble: I-F-S-H", "fish"),
                    ("Unscramble: I-R-D-B", "bird"),
                    ("Unscramble: O-M-N-O", "moon"),
                    ("Unscramble: O-D-F-O", "food"),
                    ("Unscramble: A-P-L-P-E", "apple"),
                };
                var pick = scrambles[rng.Next(scrambles.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = $"Type the word in lowercase.\n{pick.q}";
                break;
            }
            // ── Category: first/last letter ───────────────────────────────────
            case 9:
            {
                var words = new (string w, string first, string last)[]
                {
                    ("ELEPHANT", "e", "t"), ("MOUNTAIN", "m", "n"), ("LIBRARY", "l", "y"),
                    ("COMPUTER", "c", "r"), ("UMBRELLA", "u", "a"), ("DOLPHIN", "d", "n"),
                    ("VOLCANO", "v", "o"), ("CHIMNEY", "c", "y"), ("PLANET", "p", "t"),
                    ("BRIDGE", "b", "e"),
                };
                var pick = words[rng.Next(words.Length)];
                bool askFirst = rng.Next(2) == 0;
                _captchaExpectedStr = askFirst ? pick.first : pick.last;
                CaptchaQuestion = askFirst
                    ? $"What is the first letter of {pick.w}? (lowercase)"
                    : $"What is the last letter of {pick.w}? (lowercase)";
                break;
            }
            // ── Category: simple arithmetic (kept as fallback, harder range) ──
            case 10:
            {
                int a = rng.Next(15, 80), b = rng.Next(15, 80);
                bool sub = rng.Next(2) == 0 && a > b;
                _captchaExpectedStr = sub ? (a - b).ToString() : (a + b).ToString();
                CaptchaQuestion = sub ? $"What is {a} − {b}?" : $"What is {a} + {b}?";
                break;
            }
            // ── Category: opposite word ───────────────────────────────────────
            default:
            {
                var opposites = new (string q, string a)[]
                {
                    ("What is the opposite of HOT?", "cold"),
                    ("What is the opposite of FAST?", "slow"),
                    ("What is the opposite of DAY?", "night"),
                    ("What is the opposite of HAPPY?", "sad"),
                    ("What is the opposite of BIG?", "small"),
                    ("What is the opposite of EMPTY?", "full"),
                    ("What is the opposite of START?", "stop"),
                    ("What is the opposite of LOUD?", "quiet"),
                    ("What is the opposite of LOVE?", "hate"),
                    ("What is the opposite of ANCIENT?", "modern"),
                };
                var pick = opposites[rng.Next(opposites.Length)];
                _captchaExpectedStr = pick.a;
                CaptchaQuestion = pick.q;
                break;
            }
        }

        _captchaExpected = 0; // unused — kept for field compatibility
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
            var (user, token, error) = await ServerClient.Instance.LoginAsync(u, Password);
            if (user == null) { ErrorMessage = error; return; }
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
        if (!string.Equals(CaptchaAnswer.Trim(), _captchaExpectedStr, StringComparison.OrdinalIgnoreCase))
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
