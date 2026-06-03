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

    public override async Task OnConnectedAsync()
    {
        var convIds = await db.ConvMembers
            .Where(m => m.UserId == Me)
            .Select(m => m.ConversationId)
            .ToListAsync();
        foreach (var id in convIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv:{id}");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{Me}");
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(Guid conversationId, string text)
    {
        var isMember = await db.ConvMembers
            .AnyAsync(m => m.ConversationId == conversationId && m.UserId == Me);
        if (!isMember) return;

        var msg = new Message { ConversationId = conversationId, SenderId = Me, Text = text.Trim() };
        db.Messages.Add(msg);

        var memberIds = await db.ConvMembers
            .Where(m => m.ConversationId == conversationId && m.UserId != Me)
            .Select(m => m.UserId).ToListAsync();

        var me = await db.Users.FindAsync(Me);
        var notifiableIds = await db.Users
            .Where(u => memberIds.Contains(u.Id) && u.NotifyDMs)
            .Select(u => u.Id)
            .ToListAsync();
        var preview = text.Length > 40 ? text[..37] + "…" : text;
        foreach (var uid in notifiableIds)
            db.Notifications.Add(new Notification
            {
                RecipientId = uid, ActorId = Me, Type = "dm",
                Body = $"@{me?.Username} messaged you: {preview}"
            });

        await db.SaveChangesAsync();

        var payload = new
        {
            msg.Id, msg.ConversationId, msg.SenderId,
            SenderUsername = me?.Username ?? "",
            msg.Text, msg.SentAt
        };
        await Clients.Group($"conv:{conversationId}").SendAsync("NewMessage", payload);

        foreach (var uid in memberIds)
        {
            var count = await db.Notifications.CountAsync(n => n.RecipientId == uid && !n.IsRead);
            await Clients.Group($"user:{uid}").SendAsync("NotificationCount", count);
        }
    }

    public async Task Typing(Guid conversationId)
    {
        var me = await db.Users.FindAsync(Me);
        await Clients.OthersInGroup($"conv:{conversationId}")
            .SendAsync("UserTyping", new { conversationId, username = me?.Username });
    }

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

    private Task<bool> IsAdminAsync(Guid conversationId) =>
        db.ConvMembers.AnyAsync(m => m.ConversationId == conversationId && m.UserId == Me && m.IsAdmin);

    public async Task RenameConversation(Guid conversationId, string newName)
    {
        if (!await IsAdminAsync(conversationId)) return;

        var conv = await db.Conversations.FindAsync(conversationId);
        if (conv == null) return;
        conv.Name = newName.Trim();
        await db.SaveChangesAsync();

        await Clients.Group($"conv:{conversationId}")
            .SendAsync("ConversationRenamed", new { conversationId, newName = conv.Name });
    }

    public async Task AddMember(Guid conversationId, Guid userId)
    {
        if (!await IsAdminAsync(conversationId)) return;

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
