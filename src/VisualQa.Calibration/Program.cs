using System.Text.Json;
using System.IO;
using System.Windows;
using VisualQa.Configuration;
using VisualQa.Core;
using VisualQa.ImageProcessing;
using VisualQa.Reporting;
using VisualQa.WpfCapture;

namespace VisualQa.Calibration;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var manifestPath = Value(args, "--manifest") ?? "calibration/manifest.json"; var output = Value(args, "--output") ?? "calibration-results"; var config = Value(args, "--config");
        try
        {
            var manifest = CalibrationManifest.Load(manifestPath); if (manifest.SchemaVersion != 1) throw new InvalidDataException("Unsupported calibration manifest schema.");
            var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!; var baseline = Path.Combine(root, "baseline.png");
            Directory.CreateDirectory(output);
            if (!File.Exists(baseline) || args.Contains("--refresh-baseline", StringComparer.OrdinalIgnoreCase)) new WpfScreenshotRenderer().Render(CalibrationControlFactory.Create("exact"), baseline, manifest.Width, manifest.Height, manifest.Dpi);
            var options = VisualQaOptions.Load(config); var caseResults = new List<CalibrationCaseResult>();
            foreach (var item in manifest.Cases)
            {
                var caseDirectory = Path.Combine(output, item.Id); Directory.CreateDirectory(caseDirectory); var actual = Path.Combine(caseDirectory, "actual.png");
                new WpfScreenshotRenderer().Render(CalibrationControlFactory.Create(item.Variant), actual, manifest.Width, manifest.Height, manifest.Dpi);
                var imageOptions = new ImageComparisonOptions(options.Visual.PixelColorTolerance, options.Regions.MinimumArea, options.ImageOnly.AllowTranslationAlignment, options.Alignment.MaxTranslationPx);
                var report = new ImageOnlyComparer().Compare(baseline, actual, caseDirectory, imageOptions, options.ImageOnly.PassDifferentPixelPercentage, options.ImageOnly.WarningDifferentPixelPercentage, options.ImageOnly.SsimPass, options.ImageOnly.SsimWarning, item.Title);
                new JsonReportWriter().Write(report, Path.Combine(caseDirectory, "report.json")); new HtmlReportWriter().Write(report, Path.Combine(caseDirectory, "report.html"));
                var regions = Convert.ToInt32(report.Findings.First(x => x.Rule == "visual.regions").Actual);
                caseResults.Add(new() { Id = item.Id, Expected = item.ExpectedStatus, Actual = report.Status, Regions = regions, Passed = report.Status == item.ExpectedStatus && (!item.RequiresDiffRegion || regions > 0) });
            }
            var summary = Metrics(caseResults); File.WriteAllText(Path.Combine(output, "calibration-summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(output, "calibration-summary.html"), $"<h1>Visual QA calibration</h1><p>Pass: {caseResults.Count(x => x.Passed)}/{caseResults.Count}</p><pre>{System.Net.WebUtility.HtmlEncode(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }))}</pre>");
            return caseResults.All(x => x.Passed) ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
    }
    private static CalibrationSummary Metrics(IReadOnlyList<CalibrationCaseResult> results)
    { var tp = results.Count(x => x.Expected == QaStatus.Fail && x.Actual == QaStatus.Fail); var fp = results.Count(x => x.Expected != QaStatus.Fail && x.Actual == QaStatus.Fail); var fn = results.Count(x => x.Expected == QaStatus.Fail && x.Actual != QaStatus.Fail); var tn = results.Count(x => x.Expected != QaStatus.Fail && x.Actual != QaStatus.Fail); return new() { Cases = results, TruePositives = tp, FalsePositives = fp, FalseNegatives = fn, TrueNegatives = tn, Precision = tp + fp == 0 ? 1 : tp / (double)(tp + fp), Recall = tp + fn == 0 ? 1 : tp / (double)(tp + fn), FalsePositiveRate = fp + tn == 0 ? 0 : fp / (double)(fp + tn) }; }
    private static string? Value(string[] args, string option) { var index = Array.IndexOf(args, option); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
}
