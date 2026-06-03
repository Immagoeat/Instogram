using System.ComponentModel.DataAnnotations;

namespace InstogramServer.Models;

public class User
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    [MaxLength(50)]  public string Username     { get; set; } = "";
    [MaxLength(100)] public string DisplayName  { get; set; } = "";
    public string PasswordHash  { get; set; } = "";
    [MaxLength(300)] public string Bio          { get; set; } = "";
    [MaxLength(200)] public string Website      { get; set; } = "";
    [MaxLength(100)] public string Email        { get; set; } = "";
    [MaxLength(30)]  public string Phone        { get; set; } = "";
    [MaxLength(200)] public string Address      { get; set; } = "";
    public string AccentColor   { get; set; } = "#8b5cf6";
    public string AvatarUrl     { get; set; } = "";
    public bool   NotifyDMs     { get; set; } = true;
    public bool   NotifyFollowedPosts { get; set; } = true;
    public bool   IsVerified    { get; set; } = false;
    public bool   IsMaster      { get; set; } = false;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    public ICollection<Follow>          Followers        { get; set; } = new List<Follow>();
    public ICollection<Follow>          Following        { get; set; } = new List<Follow>();
    public ICollection<Post>            Posts            { get; set; } = new List<Post>();
    public ICollection<FriendRequest>   SentRequests     { get; set; } = new List<FriendRequest>();
    public ICollection<FriendRequest>   ReceivedRequests { get; set; } = new List<FriendRequest>();
    public ICollection<ConversationMember> Conversations { get; set; } = new List<ConversationMember>();
}

public class Follow
{
    public Guid FollowerId { get; set; }
    public User Follower   { get; set; } = null!;
    public Guid FolloweeId { get; set; }
    public User Followee   { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FriendRequest
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   SenderId   { get; set; }
    public User   Sender     { get; set; } = null!;
    public Guid   RecipientId { get; set; }
    public User   Recipient  { get; set; } = null!;
    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum FriendRequestStatus { Pending, Accepted, Declined }

public class Post
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public Guid   AuthorId  { get; set; }
    public User   Author    { get; set; } = null!;
    public string Caption   { get; set; } = "";
    public string Tags      { get; set; } = ""; // comma-separated
    public string ImageUrl  { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PostLike>    Likes    { get; set; } = new List<PostLike>();
    public ICollection<Comment>     Comments { get; set; } = new List<Comment>();
}

public class PostLike
{
    public Guid PostId { get; set; }
    public Post Post   { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User   { get; set; } = null!;
}

public class Comment
{
    public Guid   Id       { get; set; } = Guid.NewGuid();
    public Guid   PostId   { get; set; }
    public Post   Post     { get; set; } = null!;
    public Guid   AuthorId { get; set; }
    public User   Author   { get; set; } = null!;
    public string Text     { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Story
{
    public Guid   Id              { get; set; } = Guid.NewGuid();
    public Guid   AuthorId        { get; set; }
    public User   Author          { get; set; } = null!;
    public string Text            { get; set; } = "";
    public string BackgroundColor { get; set; } = "#1a0a3a";
    public string ImageUrl        { get; set; } = "";
    // Fractional position (0‒1) and scale of the text overlay
    public double TextX           { get; set; } = 0.5;
    public double TextY           { get; set; } = 0.5;
    public double TextScale       { get; set; } = 1.0;
    public double TextRotation    { get; set; } = 0.0;
    // Comma-separated usernames tagged in this story
    public string TaggedUsers     { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt     { get; set; } = DateTime.UtcNow.AddHours(24);
    public ICollection<StorySeen> SeenBy { get; set; } = new List<StorySeen>();
}

public class StorySeen
{
    public Guid StoryId { get; set; }
    public Story Story  { get; set; } = null!;
    public Guid UserId  { get; set; }
    public User User    { get; set; } = null!;
}

// ── Conversations (1-on-1 and group) ─────────────────────────────────────────

public class Conversation
{
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string Name      { get; set; } = "";  // empty = DM, set = group
    public bool   IsGroup   { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ConversationMember>  Members  { get; set; } = new List<ConversationMember>();
    public ICollection<Message>             Messages { get; set; } = new List<Message>();
}

public class ConversationMember
{
    public Guid         ConversationId { get; set; }
    public Conversation Conversation   { get; set; } = null!;
    public Guid         UserId         { get; set; }
    public User         User           { get; set; } = null!;
    public bool         IsAdmin        { get; set; }
    public DateTime     JoinedAt       { get; set; } = DateTime.UtcNow;
}

public class Message
{
    public Guid         Id             { get; set; } = Guid.NewGuid();
    public Guid         ConversationId { get; set; }
    public Conversation Conversation   { get; set; } = null!;
    public Guid         SenderId       { get; set; }
    public User         Sender         { get; set; } = null!;
    public string       Text           { get; set; } = "";
    public DateTime     SentAt         { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public Guid     RecipientId  { get; set; }
    public User     Recipient    { get; set; } = null!;
    public Guid     ActorId      { get; set; }
    public string   Type         { get; set; } = ""; // "dm","post","follow","like","friend"
    public string   Body         { get; set; } = "";
    public Guid?    RelatedPostId { get; set; }
    public bool     IsRead       { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
}
