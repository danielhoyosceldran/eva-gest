using System.Security.Cryptography;
using System.Text;

namespace EvaGest.Services;

/// <summary>
/// Pure helpers for the owner's PIN and its paper recovery code: format rules, the
/// salted hash that is all the database ever keeps, and the code generator.
///
/// A stored secret is one self-describing string, "pbkdf2-sha256$iterations$salt$hash"
/// (salt and hash in base64), so a later change to the iteration count can still read
/// the secrets written before it.
/// </summary>
public static class OwnerPin
{
    public const int MinLength = 4;
    public const int MaxLength = 6;

    /// <summary>Iterations used in production. Tests pass a much smaller number so the
    /// suite stays fast; the stored string records whichever was used.</summary>
    public const int DefaultIterations = 100_000;

    private const string Scheme = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Recovery-code alphabet: no 0/O, 1/I/L or 5/S, which are easy to misread
    /// when copied by hand from a piece of paper.</summary>
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRTUVWXYZ2346789";
    private const int CodeGroups = 3;
    private const int CodeGroupLength = 4;

    /// <summary>4 to 6 plain digits. char.IsAsciiDigit rather than char.IsDigit, which
    /// also accepts Arabic-Indic and other Unicode digits a keyboard could produce.</summary>
    public static bool IsValid(string? pin)
        => pin is { Length: >= MinLength and <= MaxLength } && pin.All(char.IsAsciiDigit);

    /// <summary>Hashes a secret with a fresh random salt, ready to store.</summary>
    public static string Hash(string secret, int iterations = DefaultIterations)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Derive(secret, salt, iterations);
        return $"{Scheme}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// True when <paramref name="secret"/> is the one <paramref name="stored"/> was made
    /// from. A malformed stored value is simply "no match": it can only come from a
    /// hand-edited database, and failing closed is the safe answer there.
    /// </summary>
    public static bool Verify(string secret, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;

        string[] parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme) return false;
        if (!int.TryParse(parts[1], out int iterations) || iterations <= 0) return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Derive(secret, salt, iterations);
            // Constant-time comparison, so the time taken does not leak how many
            // leading bytes matched.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>A new recovery code, e.g. "K7QM-2XPA-9TRD": 12 characters from a
    /// 29-letter alphabet, about 58 bits — far beyond guessing within the lockout.</summary>
    public static string NewRecoveryCode()
    {
        var code = new StringBuilder();
        for (int group = 0; group < CodeGroups; group++)
        {
            if (group > 0) code.Append('-');
            for (int i = 0; i < CodeGroupLength; i++)
                code.Append(CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]);
        }
        return code.ToString();
    }

    /// <summary>
    /// What a recovery code is hashed and compared as: upper case, with the dashes and
    /// spaces dropped. The user copies it back from paper, so "k7qm 2xpa 9trd" has to be
    /// accepted just like the "K7QM-2XPA-9TRD" that was shown.
    /// </summary>
    public static string NormaliseRecoveryCode(string? code)
        => new((code ?? string.Empty)
            .Where(c => c is not ('-' or ' '))
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static byte[] Derive(string secret, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
