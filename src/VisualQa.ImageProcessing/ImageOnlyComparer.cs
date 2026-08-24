using VisualQa.Core;

namespace VisualQa.ImageProcessing;

public sealed class ImageOnlyComparer
{
    public QaReport Compare(string referencePath, string actualPath, string outputDirectory, ImageComparisonOptions imageOptions, double passPixelPercentage, double warningPixelPercentage, double ssimPass, double ssimWarning, string component = "Image comparison")
    {
        var result = new PixelDiffEngine().Compare(referencePath, actualPath, outputDirectory, imageOptions);
        var status = result.DifferentPixelPercentage <= passPixelPercentage && result.Similarity >= ssimPass ? QaStatus.Pass : result.DifferentPixelPercentage <= warningPixelPercentage && result.Similarity >= ssimWarning ? QaStatus.Warning : QaStatus.Fail;
        var findings = new List<QaFinding>
        {
            new() { Rule = "pixel.difference", Status = status, Expected = $"≤ {warningPixelPercentage:0.###}%", Actual = $"{result.DifferentPixelPercentage:0.###}%", Delta = result.DifferentPixelPercentage, Message = $"{result.DifferentPixels}/{result.TotalPixels} pixels differ." },
            new() { Rule = "visual.ssim", Status = result.Similarity >= ssimPass ? QaStatus.Pass : result.Similarity >= ssimWarning ? QaStatus.Warning : QaStatus.Fail, Expected = ssimPass, Actual = result.Similarity, Message = $"Global structural similarity is {result.Similarity:P3}." },
            new() { Rule = "visual.regions", Status = result.Regions.Count == 0 ? QaStatus.Pass : QaStatus.Warning, Expected = 0, Actual = result.Regions.Count, Message = $"Detected {result.Regions.Count} significant mismatch region(s)." },
            new() { Rule = "alignment.translation", Status = QaStatus.NotEvaluated, Expected = "translation only", Actual = new { result.OffsetX, result.OffsetY, result.AlignmentConfidence }, Message = "No scaling, rotation, warping, or perspective correction is applied." }
        };
        return new() { Component = component, Platform = "image-only", Status = status, Similarity = result.Similarity, Findings = findings, Artifacts = Directory.EnumerateFiles(outputDirectory, "*.png").ToDictionary(x => Path.GetFileName(x)!, x => Path.GetFileName(x)!) };
    }
}
