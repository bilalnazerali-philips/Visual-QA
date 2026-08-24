using System.Text.Json;
using System.Diagnostics;
using VisualQa.Configuration;
using VisualQa.Core;
using VisualQa.ImageProcessing;
using VisualQa.Reporting;

if (args.Length > 0 && args[0] == "calibrate")
    return RunCalibration();
if (args.Length > 0 && args[0] == "compare-images")
    return CompareImages();
if (args.Length > 0 && args[0] == "seed-demo")
{
    var target = args.Length > 1 ? args[1] : "visual-tests"; DemoArtifactSeeder.SeedPatientInfo(target); Console.WriteLine($"Seeded deterministic PatientInfo image fixtures in {target}."); return 0;
}
if (args.Length == 0 || !new[] { "compare", "compare-all" }.Contains(args[0]))
    return Usage();
var designRoot = Option("--design");
var output = Option("--output");
if (designRoot is null || output is null) return Usage();
try
{
    var options = VisualQaOptions.Load(Option("--config"));
    var platforms = args[0] == "compare-all" ? new[] { "wpf", "react" } : new[] { Option("--platform") ?? throw new InvalidDataException("--platform is required.") };
    var reports = new List<QaReport>();
    foreach (var component in Directory.EnumerateDirectories(designRoot))
    {
        var designFile = Path.Combine(component, "design", "design.json");
        var reference = Path.Combine(component, "design", "reference.png");
        if (!File.Exists(designFile) || !File.Exists(reference)) continue;
        var design = Read<DesignComponentSpec>(designFile); ArtifactSchema.Require(design.SchemaVersion);
        foreach (var platform in platforms)
        {
            var capture = Path.Combine(component, platform);
            var runtimeFile = Path.Combine(capture, "runtime.json"); var actual = Path.Combine(capture, "actual.png");
            if (!File.Exists(runtimeFile) || !File.Exists(actual)) continue;
            var runtime = Read<RuntimeComponentSnapshot>(runtimeFile); ArtifactSchema.Require(runtime.SchemaVersion);
            var resultDirectory = Path.Combine(output, Path.GetFileName(component), platform);
            var findings = new List<QaFinding>();
            foreach (var validator in new IQaValidator[] { new StructuralValidator(), new GeometryValidator(), new SpacingValidator(), new TypographyValidator(), new IconValidator() }) findings.AddRange(validator.Validate(design, runtime, options.Tolerances));
            var image = new PixelDiffEngine().Compare(reference, actual, resultDirectory, options.Visual.PixelColorTolerance, options.Regions.MinimumArea, options.Alignment.MaxTranslationPx);
            findings.Add(new() { Rule = "pixel.difference", Status = image.DifferentPixels == 0 ? QaStatus.Pass : QaStatus.Warning, Expected = 0, Actual = image.DifferentPixels, Delta = image.DifferentPixels, Message = $"{image.DifferentPixels}/{image.TotalPixels} pixels differ." });
            findings.Add(new() { Rule = "perceptual.ssim", Status = image.Similarity >= options.Visual.SsimPass ? QaStatus.Pass : image.Similarity >= options.Visual.SsimWarning ? QaStatus.Warning : QaStatus.Fail, Expected = options.Visual.SsimPass, Actual = image.Similarity, Message = $"Structural similarity {image.Similarity:P2}." });
            var report = new QaReport { Component = design.Name, Platform = platform, Status = new QaEvaluator().Evaluate(findings, options.WarningsBlock), Similarity = image.Similarity, Findings = findings, Artifacts = Directory.EnumerateFiles(resultDirectory, "*.png").ToDictionary(x => Path.GetFileName(x)!, x => Path.GetFileName(x)!) };
            new JsonReportWriter().Write(report, Path.Combine(resultDirectory, "report.json")); new HtmlReportWriter().Write(report, Path.Combine(resultDirectory, "report.html")); reports.Add(report);
        }
    }
    Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
    return reports.Any(x => x.Status == QaStatus.Fail) ? 1 : 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }

string? Option(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException($"Cannot read {path}");
static int Usage() { Console.Error.WriteLine("Usage: visualqa compare|compare-all --design <directory> --output <directory> [--platform wpf|react] [--config file]\n       visualqa compare-images --reference <baseline.png> --actual <variant.png> --output <results> [--config file]\n       visualqa calibrate [--manifest calibration/manifest.json] [--output calibration-results] [--config visualqa.json] (Windows only)"); return 2; }

int CompareImages()
{
    var reference = Option("--reference"); var actual = Option("--actual"); var outputDirectory = Option("--output");
    if (reference is null || actual is null || outputDirectory is null) return Usage();
    try
    {
        if (!File.Exists(reference) || !File.Exists(actual)) throw new FileNotFoundException("Both --reference and --actual must name existing image files.");
        var options = VisualQaOptions.Load(Option("--config"));
        var imageOptions = new ImageComparisonOptions(options.Visual.PixelColorTolerance, options.Regions.MinimumArea, options.ImageOnly.AllowTranslationAlignment, options.Alignment.MaxTranslationPx);
        var report = new ImageOnlyComparer().Compare(reference, actual, outputDirectory, imageOptions, options.ImageOnly.PassDifferentPixelPercentage, options.ImageOnly.WarningDifferentPixelPercentage, options.ImageOnly.SsimPass, options.ImageOnly.SsimWarning, Path.GetFileNameWithoutExtension(actual));
        new JsonReportWriter().Write(report, Path.Combine(outputDirectory, "report.json")); new HtmlReportWriter().Write(report, Path.Combine(outputDirectory, "report.html"));
        return report.Status == QaStatus.Fail ? 1 : 0;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
}

int RunCalibration()
{
    if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Calibration rendering is only supported on Windows because it uses WPF."); return 2; }
    var project = Path.Combine(Directory.GetCurrentDirectory(), "src", "VisualQa.Calibration", "VisualQa.Calibration.csproj");
    if (!File.Exists(project)) { Console.Error.WriteLine("VisualQa.Calibration project was not found. Run this command from the repository root."); return 2; }
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    start.ArgumentList.Add("run"); start.ArgumentList.Add("--project"); start.ArgumentList.Add(project); start.ArgumentList.Add("--");
    foreach (var argument in args.Skip(1)) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the calibration runner."); process.WaitForExit(); return process.ExitCode;
}
