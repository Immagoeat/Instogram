using System.Security.Claims;
using System.Text;
using InstogramServer.Data;
using InstogramServer.Hubs;
using InstogramServer.Models;
using InstogramServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key missing from config");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer   = true,
            ValidIssuer      = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience    = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true
        };
        // Allow SignalR to get the token from the query string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hub"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── EF Core + SQLite ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=instogram.db"));

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddScoped<TokenService>();

var app = builder.Build();

// ── Migrate ───────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Shared directories (declared early so all route groups can use them) ──────
var imageDir = app.Configuration["ImageDir"]
    ?? Path.Combine(AppContext.BaseDirectory, "images");
Directory.CreateDirectory(imageDir);

var avatarDir = app.Configuration["AvatarDir"]
    ?? Path.Combine(AppContext.BaseDirectory, "avatars");
Directory.CreateDirectory(avatarDir);

// ── DTOs ──────────────────────────────────────────────────────────────────────
static object UserDto(User u) => new
{
    u.Id, u.Username, u.DisplayName, u.Bio, u.Website,
    u.AccentColor, u.AvatarUrl, u.Email, u.Phone, u.Address,
    u.IsVerified, u.IsMaster, u.CreatedAt
};

static object PostDto(Post p, Guid me) => new
{
    p.Id, p.AuthorId,
    AuthorUsername = p.Author.Username,
    AuthorDisplayName = p.Author.DisplayName,
    AuthorAccent = p.Author.AccentColor,
    p.Caption, p.Tags, p.ImageUrl, p.CreatedAt,
    LikeCount   = p.Likes.Count,
    IsLiked     = p.Likes.Any(l => l.UserId == me),
    Comments    = p.Comments.OrderBy(c => c.CreatedAt).Select(c => new
    {
        c.Id, c.AuthorId,
        AuthorUsername = c.Author.Username,
        c.Text, c.CreatedAt
    })
};

static object StoryDto(Story s, User author, bool hasSeen) => new
{
    s.Id, s.AuthorId,
    AuthorUsername    = author.Username,
    AuthorDisplayName = author.DisplayName,
    AuthorAccent      = author.AccentColor,
    s.Text, s.BackgroundColor, s.ImageUrl,
    s.TextX, s.TextY, s.TextScale, s.TextRotation, s.TaggedUsers,
    s.CreatedAt, s.ExpiresAt,
    HasSeen = hasSeen
};

static object ConvDto(Conversation c, Guid me) => new
{
    c.Id, c.Name, c.IsGroup, c.CreatedAt,
    Members = c.Members.Select(m => new
    {
        m.UserId,
        m.User.Username,
        m.User.DisplayName,
        m.User.AccentColor,
        m.IsAdmin
    }),
    LastMessage = c.Messages.OrderByDescending(m => m.SentAt).Select(m => new
    {
        m.Id, m.SenderId, m.Text, m.SentAt
    }).FirstOrDefault()
};

// ═════════════════════════════════════════════════════════════════════════════
// AUTH
// ═════════════════════════════════════════════════════════════════════════════

var auth = app.MapGroup("/auth");

auth.MapPost("/register", async (RegisterRequest req, AppDbContext db, TokenService tokens) =>
{
    var normalized = req.Username.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(u => u.Username.ToLower() == normalized))
        return Results.Conflict("Username taken");

    var user = new User
    {
        Username     = normalized,
        DisplayName  = req.DisplayName.Trim(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        Email   = req.Email,
        Phone   = req.Phone,
        Address = req.Address
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Ok(new { token = tokens.Generate(user), user = UserDto(user) });
});

auth.MapPost("/login", async (LoginRequest req, AppDbContext db, TokenService tokens) =>
{
    var normalized = req.Username.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);
    if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();
    return Results.Ok(new { token = tokens.Generate(user), user = UserDto(user) });
});

// ═════════════════════════════════════════════════════════════════════════════
// USERS
// ═════════════════════════════════════════════════════════════════════════════

var users = app.MapGroup("/users").RequireAuthorization();

