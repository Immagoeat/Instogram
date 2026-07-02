using InstogramDependencies;

namespace InstogramApp.Services;

public class AppState
{
    public static AppState Instance { get; } = new();

    public PersistenceService   Db            { get; } = new("data");
    public AccountService       Accounts      { get; private set; } = null!;
    public PostService          Posts         { get; private set; } = null!;
    public FeedService          Feed          { get; private set; } = null!;
    public DirectMessageService DMs           { get; private set; } = null!;
    public NotificationService  Notifications { get; private set; } = null!;
    public StoryService         Stories       { get; private set; } = null!;
    private User? _currentUser;
    public User? CurrentUser
    {
        get
        {
            // In server mode return a lightweight stub so ViewModels that do
            // CurrentUser! don't throw — real data comes from ServerClient.
            if (_currentUser == null && IsServerMode)
                _currentUser = new User
                {
                    Id          = System.Guid.TryParse(ServerUserId, out var g) ? g : System.Guid.Empty,
                    Username    = ServerUsername,
                    DisplayName = ServerDisplay,
                    AccentColor = ServerAccent,
                };
            return _currentUser;
        }
        set => _currentUser = value;
    }

    // Media device preferences (persisted in memory, reset on restart)
    public int    MicDeviceIndex     { get; set; } = -1;  // -1 = system default
    public int    SpeakerDeviceIndex { get; set; } = -1;
    public string CameraDevicePath   { get; set; } = "";  // "" = first available

    // FFmpeg lib path — detected once at first use
    private static string? _ffmpegLibPath;
    public static string FfmpegLibPath => _ffmpegLibPath ??= DetectFfmpegPath();
    private static string DetectFfmpegPath()
    {
        foreach (var path in new[] {
            "/usr/lib/x86_64-linux-gnu",
            "/usr/lib/aarch64-linux-gnu",
            "/usr/local/lib",
            "/usr/lib" })
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(path, "libavcodec.so.60")) ||
                System.IO.File.Exists(System.IO.Path.Combine(path, "libavcodec.so.58")))
                return path;
        }
        return "/usr/lib/x86_64-linux-gnu"; // fallback
    }

    // Server mode — when true, use ServerClient instead of local services
    public bool   IsServerMode     { get; set; }
    public string ServerUserId     { get; set; } = "";
    public string ServerUsername   { get; set; } = "";
    public string ServerDisplay    { get; set; } = "";
    public string ServerAccent     { get; set; } = "#8b5cf6";
    public bool   ServerIsVerified { get; set; }
    public bool   ServerIsMaster   { get; set; }

    private AppState()
    {
        (Accounts, Posts, DMs, Notifications, Stories) = Db.Load();
        Feed = new FeedService(Posts, Accounts);
    }

    public void Save() => Db.Save(Accounts, Posts, DMs, Notifications, Stories);

    // Helper: push a notification only if the recipient has opted in
    public void PushNotification(User recipient, User actor,
        NotificationType type, string body, System.Guid? relatedPostId = null)
    {
        bool allowed = type switch
        {
            NotificationType.DM      => recipient.NotifyDMs,
            NotificationType.NewPost => recipient.NotifyFollowedPosts,
            _                        => true
        };
        if (!allowed) return;
        Notifications.Push(recipient.Id, actor.Id, type, body, relatedPostId);
    }
}
