using VisualQa.Core;
using Xunit;

namespace VisualQa.Core.Tests;

public sealed class AppearanceValidatorTests
{
    [Fact]
    public void Compares_the_component_surface_when_figma_and_wpf_expose_it()
    {
        var design = new DesignComponentSpec
        {
            Width = 80,
            Height = 40,
            Elements = [new() { Id = "figma-root", Name = "Root", Type = "COMPONENT", Bounds = new(0, 0, 80, 40), BackgroundColor = "#0072DB" }]
        };
        var runtime = new RuntimeComponentSnapshot
        {
            Width = 80,
            Height = 40,
            Elements = [new() { Id = "button-root", Type = "Button", Bounds = new(0, 0, 80, 40), BackgroundColor = "#00438A" }]
        };

        var finding = Assert.Single(new AppearanceValidator().Validate(design, runtime, new()));

        Assert.Equal("color.background", finding.Rule);
        Assert.Equal(QaStatus.Fail, finding.Status);
        Assert.Equal("#0072DB", finding.Expected);
        Assert.Equal("#00438A", finding.Actual);
    }
}