users.MapGet("/me", async (HttpContext ctx, AppDbContext db) =>
{
    var me = await db.Users.FindAsync(TokenService.UserIdFromContext(ctx));
    return me == null ? Results.NotFound() : Results.Ok(UserDto(me));
});

users.MapPut("/me", async (UpdateProfileRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me = await db.Users.FindAsync(TokenService.UserIdFromContext(ctx));
    if (me == null) return Results.NotFound();
    me.DisplayName = req.DisplayName.Trim();
    me.Bio         = req.Bio;
    me.Website     = req.Website;
    me.Email       = req.Email;
    me.Phone       = req.Phone;
    me.Address     = req.Address;
    me.AccentColor = req.AccentColor;
    me.NotifyDMs   = req.NotifyDMs;
    me.NotifyFollowedPosts = req.NotifyFollowedPosts;
    await db.SaveChangesAsync();
    return Results.Ok(UserDto(me));
});

users.MapGet("/search", async (string q, AppDbContext db) =>
{
    var results = await db.Users
        .Where(u => u.Username.Contains(q) || u.DisplayName.Contains(q))
        .Take(20)
        .Select(u => new { u.Id, u.Username, u.DisplayName, u.AccentColor })
        .ToListAsync();
    return Results.Ok(results);
});

users.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var user = await db.Users
        .Include(u => u.Followers)
        .Include(u => u.Following)
        .FirstOrDefaultAsync(u => u.Id == id);
    if (user == null) return Results.NotFound();
    var hasPending = me != Guid.Empty && await db.FriendRequests.AnyAsync(r =>
        r.SenderId == me && r.RecipientId == id && r.Status == FriendRequestStatus.Pending);
    var isFriend = me != Guid.Empty && await db.FriendRequests.AnyAsync(r =>
        r.Status == FriendRequestStatus.Accepted &&
        ((r.SenderId == me && r.RecipientId == id) || (r.SenderId == id && r.RecipientId == me)));
    return Results.Ok(new
    {
        User = UserDto(user),
        FollowerCount = user.Followers.Count,
        FollowingCount = user.Following.Count,
        IsFollowing = user.Followers.Any(f => f.FollowerId == me),
        HasPendingRequest = hasPending,
        IsFriend = isFriend
    });
});

users.MapPost("/{id:guid}/verify", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me = await db.Users.FindAsync(TokenService.UserIdFromContext(ctx));
    if (me == null || !me.IsMaster) return Results.Forbid();
    var target = await db.Users.FindAsync(id);
    if (target == null) return Results.NotFound();
    target.IsVerified = !target.IsVerified;
    await db.SaveChangesAsync();
    return Results.Ok(new { target.Id, target.Username, target.IsVerified });
});

users.MapPost("/{id:guid}/follow", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    if (me == id) return Results.BadRequest();
    var already = await db.Follows.AnyAsync(f => f.FollowerId == me && f.FolloweeId == id);
    if (already) return Results.Ok();
    db.Follows.Add(new Follow { FollowerId = me, FolloweeId = id });

    var target = await db.Users.FindAsync(id);
    var actor  = await db.Users.FindAsync(me);
    if (target != null)
        db.Notifications.Add(new Notification
        {
            RecipientId = id, ActorId = me, Type = "follow",
            Body = $"@{actor?.Username} started following you"
        });

    await db.SaveChangesAsync();
    return Results.Ok();
});

users.MapDelete("/{id:guid}/follow", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var f  = await db.Follows.FindAsync(me, id);
    if (f != null) { db.Follows.Remove(f); await db.SaveChangesAsync(); }
    return Results.Ok();
});

// ═════════════════════════════════════════════════════════════════════════════
// FRIEND REQUESTS
// ═════════════════════════════════════════════════════════════════════════════

var friends = app.MapGroup("/friends").RequireAuthorization();

