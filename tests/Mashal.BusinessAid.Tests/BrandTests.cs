using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Mashal.Tests;

public class BrandTests
{
    private static string Root()
    {
        var path = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(path, "Mashal.BusinessAid.slnx")))
            path = Directory.GetParent(path)?.FullName ?? throw new InvalidOperationException("Solution root not found.");
        return path;
    }

    [Fact]
    public void IdentityAndAllRuntimeReferencesAreConsistentAndContentHashed()
    {
        var root = Root();
        var web = Path.Combine(root, "src", "Mashal.BusinessAid.Client", "wwwroot");
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(web, "manifest.webmanifest"))).RootElement;
        Assert.Equal("Business Aid by RUACH", manifest.GetProperty("name").GetString());
        Assert.Equal("Business Aid", manifest.GetProperty("short_name").GetString());
        Assert.Equal("/", manifest.GetProperty("id").GetString());
        Assert.Equal("/", manifest.GetProperty("start_url").GetString());
        Assert.Equal("#141821", manifest.GetProperty("theme_color").GetString());
        var references = new[]
        {
            "index.html", "open.html", "manifest.webmanifest", Path.Combine("css", "app.css")
        }.Select(p => File.ReadAllText(Path.Combine(web, p)))
            .Append(File.ReadAllText(Path.Combine(root, "src", "Mashal.BusinessAid.Client", "Components", "BrandLogo.razor")));
        foreach (var reference in references.SelectMany(text => Regex.Matches(text, """/brand-assets/[^"'\s)]+""").Select(m => m.Value)))
        {
            var file = Path.Combine(web, reference.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(file), reference);
            var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))[..16];
            Assert.Contains("." + hash + ".", Path.GetFileName(file));
            Assert.DoesNotContain("mashal-", reference);
        }
        Assert.Contains("Business Aid by RUACH", File.ReadAllText(Path.Combine(web, "index.html")));
        Assert.Contains("Business Aid by RUACH", File.ReadAllText(Path.Combine(root, "src", "Mashal.BusinessAid.Client", "Components", "BrandLogo.razor")));
    }

    [Fact]
    public void ExportInventoryMatchesDistributedBytes()
    {
        var brand = Path.Combine(Root(), "public", "brand");
        var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(brand, "asset-inventory.json")));
        foreach (var asset in inventory.RootElement.EnumerateArray())
        {
            var bytes = File.ReadAllBytes(Path.Combine(brand, asset.GetProperty("file").GetString()!));
            Assert.Equal(asset.GetProperty("bytes").GetInt64(), bytes.LongLength);
            Assert.Equal(asset.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }

    [Fact]
    public void ExportedLogosUseOutlinedTextAndSingleMasterGeometry()
    {
        var root = Root();
        XNamespace svg = "http://www.w3.org/2000/svg";
        var master = XDocument.Load(Path.Combine(root, "branding", "balanced-record-master.svg"))
            .Descendants(svg + "path").Select(p => p.Attribute("d")!.Value).ToArray();
        foreach (var name in new[] { "business-aid-wordmark.svg", "business-aid-wordmark-reversed.svg", "business-aid-wordmark-monochrome.svg", "business-aid-app-icon.svg", "favicon.svg" })
        {
            var document = XDocument.Load(Path.Combine(root, "public", "brand", name));
            Assert.Empty(document.Descendants(svg + "text"));
            var paths = document.Descendants(svg + "path").Select(p => p.Attribute("d")!.Value).ToArray();
            foreach (var path in master) Assert.Contains(path, paths);
        }
        var ico = File.ReadAllBytes(Path.Combine(root, "public", "brand", "favicon.ico"));
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
        Assert.Equal(3, BitConverter.ToUInt16(ico, 4));
        Assert.Equal(new byte[] { 16, 32, 48 }, new[] { ico[6], ico[22], ico[38] });
    }
}
