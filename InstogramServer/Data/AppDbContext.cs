using InstogramServer.Models;
using Microsoft.EntityFrameworkCore;

namespace InstogramServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>               Users         => Set<User>();
    public DbSet<Follow>             Follows       => Set<Follow>();
    public DbSet<FriendRequest>      FriendRequests => Set<FriendRequest>();
    public DbSet<Post>               Posts         => Set<Post>();
    public DbSet<PostLike>           PostLikes     => Set<PostLike>();
    public DbSet<Comment>            Comments      => Set<Comment>();
    public DbSet<Story>              Stories       => Set<Story>();
    public DbSet<StorySeen>          StorySeens    => Set<StorySeen>();
    public DbSet<Conversation>       Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConvMembers   => Set<ConversationMember>();
    public DbSet<Message>            Messages      => Set<Message>();
    public DbSet<Notification>       Notifications => Set<Notification>();
    public DbSet<BannedWord>         BannedWords   => Set<BannedWord>();
    public DbSet<AutomodFlag>        AutomodFlags  => Set<AutomodFlag>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Follow composite PK
        b.Entity<Follow>().HasKey(f => new { f.FollowerId, f.FolloweeId });
        b.Entity<Follow>()
            .HasOne(f => f.Follower).WithMany(u => u.Following).HasForeignKey(f => f.FollowerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Follow>()
            .HasOne(f => f.Followee).WithMany(u => u.Followers).HasForeignKey(f => f.FolloweeId).OnDelete(DeleteBehavior.Restrict);

        // PostLike composite PK
        b.Entity<PostLike>().HasKey(l => new { l.PostId, l.UserId });

        // StorySeen composite PK
        b.Entity<StorySeen>().HasKey(s => new { s.StoryId, s.UserId });

        // ConversationMember composite PK
        b.Entity<ConversationMember>().HasKey(m => new { m.ConversationId, m.UserId });

        // FriendRequest
        b.Entity<FriendRequest>()
            .HasOne(r => r.Sender).WithMany(u => u.SentRequests).HasForeignKey(r => r.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FriendRequest>()
            .HasOne(r => r.Recipient).WithMany(u => u.ReceivedRequests).HasForeignKey(r => r.RecipientId).OnDelete(DeleteBehavior.Restrict);

        // Username unique index
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
    }
}