friends.MapPost("/request/{targetId:guid}", async (Guid targetId, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    if (me == targetId) return Results.BadRequest();
    var exists = await db.FriendRequests.AnyAsync(r =>
        r.SenderId == me && r.RecipientId == targetId && r.Status == FriendRequestStatus.Pending);
    if (exists) return Results.Ok();
    db.FriendRequests.Add(new FriendRequest { SenderId = me, RecipientId = targetId });

    var actor = await db.Users.FindAsync(me);
    db.Notifications.Add(new Notification
    {
        RecipientId = targetId, ActorId = me, Type = "friend",
        Body = $"@{actor?.Username} sent you a friend request"
    });
    await db.SaveChangesAsync();
    return Results.Ok();
});

friends.MapPost("/accept/{requestId:guid}", async (Guid requestId, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var req = await db.FriendRequests.FindAsync(requestId);
    if (req == null || req.RecipientId != me) return Results.NotFound();
    req.Status = FriendRequestStatus.Accepted;
    if (!await db.Follows.AnyAsync(f => f.FollowerId == me && f.FolloweeId == req.SenderId))
        db.Follows.Add(new Follow { FollowerId = me, FolloweeId = req.SenderId });
    if (!await db.Follows.AnyAsync(f => f.FollowerId == req.SenderId && f.FolloweeId == me))
        db.Follows.Add(new Follow { FollowerId = req.SenderId, FolloweeId = me });

    await db.SaveChangesAsync();
    return Results.Ok();
});

friends.MapPost("/decline/{requestId:guid}", async (Guid requestId, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var req = await db.FriendRequests.FindAsync(requestId);
    if (req == null || req.RecipientId != me) return Results.NotFound();
    req.Status = FriendRequestStatus.Declined;
    await db.SaveChangesAsync();
    return Results.Ok();
});

friends.MapGet("/requests/incoming", async (HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var reqs = await db.FriendRequests
        .Include(r => r.Sender)
        .Where(r => r.RecipientId == me && r.Status == FriendRequestStatus.Pending)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new { r.Id, r.SenderId, r.Sender.Username, r.Sender.DisplayName, r.Sender.AccentColor, r.CreatedAt })
        .ToListAsync();
    return Results.Ok(reqs);
});

friends.MapGet("/requests/outgoing", async (HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var reqs = await db.FriendRequests
        .Include(r => r.Recipient)
        .Where(r => r.SenderId == me && r.Status == FriendRequestStatus.Pending)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new { r.Id, r.RecipientId, r.Recipient.Username, r.Recipient.DisplayName, r.CreatedAt })
        .ToListAsync();
    return Results.Ok(reqs);
});

friends.MapGet("/list", async (HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var accepted = await db.FriendRequests
        .Where(r => r.Status == FriendRequestStatus.Accepted &&
                    (r.SenderId == me || r.RecipientId == me))
        .ToListAsync();
    var friendIds = accepted.Select(r => r.SenderId == me ? r.RecipientId : r.SenderId).ToList();
    var users = await db.Users
        .Where(u => friendIds.Contains(u.Id))
        .Select(u => new { u.Id, u.Username, u.DisplayName, u.AccentColor, u.IsVerified, u.IsMaster })
        .ToListAsync();
    return Results.Ok(users);
});

// ═════════════════════════════════════════════════════════════════════════════
// POSTS
// ═════════════════════════════════════════════════════════════════════════════

var posts = app.MapGroup("/posts").RequireAuthorization();

posts.MapPost("", async (CreatePostRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var post = new Post { AuthorId = me, Caption = req.Caption.Trim(), Tags = req.Tags };
    db.Posts.Add(post);
    var actor = await db.Users.FindAsync(me);
    var followerIds = await db.Follows
        .Where(f => f.FolloweeId == me)
        .Select(f => f.FollowerId)
        .ToListAsync();
    var notifiableFollowers = await db.Users
        .Where(u => followerIds.Contains(u.Id) && u.NotifyFollowedPosts)
        .Select(u => u.Id)
        .ToListAsync();
    foreach (var fid in notifiableFollowers)
        db.Notifications.Add(new Notification
        {
            RecipientId = fid, ActorId = me, Type = "post",
            RelatedPostId = post.Id,
            Body = $"@{actor?.Username} shared a new post"
        });

    await db.SaveChangesAsync();
    var created = await db.Posts
        .Include(p => p.Author)
        .Include(p => p.Likes)
        .Include(p => p.Comments).ThenInclude(c => c.Author)
        .FirstAsync(p => p.Id == post.Id);
    return Results.Ok(PostDto(created, me));
});

