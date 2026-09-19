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
