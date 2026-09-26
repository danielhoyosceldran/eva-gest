using System.Globalization;
using System.Text;
using EvaGest.Models;

namespace EvaGest.Helpers;

/// <summary>
/// The matching behind the client picker, kept as pure statics so it can be tested
/// without a UI thread.
///
/// Every word typed must appear somewhere in the client: in the name (anywhere, not
/// just at the start, ignoring case and accents, so "marti" finds "Martí") or, for a
/// word made of digits, in the mobile number. "joan 678" therefore narrows two Joans
/// down to the one whose phone contains 678.
/// </summary>
public static class ClientSearch
{
    /// <summary>How many matches the dropdown shows. Enough to spot the right person
    /// among namesakes; more is a list to read, not a pick.</summary>
    public const int MaxResults = 8;

    /// <summary>
    /// The clients matching <paramref name="query"/>, best first: a name that starts
    /// with what was typed, then one with a word starting with it, then any other
    /// match; ties alphabetically. An empty query returns the first clients
    /// alphabetically, so pressing Down on an empty box still offers something.
    /// </summary>
    public static List<Client> Find(IEnumerable<Client> clients, string? query, int max = MaxResults)
    {
        string[] words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string joined = string.Join(' ', words);

        return clients
            .Select(c => (client: c, name: Normalize(c.Name)))
            .Where(x => words.All(w => MatchesWord(x.client, x.name, w)))
            .OrderBy(x => Rank(x.name, joined))
            .ThenBy(x => x.name, StringComparer.Ordinal)
            .Take(max)
            .Select(x => x.client)
            .ToList();
    }

    /// <summary>Lower case, accents stripped, whitespace collapsed to single spaces.
    /// Both the query and every name go through this, so they compare like with like.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // FormD splits "í" into "i" plus a combining accent; dropping the combining
        // marks leaves the bare letter. "ç" and "ñ" become "c" and "n" the same way.
        var builder = new StringBuilder(text.Length);
        bool lastWasSpace = false;
        foreach (char ch in text.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
                continue;
            }
            builder.Append(char.ToLowerInvariant(ch));
            lastWasSpace = false;
        }
        return builder.ToString();
    }

    private static bool MatchesWord(Client client, string normalizedName, string word)
    {
        if (normalizedName.Contains(word, StringComparison.Ordinal)) return true;

        // Phones are stored however they were typed ("612 34 56 78"), so both sides are
        // compared as bare digits.
        return word.All(char.IsDigit) && Digits(client.Mobile).Contains(word, StringComparison.Ordinal);
    }

    /// <summary>0 = the name starts with the query, 1 = some word of it does, 2 = the
    /// query only appears inside a word or in the phone.</summary>
    private static int Rank(string normalizedName, string query)
    {
        if (query.Length == 0 || normalizedName.StartsWith(query, StringComparison.Ordinal)) return 0;
        if (normalizedName.Contains(' ' + query, StringComparison.Ordinal)) return 1;
        return 2;
    }

    private static string Digits(string? text)
        => text is null ? string.Empty : new string(text.Where(char.IsDigit).ToArray());
}