posts.MapGet("/feed", async (HttpContext ctx, AppDbContext db, int page = 0) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var followingIds = await db.Follows.Where(f => f.FollowerId == me).Select(f => f.FolloweeId).ToListAsync();
    followingIds.Add(me);
    var feed = await db.Posts
        .Include(p => p.Author)
        .Include(p => p.Likes)
        .Include(p => p.Comments).ThenInclude(c => c.Author)
        .Where(p => followingIds.Contains(p.AuthorId))
        .OrderByDescending(p => p.CreatedAt)
        .Skip(page * 20).Take(20)
        .ToListAsync();
    return Results.Ok(feed.Select(p => PostDto(p, me)));
});

posts.MapGet("/explore", async (HttpContext ctx, AppDbContext db, string? tag = null, string? q = null, int page = 0) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var query = db.Posts.Include(p => p.Author).Include(p => p.Likes)
        .Include(p => p.Comments).ThenInclude(c => c.Author)
        .AsQueryable();
    if (!string.IsNullOrEmpty(tag))
        query = query.Where(p => p.Tags.Contains(tag));
    if (!string.IsNullOrEmpty(q))
        query = query.Where(p => p.Caption.Contains(q) || p.Tags.Contains(q) || p.Author.Username.Contains(q) || p.Author.DisplayName.Contains(q));
    var results = await query.OrderByDescending(p => p.CreatedAt).Skip(page * 20).Take(20).ToListAsync();
    return Results.Ok(results.Select(p => PostDto(p, me)));
});

posts.MapGet("/trending-tags", async (AppDbContext db) =>
{
    var recent = await db.Posts
        .Where(p => !string.IsNullOrEmpty(p.Tags))
        .OrderByDescending(p => p.CreatedAt)
        .Take(200)
        .Select(p => p.Tags)
        .ToListAsync();

    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var tagStr in recent)
        foreach (var tag in tagStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            counts[tag] = counts.GetValueOrDefault(tag) + 1;

    var top = counts.OrderByDescending(kv => kv.Value).Take(20)
        .Select(kv => new { tag = kv.Key, count = kv.Value });
    return Results.Ok(top);
});

posts.MapPost("/{id:guid}/like", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var post = await db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == id);
    if (post == null) return Results.NotFound();
    var liked = await db.PostLikes.AnyAsync(l => l.PostId == id && l.UserId == me);
    if (!liked)
    {
        db.PostLikes.Add(new PostLike { PostId = id, UserId = me });
        if (post.AuthorId != me)
        {
            var actor = await db.Users.FindAsync(me);
            db.Notifications.Add(new Notification
            {
                RecipientId = post.AuthorId, ActorId = me, Type = "like",
                RelatedPostId = id,
                Body = $"@{actor?.Username} liked your post"
            });
        }
    }
    else
    {
        var like = await db.PostLikes.FindAsync(id, me);
        if (like != null) db.PostLikes.Remove(like);
    }

    await db.SaveChangesAsync();
    var count = await db.PostLikes.CountAsync(l => l.PostId == id);
    return Results.Ok(new { liked = !liked, count });
});

posts.MapPost("/{id:guid}/comment", async (Guid id, CommentRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var post = await db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == id);
    if (post == null) return Results.NotFound();
    var actor   = await db.Users.FindAsync(me);
    var comment = new Comment { PostId = id, AuthorId = me, Text = req.Text.Trim() };
    db.Comments.Add(comment);
    if (post.AuthorId != me)
        db.Notifications.Add(new Notification
        {
            RecipientId = post.AuthorId, ActorId = me, Type = "comment",
            RelatedPostId = id,
            Body = $"@{actor?.Username} commented: {(req.Text.Length > 40 ? req.Text[..37] + "…" : req.Text)}"
        });
    await db.SaveChangesAsync();
    return Results.Ok(new { comment.Id, comment.PostId, comment.AuthorId, AuthorUsername = actor?.Username, comment.Text, comment.CreatedAt });
});

