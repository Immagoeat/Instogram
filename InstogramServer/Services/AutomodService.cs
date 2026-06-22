using System.Text.RegularExpressions;
using InstogramServer.Data;
using InstogramServer.Models;
using Microsoft.EntityFrameworkCore;

namespace InstogramServer.Services;

public class AutomodService(AppDbContext db)
{
    // Returns the first matched word, or null if clean
    public async Task<string?> CheckAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var words = await db.BannedWords.Select(w => w.Word).ToListAsync();
        var lower = text.ToLowerInvariant();
        foreach (var word in words)
        {
            if (Regex.IsMatch(lower, $@"\b{Regex.Escape(word)}\b"))
                return word;
        }
        return null;
    }

    public async Task FlagAsync(Guid authorId, string authorName,
        AutomodContentType type, Guid? contentId, string snippet, string matchedWord)
    {
        db.AutomodFlags.Add(new AutomodFlag
        {
            AuthorId    = authorId,
            AuthorName  = authorName,
            ContentType = type,
            ContentId   = contentId,
            Snippet     = snippet.Length > 200 ? snippet[..200] + "…" : snippet,
            MatchedWord = matchedWord
        });
        await db.SaveChangesAsync();
    }
}
