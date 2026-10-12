namespace EvaGest.Helpers;

/// <summary>
/// One entry of a search picker: the object it stands for, the text it is shown and
/// searched by, and an optional second text shown on the right of its row (a client's
/// phone, which tells namesakes apart).
/// </summary>
public sealed record PickerOption(object Value, string Label, string? Detail = null);

/// <summary>
/// The matching behind the search pickers (workers, services, products, categories…),
/// kept as pure statics so it can be tested without a UI thread. Same rules as
/// <see cref="ClientSearch"/>: every word typed must appear somewhere in the label,
/// ignoring case and accents, or — for a word made of digits — in the detail.
///
/// Unlike the client search, ties keep the order the options came in rather than going
/// alphabetical: some lists are ordered on purpose ("Totes" before the workers, services
/// and products before the expense categories), and an empty search shows them that way.
/// </summary>
public static class OptionSearch
{
    /// <summary>How many matches the dropdown shows, the same as the client box.</summary>
    public const int MaxResults = ClientSearch.MaxResults;

    /// <summary>The options matching <paramref name="query"/>: a label that starts with
    /// what was typed first, then one with a word starting with it, then any other match.
    /// An empty query returns the first options as given.</summary>
    public static List<PickerOption> Find(IEnumerable<PickerOption> options, string? query, int max = MaxResults)
    {
        string[] words = ClientSearch.Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string joined = string.Join(' ', words);

        // The index is the tie-breaker: OrderBy is stable, but saying it keeps it so.
        return options
            .Select((option, index) => (option, label: ClientSearch.Normalize(option.Label), index))
            .Where(x => words.All(w => MatchesWord(x.option, x.label, w)))
            .OrderBy(x => Rank(x.label, joined))
            .ThenBy(x => x.index)
            .Take(max)
            .Select(x => x.option)
            .ToList();
    }

    /// <summary>The option whose whole label is what was typed (ignoring case and
    /// accents), or null. Lets "marta" left in a box count as picking Marta.</summary>
    public static PickerOption? Exact(IEnumerable<PickerOption> options, string? text)
    {
        string wanted = ClientSearch.Normalize(text);
        if (wanted.Length == 0) return null;
        var hits = options.Where(o => ClientSearch.Normalize(o.Label) == wanted).Take(2).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    private static bool MatchesWord(PickerOption option, string normalizedLabel, string word)
    {
        if (normalizedLabel.Contains(word, StringComparison.Ordinal)) return true;

        // A detail such as a phone is stored however it was typed ("612 34 56 78"), so
        // both sides are compared as bare digits.
        return word.All(char.IsDigit) && Digits(option.Detail).Contains(word, StringComparison.Ordinal);
    }

    /// <summary>0 = the label starts with the query, 1 = some word of it does, 2 = the
    /// query only appears inside a word or in the detail.</summary>
    private static int Rank(string normalizedLabel, string query)
    {
        if (query.Length == 0 || normalizedLabel.StartsWith(query, StringComparison.Ordinal)) return 0;
        if (normalizedLabel.Contains(' ' + query, StringComparison.Ordinal)) return 1;
        return 2;
    }

    private static string Digits(string? text)
        => text is null ? string.Empty : new string(text.Where(char.IsDigit).ToArray());
}
