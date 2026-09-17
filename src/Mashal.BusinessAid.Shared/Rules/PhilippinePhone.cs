using System.Text.RegularExpressions;

namespace Mashal.BusinessAid.Shared;
public static partial class PhilippinePhone
{
    public static string Normalize(string? input)
    {
        var value = input?.Trim() ?? "";
        Rules.Require(value.Length <= 32 && !value.Any(c => !(c is >= '0' and <= '9' or '+' or ' ' or '-' or '(' or ')')), "Enter a Philippine mobile number using 09XXXXXXXXX or +639XXXXXXXXX.");
        value = value.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (value.StartsWith("09", StringComparison.Ordinal))
            value = "+63" + value[1..];
        else if (value.StartsWith("639", StringComparison.Ordinal))
            value = "+" + value;
        Rules.Require(Mobile().IsMatch(value), "Enter a Philippine mobile number: 09 followed by nine digits. Landlines are not supported.");
        return value;
    }

    [GeneratedRegex(@"\A\+639[0-9]{9}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Mobile();
}
