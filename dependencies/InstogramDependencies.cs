using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InstogramDependencies
{
    public class ID
    {
        public static void Print(string item) => Console.WriteLine(item);
    }

    // ── Models ────────────────────────────────────────────────────────────────

    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Bio { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string Website { get; set; } = "";
        // Hex colour string e.g. "#8b5cf6" used for avatar background and accent
        public string AccentColor { get; set; } = "#8b5cf6";
        // Absolute path to a local image file; empty = use initials avatar
        public string AvatarPath { get; set; } = "";
        public bool NotifyDMs { get; set; } = true;
        public bool NotifyFollowedPosts { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public HashSet<Guid> Followers { get; set; } = new();
        public HashSet<Guid> Following { get; set; } = new();

        [JsonConstructor]
        public User() { }

        public User(string username, string displayName, string passwordHash = "")
        {
            Username = username;
            DisplayName = displayName;
            PasswordHash = passwordHash;
        }

        public void Follow(User target)
        {
            if (target.Id == Id) return;
            Following.Add(target.Id);
            target.Followers.Add(Id);
        }

        public void Unfollow(User target)
        {
            Following.Remove(target.Id);
            target.Followers.Remove(Id);
        }

        public bool IsFollowing(User target) => Following.Contains(target.Id);

        public override string ToString() => $"@{Username} ({DisplayName})";
    }

    public enum MediaType { Image, Video, Text }

    public class Post
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AuthorId { get; set; }
        public string Caption { get; set; } = "";
        public MediaType Media { get; set; }
        public string MediaUrl { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public HashSet<Guid> Likes { get; set; } = new();

        [JsonConstructor]
        public Post() { }

        public Post(Guid authorId, string caption, MediaType media = MediaType.Image)
        {
            AuthorId = authorId;
            Caption = caption;
            Media = media;
        }

        public void Like(Guid userId) => Likes.Add(userId);
        public void Unlike(Guid userId) => Likes.Remove(userId);
        public void AddComment(Comment comment) => Comments.Add(comment);
    }

    public class Comment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AuthorId { get; set; }
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonConstructor]
        public Comment() { }

        public Comment(Guid authorId, string text)
        {
            AuthorId = authorId;
            Text = text;
        }
    }

    public class DirectMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SenderId { get; set; }
        public Guid RecipientId { get; set; }
        public string Text { get; set; } = "";
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }

        [JsonConstructor]
        public DirectMessage() { }

        public DirectMessage(Guid senderId, Guid recipientId, string text)
        {
            SenderId = senderId;
            RecipientId = recipientId;
            Text = text;
        }
    }

    public enum NotificationType { DM, NewPost, Follow, Like, Comment }

    public class NotificationItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecipientId { get; set; }
        public Guid ActorId { get; set; }        // who triggered it
        public NotificationType Type { get; set; }
        public string Body { get; set; } = "";   // pre-formatted summary text
        public Guid? RelatedPostId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }

        [JsonConstructor]
        public NotificationItem() { }
    }

    public class Story
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AuthorId { get; set; }
        public string Text { get; set; } = "";
        public string BackgroundColor { get; set; } = "#1a0a3a";
        public string TaggedUsers { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Stories expire after 24 h
        public bool IsExpired => (DateTime.UtcNow - CreatedAt).TotalHours >= 24;
        public HashSet<Guid> SeenBy { get; set; } = new();

        [JsonConstructor]
        public Story() { }

        public Story(Guid authorId, string text, string backgroundColor)
        {
            AuthorId = authorId;
            Text = text;
            BackgroundColor = backgroundColor;
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    file class DbFile
    {
        public List<User>             Users         { get; set; } = new();
        public List<Post>             Posts         { get; set; } = new();
        public List<DirectMessage>    Messages      { get; set; } = new();
        public List<NotificationItem> Notifications { get; set; } = new();
        public List<Story>            Stories       { get; set; } = new();
    }

    public class PersistenceService
    {
        private readonly string _igdbPath;
        private readonly string _legacyJsonPath;

        // App-specific salt mixed with a machine identifier to derive the encryption key.
        // Not secret on its own — security comes from the key derivation + GCM authentication.
        private static readonly byte[] _appSalt = Encoding.UTF8.GetBytes("InstogramDB-v1-salt-2025");

        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() }
        };

        public PersistenceService(string dataDirectory = ".")
        {
            Directory.CreateDirectory(dataDirectory);
            _igdbPath     = Path.Combine(dataDirectory, "instogram.igdb");
            _legacyJsonPath = Path.Combine(dataDirectory, "instogram.json");
        }

        // ── Key derivation ────────────────────────────────────────────────────

        private static byte[] DeriveKey()
        {
            // Mix machine-specific data with the app salt via PBKDF2 to produce a 256-bit key.
            // This ties the database to this machine without requiring a user-supplied passphrase.
            var machineId = GetMachineId();
            var keyMaterial = Encoding.UTF8.GetBytes(machineId);
            using var pbkdf2 = new Rfc2898DeriveBytes(keyMaterial, _appSalt, 100_000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32); // 256-bit key
        }

        private static string GetMachineId()
        {
            // Try /etc/machine-id (Linux) then fall back to a persisted random ID
            if (File.Exists("/etc/machine-id"))
            {
                var id = File.ReadAllText("/etc/machine-id").Trim();
                if (!string.IsNullOrEmpty(id)) return id;
            }

            var fallbackPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "instogram-mid.txt");

            if (File.Exists(fallbackPath))
                return File.ReadAllText(fallbackPath).Trim();

            var newId = Guid.NewGuid().ToString("N");
            try { File.WriteAllText(fallbackPath, newId); } catch { /* best-effort */ }
            return newId;
        }

        // ── AES-256-GCM helpers ───────────────────────────────────────────────

        // Wire format: [ 12-byte nonce ][ 16-byte tag ][ ciphertext ]
        private static byte[] Encrypt(byte[] plaintext, byte[] key)
        {
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[plaintext.Length];
            var tag        = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            nonce.CopyTo(result, 0);
            tag.CopyTo(result, nonce.Length);
            ciphertext.CopyTo(result, nonce.Length + tag.Length);
            return result;
        }

        private static byte[] Decrypt(byte[] data, byte[] key)
        {
            const int nonceLen = 12;
            const int tagLen   = 16;

            if (data.Length < nonceLen + tagLen)
                throw new CryptographicException("Invalid .igdb file: data too short.");

            var nonce      = data[..nonceLen];
            var tag        = data[nonceLen..(nonceLen + tagLen)];
            var ciphertext = data[(nonceLen + tagLen)..];
            var plaintext  = new byte[ciphertext.Length];

            using var aes = new AesGcm(key, tagLen);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Save(AccountService accounts, PostService posts,
                         DirectMessageService dms, NotificationService notifications,
                         StoryService stories)
        {
            var db = new DbFile
            {
                Users         = accounts.AllUsers().ToList(),
                Posts         = posts.AllPosts().ToList(),
                Messages      = dms.AllMessages().ToList(),
                Notifications = notifications.AllNotifications().ToList(),
                Stories       = stories.AllStories().ToList()
            };
            var json      = JsonSerializer.Serialize(db, _opts);
            var plaintext = Encoding.UTF8.GetBytes(json);
            var key       = DeriveKey();
            var encrypted = Encrypt(plaintext, key);
            File.WriteAllBytes(_igdbPath, encrypted);
        }

        public (AccountService accounts, PostService posts, DirectMessageService dms,
                NotificationService notifications, StoryService stories) Load()
        {
            var accounts      = new AccountService();
            var posts         = new PostService(accounts);
            var dms           = new DirectMessageService();
            var notifications = new NotificationService();
            var stories       = new StoryService();

            if (File.Exists(_igdbPath))
            {
                try
                {
                    var key       = DeriveKey();
                    var encrypted = File.ReadAllBytes(_igdbPath);
                    var plaintext = Decrypt(encrypted, key);
                    var json      = Encoding.UTF8.GetString(plaintext);
                    var db        = JsonSerializer.Deserialize<DbFile>(json, _opts);
                    if (db != null)
                    {
                        accounts.LoadUsers(db.Users);
                        posts.LoadPosts(db.Posts);
                        dms.LoadMessages(db.Messages);
                        notifications.LoadNotifications(db.Notifications);
                        stories.LoadStories(db.Stories);
                    }
                    return (accounts, posts, dms, notifications, stories);
                }
                catch (CryptographicException)
                {
                    return (accounts, posts, dms, notifications, stories);
                }
            }

            // Legacy migration path
            if (File.Exists(_legacyJsonPath))
            {
                var db = JsonSerializer.Deserialize<DbFile>(
                    File.ReadAllText(_legacyJsonPath), _opts);
                if (db != null)
                {
                    accounts.LoadUsers(db.Users);
                    posts.LoadPosts(db.Posts);
                }
                Save(accounts, posts, dms, notifications, stories);
                try { File.Delete(_legacyJsonPath); } catch { /* best-effort */ }
            }

            return (accounts, posts, dms, notifications, stories);
        }

        public string DataPath => _igdbPath;
    }

    // ── Services ──────────────────────────────────────────────────────────────

    public class AccountService
    {
        private readonly Dictionary<Guid, User> _users = new();
        private readonly Dictionary<string, Guid> _byUsername = new();

        public User Register(string username, string displayName, string password,
                             string email = "", string phone = "", string address = "")
        {
            if (_byUsername.ContainsKey(username.ToLower()))
                throw new InvalidOperationException($"Username '{username}' is already taken.");

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.");

            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User(username, displayName, hash)
            {
                Email   = email,
                Phone   = phone,
                Address = address
            };
            _users[user.Id] = user;
            _byUsername[username.ToLower()] = user.Id;
            return user;
        }

        public bool VerifyPassword(string username, string password)
        {
            var user = GetByUsername(username);
            if (user == null) return false;
            if (string.IsNullOrEmpty(user.PasswordHash)) return false;
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }

        // Restores users from deserialized data without re-generating IDs
        public void LoadUsers(IEnumerable<User> users)
        {
            foreach (var u in users)
            {
                _users[u.Id] = u;
                _byUsername[u.Username.ToLower()] = u.Id;
            }
        }

        public User? GetById(Guid id) => _users.TryGetValue(id, out var u) ? u : null;

        public User? GetByUsername(string username) =>
            _byUsername.TryGetValue(username.ToLower(), out var id) ? _users[id] : null;

        public bool Exists(string username) => _byUsername.ContainsKey(username.ToLower());

        public IReadOnlyList<User> AllUsers() => _users.Values.ToList();
    }

    public class PostService
    {
        private readonly Dictionary<Guid, Post> _posts = new();
        private readonly AccountService _accounts;

        public PostService(AccountService accounts) => _accounts = accounts;

        public Post CreatePost(User author, string caption,
            MediaType media = MediaType.Image, string mediaUrl = "",
            IEnumerable<string>? tags = null)
        {
            var post = new Post(author.Id, caption, media) { MediaUrl = mediaUrl };
            if (tags != null) post.Tags.AddRange(tags);
            _posts[post.Id] = post;
            return post;
        }

        public void LoadPosts(IEnumerable<Post> posts)
        {
            foreach (var p in posts)
                _posts[p.Id] = p;
        }

        public void Like(Post post, User user) => post.Like(user.Id);
        public void Unlike(Post post, User user) => post.Unlike(user.Id);

        public Comment AddComment(Post post, User author, string text)
        {
            var comment = new Comment(author.Id, text);
            post.AddComment(comment);
            return comment;
        }

        public IReadOnlyList<Post> GetPostsByUser(Guid userId) =>
            _posts.Values.Where(p => p.AuthorId == userId)
                         .OrderByDescending(p => p.CreatedAt)
                         .ToList();

        public Post? GetById(Guid id) => _posts.TryGetValue(id, out var p) ? p : null;

        public IReadOnlyList<Post> AllPosts() => _posts.Values.ToList();
    }

    public class DirectMessageService
    {
        private readonly List<DirectMessage> _messages = new();

        public DirectMessage Send(Guid senderId, Guid recipientId, string text)
        {
            var msg = new DirectMessage(senderId, recipientId, text);
            _messages.Add(msg);
            return msg;
        }

        public void LoadMessages(IEnumerable<DirectMessage> messages)
        {
            _messages.Clear();
            _messages.AddRange(messages);
        }

        // Returns all messages in a conversation between two users, oldest first
        public IReadOnlyList<DirectMessage> GetConversation(Guid a, Guid b) =>
            _messages
                .Where(m => (m.SenderId == a && m.RecipientId == b) ||
                             (m.SenderId == b && m.RecipientId == a))
                .OrderBy(m => m.SentAt)
                .ToList();

        // Returns the distinct set of user IDs who have exchanged messages with userId
        public IReadOnlyList<Guid> GetConversationPartners(Guid userId) =>
            _messages
                .Where(m => m.SenderId == userId || m.RecipientId == userId)
                .Select(m => m.SenderId == userId ? m.RecipientId : m.SenderId)
                .Distinct()
                .ToList();

        public int UnreadCount(Guid recipientId) =>
            _messages.Count(m => m.RecipientId == recipientId && !m.IsRead);

        public void MarkRead(Guid senderId, Guid recipientId)
        {
            foreach (var m in _messages.Where(m => m.SenderId == senderId && m.RecipientId == recipientId && !m.IsRead))
                m.IsRead = true;
        }

        public IReadOnlyList<DirectMessage> AllMessages() => _messages.AsReadOnly();
    }

    public class StoryService
    {
        private readonly List<Story> _stories = new();

        public Story Post(Guid authorId, string text, string backgroundColor)
        {
            var story = new Story(authorId, text, backgroundColor);
            _stories.Add(story);
            return story;
        }

        public void LoadStories(IEnumerable<Story> stories)
        {
            _stories.Clear();
            // Drop expired stories on load
            _stories.AddRange(stories.Where(s => !s.IsExpired));
        }

        // Active (non-expired) stories for users the viewer follows + own
        public IReadOnlyList<Story> GetStoriesForViewer(Guid viewerId, HashSet<Guid> following)
        {
            var relevant = new HashSet<Guid>(following) { viewerId };
            return _stories
                .Where(s => !s.IsExpired && relevant.Contains(s.AuthorId))
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        // All active stories by a single user, newest first
        public IReadOnlyList<Story> GetByUser(Guid userId) =>
            _stories.Where(s => s.AuthorId == userId && !s.IsExpired)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToList();

        public bool HasUnseenStories(Guid authorId, Guid viewerId) =>
            _stories.Any(s => s.AuthorId == authorId && !s.IsExpired && !s.SeenBy.Contains(viewerId));

        public void MarkSeen(Guid storyId, Guid viewerId)
        {
            var s = _stories.FirstOrDefault(s => s.Id == storyId);
            s?.SeenBy.Add(viewerId);
        }

        public IReadOnlyList<Story> AllStories() => _stories.AsReadOnly();
    }

    public class NotificationService
    {
        private readonly List<NotificationItem> _items = new();

        public NotificationItem Push(Guid recipientId, Guid actorId,
            NotificationType type, string body, Guid? relatedPostId = null)
        {
            var n = new NotificationItem
            {
                RecipientId    = recipientId,
                ActorId        = actorId,
                Type           = type,
                Body           = body,
                RelatedPostId  = relatedPostId
            };
            _items.Add(n);
            return n;
        }

        public void LoadNotifications(IEnumerable<NotificationItem> items)
        {
            _items.Clear();
            // Keep only last 90 days
            _items.AddRange(items.Where(n => (DateTime.UtcNow - n.CreatedAt).TotalDays <= 90));
        }

        public IReadOnlyList<NotificationItem> ForUser(Guid userId) =>
            _items.Where(n => n.RecipientId == userId)
                  .OrderByDescending(n => n.CreatedAt)
                  .ToList();

        public int UnreadCount(Guid userId) =>
            _items.Count(n => n.RecipientId == userId && !n.IsRead);

        public void MarkAllRead(Guid userId)
        {
            foreach (var n in _items.Where(n => n.RecipientId == userId && !n.IsRead))
                n.IsRead = true;
        }

        public IReadOnlyList<NotificationItem> AllNotifications() => _items.AsReadOnly();
    }

    public class FeedService
    {
        private readonly PostService _posts;
        private readonly AccountService _accounts;

        public FeedService(PostService posts, AccountService accounts)
        {
            _posts = posts;
            _accounts = accounts;
        }

        public IReadOnlyList<Post> GetFollowingFeed(User viewer, int limit = 20) =>
            _posts.AllPosts()
                  .Where(p => viewer.Following.Contains(p.AuthorId))
                  .OrderByDescending(p => p.CreatedAt)
                  .Take(limit)
                  .ToList();

        public IReadOnlyList<Post> GetRecommendedFeed(User viewer, int limit = 20)
        {
            var seen = _posts.AllPosts()
                             .Where(p => viewer.Following.Contains(p.AuthorId) || p.AuthorId == viewer.Id)
                             .Select(p => p.Id)
                             .ToHashSet();

            double Score(Post p)
            {
                double recency = 1.0 / (1.0 + (DateTime.UtcNow - p.CreatedAt).TotalHours);
                return (p.Likes.Count * 3 + p.Comments.Count * 2) * recency + recency * 10;
            }

            return _posts.AllPosts()
                         .Where(p => !seen.Contains(p.Id) && p.AuthorId != viewer.Id)
                         .OrderByDescending(Score)
                         .Take(limit)
                         .ToList();
        }

        public IReadOnlyList<Post> GetExploreFeed(int limit = 20) =>
            _posts.AllPosts()
                  .OrderByDescending(p => p.Likes.Count * 3 + p.Comments.Count * 2)
                  .ThenByDescending(p => p.CreatedAt)
                  .Take(limit)
                  .ToList();
    }

    // ── Display helpers ───────────────────────────────────────────────────────

    public static class Renderer
    {
        public static void PrintPost(Post post, AccountService accounts)
        {
            var author = accounts.GetById(post.AuthorId);
            Console.WriteLine($"┌─ [{post.Media}] @{author?.Username ?? "unknown"}  {post.CreatedAt:MMM dd, HH:mm}");
            Console.WriteLine($"│  {post.Caption}");
            if (post.Tags.Count > 0)
                Console.WriteLine($"│  {string.Join(" ", post.Tags.Select(t => "#" + t))}");
            Console.WriteLine($"│  ♥ {post.Likes.Count} likes   💬 {post.Comments.Count} comments");
            foreach (var c in post.Comments.Take(2))
            {
                var ca = accounts.GetById(c.AuthorId);
                Console.WriteLine($"│    @{ca?.Username ?? "?"}: {c.Text}");
            }
            if (post.Comments.Count > 2)
                Console.WriteLine($"│    ... and {post.Comments.Count - 2} more comment(s)");
            Console.WriteLine("└───────────────────────────────────────────");
        }

        public static void PrintUser(User user)
        {
            Console.WriteLine($"  @{user.Username}  |  {user.DisplayName}");
            if (!string.IsNullOrEmpty(user.Bio)) Console.WriteLine($"  {user.Bio}");
            Console.WriteLine($"  Followers: {user.Followers.Count}  Following: {user.Following.Count}");
        }

        public static void PrintFeed(string title, IReadOnlyList<Post> posts, AccountService accounts)
        {
            Console.WriteLine($"\n══ {title} ({posts.Count} posts) ══════════════════");
            if (posts.Count == 0) { Console.WriteLine("  (nothing here yet)"); return; }
            foreach (var p in posts) PrintPost(p, accounts);
        }
    }
}
