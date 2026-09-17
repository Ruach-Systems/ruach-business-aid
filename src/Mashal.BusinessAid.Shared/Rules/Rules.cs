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

    public static void Require(bool valid, string message)
    {
        if (!valid)
            throw new DomainException("validation", message);
    }

    public static long Round(decimal value) => checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));
    public static long Average(decimal quantity, long cost, decimal received, long purchase) => quantity + received <= 0 ? 0 : Round((Math.Max(0, quantity) * cost + purchase) / (Math.Max(0, quantity) + received));
}
