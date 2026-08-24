using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VisualQa.Core;
using VisualQa.ImageProcessing;
using Xunit;

namespace VisualQa.Core.Tests;
public sealed class ImageOnlyComparerTests
{
    [Fact]
    public void Identical_images_pass_and_emit_artifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "visualqa-image-test-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var reference = Path.Combine(root, "reference.png"); using (var image = new Image<Rgba32>(20, 20, new Rgba32(255, 255, 255))) image.Save(reference);
        var report = new ImageOnlyComparer().Compare(reference, reference, Path.Combine(root, "result"), new(0, 1, false, 0), 0, 1, .999, .97);
        Assert.Equal(QaStatus.Pass, report.Status); Assert.True(File.Exists(Path.Combine(root, "result", "diff-regions.png")));
    }
    [Fact]
    public void Changed_image_fails_when_outside_warning_budget()
    {
        var root = Path.Combine(Path.GetTempPath(), "visualqa-image-test-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        var reference = Path.Combine(root, "reference.png"); var actual = Path.Combine(root, "actual.png"); using (var image = new Image<Rgba32>(40, 40, new Rgba32(255, 255, 255))) { image.Save(reference); for (var y = 0; y < 20; y++) for (var x = 0; x < 20; x++) image[x, y] = new Rgba32(255, 0, 0); image.Save(actual); }
        var report = new ImageOnlyComparer().Compare(reference, actual, Path.Combine(root, "result"), new(0, 1, false, 0), 0, 1, .999, .97);
        Assert.Equal(QaStatus.Fail, report.Status);
    }
}
