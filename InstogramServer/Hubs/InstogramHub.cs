using System.Security.Claims;
using InstogramServer.Data;
using InstogramServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InstogramServer.Hubs;

[Authorize]
public class InstogramHub(AppDbContext db) : Hub
{
    Guid Me => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Connection lifecycle ───────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        // Join all conversation groups this user belongs to
        var convIds = await db.ConvMembers
            .Where(m => m.UserId == Me)
            .Select(m => m.ConversationId)
            .ToListAsync();
        foreach (var id in convIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv:{id}");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{Me}");
        await base.OnConnectedAsync();
    }

    // ── Messaging ─────────────────────────────────────────────────────────

    public async Task SendMessage(Guid conversationId, string text)
    {
        // Verify membership
        var isMember = await db.ConvMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == Me);
        if (!isMember) return;

        var msg = new Message { ConversationId = conversationId, SenderId = Me, Text = text.Trim() };
        db.Messages.Add(msg);

        // Notify other members
        var memberIds = await db.ConvMembers
            .Where(m => m.ConversationId == conversationId && m.UserId != Me)
            .Select(m => m.UserId).ToListAsync();

        var me = await db.Users.FindAsync(Me);
        foreach (var uid in memberIds)
        {
            var recipient = await db.Users.FindAsync(uid);
            if (recipient?.NotifyDMs == true)
            {
                var notif = new Notification
                {
                    RecipientId = uid, ActorId = Me, Type = "dm",
                    Body = $"@{me?.Username} messaged you: {(text.Length > 40 ? text[..37] + "…" : text)}"
                };
                db.Notifications.Add(notif);
            }
        }

        await db.SaveChangesAsync();

        var payload = new
        {
            msg.Id, msg.ConversationId, msg.SenderId,
            SenderUsername = me?.Username ?? "",
            msg.Text, msg.SentAt
        };
        await Clients.Group($"conv:{conversationId}").SendAsync("NewMessage", payload);

        // Push unread count update to each recipient
        foreach (var uid in memberIds)
        {
            var count = await db.Notifications.CountAsync(n => n.RecipientId == uid && !n.IsRead);
            await Clients.Group($"user:{uid}").SendAsync("NotificationCount", count);
        }
    }

    // ── Typing indicator ──────────────────────────────────────────────────

    public async Task Typing(Guid conversationId)
    {
        var me = await db.Users.FindAsync(Me);
        await Clients.OthersInGroup($"conv:{conversationId}")
            .SendAsync("UserTyping", new { conversationId, username = me?.Username });
    }

    // ── Call signalling (WebRTC) ──────────────────────────────────────────

    public async Task CallUser(Guid targetUserId, string sdpOffer)
    {
        var me = await db.Users.FindAsync(Me);
        await Clients.Group($"user:{targetUserId}")
            .SendAsync("IncomingCall", new { CallerId = Me, CallerName = me?.DisplayName, sdpOffer });
    }

    public async Task CallAnswer(Guid callerId, string sdpAnswer) =>
        await Clients.Group($"user:{callerId}")
            .SendAsync("CallAnswered", new { AnswererId = Me, sdpAnswer });

    public async Task IceCandidate(Guid targetUserId, string candidate) =>
        await Clients.Group($"user:{targetUserId}")
            .SendAsync("IceCandidate", new { FromId = Me, candidate });

    public async Task HangUp(Guid targetUserId) =>
        await Clients.Group($"user:{targetUserId}")
            .SendAsync("CallEnded", new { FromId = Me });

    // ── Group chat management ─────────────────────────────────────────────

    public async Task RenameConversation(Guid conversationId, string newName)
    {
        var isAdmin = await db.ConvMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == Me && m.IsAdmin);
        if (!isAdmin) return;

        var conv = await db.Conversations.FindAsync(conversationId);
        if (conv == null) return;
        conv.Name = newName.Trim();
        await db.SaveChangesAsync();

        await Clients.Group($"conv:{conversationId}")
            .SendAsync("ConversationRenamed", new { conversationId, newName = conv.Name });
    }

    public async Task AddMember(Guid conversationId, Guid userId)
    {
        var isAdmin = await db.ConvMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == Me && m.IsAdmin);
        if (!isAdmin) return;

        var already = await db.ConvMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == userId);
        if (already) return;

        db.ConvMembers.Add(new ConversationMember { ConversationId = conversationId, UserId = userId });
        await db.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv:{conversationId}");
        var user = await db.Users.FindAsync(userId);
        await Clients.Group($"conv:{conversationId}")
            .SendAsync("MemberAdded", new { conversationId, userId, username = user?.Username });
    }
}
