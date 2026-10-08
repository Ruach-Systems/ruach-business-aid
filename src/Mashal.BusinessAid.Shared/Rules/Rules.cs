using System.Text.RegularExpressions;

namespace Mashal.BusinessAid.Shared;
public sealed class DomainException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public static class Rules
{
    public static void CheckVersion(Entity? current, string? expected)
    {
        if (current is null)
        {
            if (expected is not null)
                throw new DomainException("conflict", "The record no longer exists.", 409);
            return;
        }

        if (current.Version is null || expected != Convert.ToBase64String(current.Version))
            throw new DomainException("conflict", "This record changed on another device. Review the server version before retrying.", 409);
    }

    public static string Name(string? value)
    {
        var name = Regex.Replace((value ?? "").Normalize(System.Text.NormalizationForm.FormKC).Trim(), @"\s+", " ");
        Require(name.Length is> 0 and <= 160, "Name must contain 1–160 characters.");
        return name;
    }

    // Bounds keep every quantity × amount product, and sale totals, within bigint centavos.
    public const long MaxQuantity = 1_000_000;
    public const long MaxCentavos = 10_000_000_000;
    public const int MaxSaleLines = 500;
    public const int MaxNoteLength = 400;
    public const int MaxReasonLength = 80;

    public static void Quantity(long value, long minimum, string message) =>
        Require(value >= minimum && value <= MaxQuantity, $"{message} Use at most {MaxQuantity:N0}.");

    public static void Centavos(long value, long minimum, string message) =>
        Require(value >= minimum && value <= MaxCentavos, $"{message} Use at most ₱{MaxCentavos / 100:N0}.");

    public static void Note(string? value) =>
        Require((value?.Length ?? 0) <= MaxNoteLength, $"Notes can contain at most {MaxNoteLength} characters.");

    public static void Require(bool valid, string message)
    {
        if (!valid)
            throw new DomainException("validation", message);
    }

    public static long Round(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
}