// ═════════════════════════════════════════════════════════════════════════════
// STORIES
// ═════════════════════════════════════════════════════════════════════════════

var stories = app.MapGroup("/stories").RequireAuthorization();

stories.MapPost("", async (CreateStoryRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var author = await db.Users.FindAsync(me);
    if (author == null) return Results.Unauthorized();
    var story = new Story
    {
        AuthorId        = me,
        Text            = req.Text,
        BackgroundColor = req.BackgroundColor,
        TextX           = req.TextX,
        TextY           = req.TextY,
        TextScale       = req.TextScale,
        TextRotation    = req.TextRotation,
        TaggedUsers     = req.TaggedUsers
    };
    db.Stories.Add(story);
    await db.SaveChangesAsync();
    return Results.Ok(StoryDto(story, author, false));
});

stories.MapPost("/{id:guid}/image", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me    = TokenService.UserIdFromContext(ctx);
    var story = await db.Stories.FindAsync(id);
    if (story == null || story.AuthorId != me) return Results.NotFound();

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null) return Results.BadRequest("No file");
    if (file.Length > 15 * 1024 * 1024) return Results.BadRequest("File too large (max 15 MB)");

    var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
    var filename = $"story_{id:N}{ext}";
    var dest     = Path.Combine(imageDir, filename);
    await using var fs = File.Create(dest);
    await file.CopyToAsync(fs);

    story.ImageUrl = $"/images/{filename}";
    await db.SaveChangesAsync();
    return Results.Ok(new { imageUrl = story.ImageUrl });
});

stories.MapGet("/feed", async (HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var followingIds = await db.Follows.Where(f => f.FollowerId == me).Select(f => f.FolloweeId).ToListAsync();
    followingIds.Add(me);
    var now = DateTime.UtcNow;
    var feed = await db.Stories
        .Include(s => s.Author)
        .Include(s => s.SeenBy)
        .Where(s => followingIds.Contains(s.AuthorId) && s.ExpiresAt > now)
        .OrderByDescending(s => s.CreatedAt)
        .ToListAsync();
    return Results.Ok(feed.Select(s => StoryDto(s, s.Author, s.SeenBy.Any(ss => ss.UserId == me))));
});

stories.MapPost("/{id:guid}/seen", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me  = TokenService.UserIdFromContext(ctx);
    var seen = await db.StorySeens.AnyAsync(s => s.StoryId == id && s.UserId == me);
    if (!seen)
    {
        db.StorySeens.Add(new StorySeen { StoryId = id, UserId = me });
        await db.SaveChangesAsync();
    }
    return Results.Ok();
});

// ═════════════════════════════════════════════════════════════════════════════
// CONVERSATIONS & MESSAGES
// ═════════════════════════════════════════════════════════════════════════════

var convs = app.MapGroup("/conversations").RequireAuthorization();

convs.MapPost("/dm", async (DmRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    // Find existing DM
    var existing = await db.Conversations
        .Include(c => c.Members)
        .Include(c => c.Messages)
        .FirstOrDefaultAsync(c =>
            !c.IsGroup &&
            c.Members.Any(m => m.UserId == me) &&
            c.Members.Any(m => m.UserId == req.TargetUserId));
    if (existing != null)
        return Results.Ok(ConvDto(existing, me));

    var conv = new Conversation { IsGroup = false };
    conv.Members.Add(new ConversationMember { UserId = me, IsAdmin = false });
    conv.Members.Add(new ConversationMember { UserId = req.TargetUserId, IsAdmin = false });
    db.Conversations.Add(conv);
    await db.SaveChangesAsync();

    var created = await db.Conversations
        .Include(c => c.Members).ThenInclude(m => m.User)
        .Include(c => c.Messages)
        .FirstAsync(c => c.Id == conv.Id);
    return Results.Ok(ConvDto(created, me));
});

