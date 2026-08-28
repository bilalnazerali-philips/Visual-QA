using VisualQa.Core;
using VisualQa.Configuration;
using Xunit;

namespace VisualQa.Core.Tests;

public sealed class TypographyAliasTests
{
    [Fact]
    public void Options_LoadsFontFamilyAliasesFromConfiguration()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"metadataAliases\":{\"enabled\":true,\"fontFamily\":{\"raw-wpf-font\":\"Figma Font\"}}}");

            VisualQaOptions options = VisualQaOptions.Load(path);

            Assert.True(options.MetadataAliases.Enabled);
            Assert.Equal("Figma Font", options.MetadataAliases.FontFamily["raw-wpf-font"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FontFamilyAlias_PassesButPreservesRawRuntimeEvidence()
    {
        const string rawWpfFont = "./NeueFrutigerWorld/#Neue Frutiger World";
        var design = new DesignComponentSpec
        {
            Elements = [new() { Id = "button-label", Text = "Button", FontFamily = "Neue Frutiger One" }],
        };
        var runtime = new RuntimeComponentSnapshot
        {
            Elements = [new() { Id = "button-label", Type = "TextBlock", Text = "Button", FontFamily = rawWpfFont }],
        };
        var aliases = new Dictionary<string,string>(StringComparer.Ordinal) { [rawWpfFont] = "Neue Frutiger One" };

        QaFinding finding = new TypographyValidator(aliases).Validate(design, runtime, new())
            .Single(finding => finding.Rule == "typography.fontFamily");

        Assert.Equal(QaStatus.Pass, finding.Status);
        Assert.Equal(rawWpfFont, finding.Actual);
        Assert.Contains("Normalized to 'Neue Frutiger One'", finding.Message);
    }

    [Fact]
    public void FontFamilyWithoutConfiguredAlias_RemainsAFailure()
    {
        var design = new DesignComponentSpec
        {
            Elements = [new() { Id = "button-label", Text = "Button", FontFamily = "Neue Frutiger One" }],
        };
        var runtime = new RuntimeComponentSnapshot
        {
            Elements = [new() { Id = "button-label", Type = "TextBlock", Text = "Button", FontFamily = "Unapproved Font" }],
        };

        QaFinding finding = new TypographyValidator().Validate(design, runtime, new())
            .Single(finding => finding.Rule == "typography.fontFamily");

        Assert.Equal(QaStatus.Fail, finding.Status);
    }
}
