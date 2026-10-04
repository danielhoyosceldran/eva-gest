using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// Block H — guards over the XAML source itself, for mistakes a running view cannot
/// report. A TextBlock bound to the wrong property still renders; it just renders a wrong
/// number, silently, and only a person reading the screen notices.
/// </summary>
public class BindingGuardTests
{
    /// <summary>Walks up from the test binaries to the folder holding the solution file.
    /// Fails loudly rather than skipping: a guard that quietly finds no files to check
    /// is worse than no guard.</summary>
    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("EvaGest.slnx").Any())
            directory = directory.Parent;

        directory.Should().NotBeNull("the tests must be able to find the XAML sources to scan");
        return directory!;
    }

    private static List<FileInfo> ViewFiles()
    {
        var views = new DirectoryInfo(Path.Combine(RepositoryRoot().FullName, "EvaGest"));
        var files = views.EnumerateFiles("*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        files.Should().NotBeEmpty("there are XAML files to scan");
        return files;
    }

    [Fact]
    public void No_view_binds_a_raw_cents_or_basis_points_property()
    {
        // Cents and basis points are storage units. Bound straight to a TextBlock they
        // print as themselves: a 15,00 € line showed "1500", and with a {0:0.00} format
        // string it showed "1500,00" — the same error with a decimal point on it. Every
        // amount reaches the screen through Money.Format and every rate through
        // Percentages.Format, which means the ViewModel exposes a *Text property.
        //
        // Ends-with is what matters: CategoryText and AmountText are fine, AmountCents is not.
        var offending = new List<string>();
        var binding = new Regex(@"\{Binding\s+(?:Path=)?(?<path>[A-Za-z0-9_.]+)", RegexOptions.Compiled);

        foreach (var file in ViewFiles())
        {
            string[] lines = File.ReadAllLines(file.FullName);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match match in binding.Matches(lines[i]))
                {
                    string leaf = match.Groups["path"].Value.Split('.')[^1];
                    if (leaf.EndsWith("Cents", StringComparison.Ordinal)
                        || leaf.EndsWith("Bp", StringComparison.Ordinal))
                        offending.Add($"{file.Name}:{i + 1} binds {match.Groups["path"].Value}");
                }
            }
        }

        offending.Should().BeEmpty(
            "amounts and VAT rates must be formatted in the ViewModel, not bound raw");
    }

    [Fact]
    public void No_view_prints_a_bare_date_or_time()
    {
        // A DateOnly or TimeOnly bound with no format goes through WPF's own converter,
        // which formats with the binding culture - en-US before App set
        // FrameworkElement.Language - so the client history read 9/26/2026 (month first)
        // and the Start page's appointments read 10:00 AM in a Catalan interface. Even
        // with the right culture the default pattern varies, so every view states one.
        //
        // Every {Binding ...} is read whole, nested braces included, so extra arguments
        // (Mode=OneWay, Path=..., UpdateSourceTrigger=...) cannot hide a bare one. Its path
        // counts as a date or time when the last segment ends in Date, Time or At
        // (StartsAt); it passes only with a StringFormat or a Converter.
        var offending = new List<string>();
        var dateLike = new Regex(@"(?:Date|Time|At)$", RegexOptions.Compiled);

        foreach (var file in ViewFiles())
        {
            string[] lines = File.ReadAllLines(file.FullName);
            for (int i = 0; i < lines.Length; i++)
                foreach (string binding in Bindings(lines[i]))
                {
                    string? path = BindingPath(binding);
                    if (path is null || !dateLike.IsMatch(path.Split('.')[^1])) continue;
                    if (binding.Contains("StringFormat=") || binding.Contains("Converter=")) continue;
                    offending.Add($"{file.Name}:{i + 1} binds {path} with no format");
                }
        }

        offending.Should().BeEmpty(
            "dates and times reach the screen through an explicit format, never WPF's default");
    }

    /// <summary>Every "{Binding ...}" markup extension on a line, from its opening brace to
    /// the brace that closes it, so StringFormat={}{0:...} and Converter={...} stay inside.</summary>
    private static IEnumerable<string> Bindings(string line)
    {
        int start = 0;
        while ((start = line.IndexOf("{Binding", start, StringComparison.Ordinal)) >= 0)
        {
            int depth = 0, end = start;
            for (; end < line.Length; end++)
            {
                if (line[end] == '{') depth++;
                else if (line[end] == '}' && --depth == 0) break;
            }
            yield return line[start..Math.Min(end + 1, line.Length)];
            start = end;
        }
    }

    /// <summary>The binding's path: "Path=X" wherever it sits, else the first positional
    /// argument. Null for a binding to the DataContext itself.</summary>
    private static string? BindingPath(string binding)
    {
        var named = Regex.Match(binding, @"Path=(?<p>[A-Za-z0-9_.]+)");
        if (named.Success) return named.Groups["p"].Value;

        var positional = Regex.Match(binding, @"^\{Binding\s+(?<p>[A-Za-z0-9_.]+)\s*[,}]");
        return positional.Success ? positional.Groups["p"].Value : null;
    }

    [Fact]
    public void No_view_formats_a_number_with_a_hardcoded_currency_symbol()
    {
        // The symbol comes from AppLanguage.Culture through Money.Format. Typed into XAML
        // it would survive a language change and could end up on the wrong side of the
        // number for a culture that puts it in front.
        var offending = new List<string>();

        foreach (var file in ViewFiles())
        {
            string[] lines = File.ReadAllLines(file.FullName);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains("StringFormat") && lines[i].Contains('€'))
                    offending.Add($"{file.Name}:{i + 1}");
        }

        offending.Should().BeEmpty("the currency symbol belongs to the culture, not to the view");
    }
}