convs.MapPost("/group", async (GroupRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var conv = new Conversation { IsGroup = true, Name = req.Name.Trim() };
    conv.Members.Add(new ConversationMember { UserId = me, IsAdmin = true });
    foreach (var uid in req.MemberIds.Where(id => id != me))
        conv.Members.Add(new ConversationMember { UserId = uid, IsAdmin = false });
    db.Conversations.Add(conv);
    await db.SaveChangesAsync();

    var created = await db.Conversations
        .Include(c => c.Members).ThenInclude(m => m.User)
        .Include(c => c.Messages)
        .FirstAsync(c => c.Id == conv.Id);
    return Results.Ok(ConvDto(created, me));
});

convs.MapGet("", async (HttpContext ctx, AppDbContext db) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var list = await db.Conversations
        .Include(c => c.Members).ThenInclude(m => m.User)
        .Include(c => c.Messages)
        .Where(c => c.Members.Any(m => m.UserId == me))
        .OrderByDescending(c => c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt)
        .ToListAsync();
    return Results.Ok(list.Select(c => ConvDto(c, me)));
});

convs.MapPut("/{id:guid}/name", async (Guid id, RenameRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var conv = await db.Conversations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
    if (conv == null) return Results.NotFound();
    var member = conv.Members.FirstOrDefault(m => m.UserId == me);
    if (member == null || !member.IsAdmin) return Results.Forbid();
    conv.Name = req.Name.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new { conv.Id, conv.Name });
});

convs.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest req, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var conv = await db.Conversations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
    if (conv == null) return Results.NotFound();
    var member = conv.Members.FirstOrDefault(m => m.UserId == me);
    if (member == null || !member.IsAdmin) return Results.Forbid();
    if (conv.Members.Any(m => m.UserId == req.UserId)) return Results.Ok();
    conv.Members.Add(new ConversationMember { ConversationId = id, UserId = req.UserId });
    await db.SaveChangesAsync();
    return Results.Ok();
});

convs.MapGet("/{id:guid}/messages", async (Guid id, HttpContext ctx, AppDbContext db, int page = 0) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var isMember = await db.ConvMembers.AnyAsync(m => m.ConversationId == id && m.UserId == me);
    if (!isMember) return Results.Forbid();
    var msgs = await db.Messages
        .Include(m => m.Sender)
        .Where(m => m.ConversationId == id)
        .OrderByDescending(m => m.SentAt)
        .Skip(page * 50).Take(50)
        .ToListAsync();
    return Results.Ok(msgs.OrderBy(m => m.SentAt).Select(m => new
    {
        m.Id, m.ConversationId, m.SenderId,
        SenderUsername = m.Sender.Username,
        m.Text, m.SentAt
    }));
});

// ═════════════════════════════════════════════════════════════════════════════
// NOTIFICATIONS
// ═════════════════════════════════════════════════════════════════════════════

var notifs = app.MapGroup("/notifications").RequireAuthorization();

notifs.MapGet("", async (HttpContext ctx, AppDbContext db, int page = 0) =>
{
    var me = TokenService.UserIdFromContext(ctx);
    var list = await db.Notifications
        .Where(n => n.RecipientId == me)
        .OrderByDescending(n => n.CreatedAt)
        .Skip(page * 30).Take(30)
        .Select(n => new { n.Id, n.ActorId, n.Type, n.Body, n.RelatedPostId, n.IsRead, n.CreatedAt })
        .ToListAsync();
    return Results.Ok(list);
});

notifs.MapGet("/count", async (HttpContext ctx, AppDbContext db) =>
{
    var me    = TokenService.UserIdFromContext(ctx);
    var count = await db.Notifications.CountAsync(n => n.RecipientId == me && !n.IsRead);
    return Results.Ok(new { count });
});

notifs.MapPost("/read-all", async (HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var unread = await db.Notifications.Where(n => n.RecipientId == me && !n.IsRead).ToListAsync();
    foreach (var n in unread) n.IsRead = true;
    await db.SaveChangesAsync();
    return Results.Ok();
});

// ═════════════════════════════════════════════════════════════════════════════
// POST IMAGES — upload + serve
// ═════════════════════════════════════════════════════════════════════════════

app.MapGet("/images/{filename}", (string filename) =>
{
    if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
        return Results.BadRequest();
    var path = Path.Combine(imageDir, filename);
    if (!File.Exists(path)) return Results.NotFound();
    var ext  = Path.GetExtension(filename).ToLowerInvariant();
    var mime = ext is ".jpg" or ".jpeg" ? "image/jpeg"
             : ext == ".png"            ? "image/png"
             : ext == ".gif"            ? "image/gif"
             : ext == ".webp"           ? "image/webp"
             : "application/octet-stream";
    return Results.File(path, mime);
});

posts.MapPost("/{id:guid}/image", async (Guid id, HttpContext ctx, AppDbContext db) =>
{
    var me   = TokenService.UserIdFromContext(ctx);
    var post = await db.Posts.FindAsync(id);
    if (post == null || post.AuthorId != me) return Results.NotFound();

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null) return Results.BadRequest("No file");
    if (file.Length > 10 * 1024 * 1024) return Results.BadRequest("File too large (max 10 MB)");

    var ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
    var filename = $"post_{id:N}{ext}";
    var dest     = Path.Combine(imageDir, filename);
    await using var fs = File.Create(dest);
    await file.CopyToAsync(fs);

    post.ImageUrl = $"/images/{filename}";
    await db.SaveChangesAsync();
    return Results.Ok(new { imageUrl = post.ImageUrl });
});

// ═════════════════════════════════════════════════════════════════════════════
// AVATARS — upload + serve
// ═════════════════════════════════════════════════════════════════════════════

// Serve avatar files at /avatars/{filename}
app.MapGet("/avatars/{filename}", (string filename) =>
{
    // Reject any path traversal attempts
    if (filename.Contains('/') || filename.Contains('\\') || filename.Contains(".."))
        return Results.BadRequest();
    var path = Path.Combine(avatarDir, filename);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/png");
});

// Upload avatar (multipart/form-data, field: "file")
users.MapPost("/me/avatar", async (HttpContext ctx, AppDbContext db) =>
{
    var me = await db.Users.FindAsync(TokenService.UserIdFromContext(ctx));
    if (me == null) return Results.NotFound();

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null) return Results.BadRequest("No file");
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest("File too large (max 5 MB)");

    var filename = $"avatar_{me.Id:N}.png";
    var dest     = Path.Combine(avatarDir, filename);
    await using var fs = File.Create(dest);
    await file.CopyToAsync(fs);

    me.AvatarUrl = $"/avatars/{filename}";
    await db.SaveChangesAsync();
    return Results.Ok(new { avatarUrl = me.AvatarUrl });
});

// ═════════════════════════════════════════════════════════════════════════════
// SignalR hub
// ═════════════════════════════════════════════════════════════════════════════

app.MapHub<InstogramHub>("/hub");

app.Run();

// ── Request records ───────────────────────────────────────────────────────────
record RegisterRequest(string Username, string DisplayName, string Password,
    string Email = "", string Phone = "", string Address = "");
record LoginRequest(string Username, string Password);
record UpdateProfileRequest(string DisplayName, string Bio, string Website,
    string Email, string Phone, string Address, string AccentColor,
    bool NotifyDMs, bool NotifyFollowedPosts);
record CreatePostRequest(string Caption, string Tags);
record CommentRequest(string Text);
record CreateStoryRequest(string Text, string BackgroundColor,
    double TextX = 0.5, double TextY = 0.5, double TextScale = 1.0, double TextRotation = 0.0, string TaggedUsers = "");
record DmRequest(Guid TargetUserId);
record GroupRequest(string Name, List<Guid> MemberIds);
record RenameRequest(string Name);
record AddMemberRequest(Guid UserId);
