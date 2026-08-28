using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using VisualQa.Configuration;
using VisualQa.Core;
using VisualQa.Figma;
using VisualQa.ImageProcessing;
using VisualQa.Reporting;

if (args.Length > 0 && args[0] == "calibrate")
    return RunCalibration();
if (args.Length > 0 && args[0] == "import-figma")
    return await ImportFigmaAsync();
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
            // Checklist phase 1: compare only evidence present in both contracts; final pixels cover all visual layers.
            foreach (var validator in new IQaValidator[] { new ComponentGeometryValidator(), new StructuralValidator(), new GeometryValidator(), new SpacingValidator(), new TypographyValidator(options.MetadataAliases.Enabled ? options.MetadataAliases.FontFamily : null), new ColorValidator(), new AppearanceValidator(), new IconValidator() }) findings.AddRange(validator.Validate(design, runtime, options.Tolerances));
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
IReadOnlyList<string> Options(string name) => args
    .Select((value, index) => new { value, index })
    .Where(item => string.Equals(item.value, name, StringComparison.OrdinalIgnoreCase) && item.index + 1 < args.Length)
    .Select(item => args[item.index + 1])
    .ToArray();
static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException($"Cannot read {path}");
static int Usage() { Console.Error.WriteLine("Usage: visualqa compare|compare-all --design <directory> --output <directory> [--platform wpf|react] [--config file]\n       visualqa compare-images --reference <baseline.png> --actual <variant.png> --output <results> [--config file]\n       visualqa import-figma [--url <figma-link-to-selection>] [--url-file <urls.txt>] [--scenario <name>] [--output <design-directory>] [--id-map <map.json>] [--mapped-only] [--token-file <local-secret-file>] [--keep-raw]\n       visualqa calibrate [--manifest calibration/manifest.json] [--output calibration-results] [--config visualqa.json] (Windows only)\n\nimport-figma defaults to .visualqa/figma-token.txt and visual-tests/<derived-component-name>/design. --url-file accepts one Figma URL per line."); return 2; }

async Task<int> ImportFigmaAsync()
{
    try
    {
        var urlFile = Option("--url-file");
        var urls = FigmaImportUrlList.Load(Options("--url"), urlFile);
        if (urls.Count == 0) return Usage();
        var batchMode = urls.Count > 1 || !string.IsNullOrWhiteSpace(urlFile);
        if (batchMode && (Option("--scenario") is not null || Option("--output") is not null || Option("--id-map") is not null))
            throw new InvalidOperationException("--scenario, --output, and --id-map apply to one import only. Do not use them with --url-file or multiple --url values.");
        var tokenFile = Option("--token-file") ?? Path.Combine(Directory.GetCurrentDirectory(), ".visualqa", "figma-token.txt");
        if (!File.Exists(tokenFile)) throw new FileNotFoundException($"Figma token file was not found. Create '{tokenFile}' and place the Figma personal access token in it, or provide --token-file.", tokenFile);
        var token = File.ReadAllText(tokenFile).Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException($"Figma token file '{tokenFile}' is empty. Place a Figma personal access token in it.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var importer = new FigmaImportService(client);
        var outcomes = new List<FigmaImportBatchOutcome>();
        foreach (var url in urls)
        {
            try
            {
                var scenario = Option("--scenario") ?? await importer.SuggestFixtureNameAsync(url, token);
                var outputDirectory = Option("--output") ?? Path.Combine(Directory.GetCurrentDirectory(), "visual-tests", scenario, "design");
                EnsureNoFixtureCollision(outputDirectory, url);
                var result = await importer.ImportAsync(new FigmaImportOptions
                {
                    Url = url,
                    Scenario = scenario,
                    OutputDirectory = outputDirectory,
                    Token = token,
                    IdMapPath = Option("--id-map"),
                    MappedNodesOnly = args.Contains("--mapped-only", StringComparer.OrdinalIgnoreCase),
                    KeepRawResponse = args.Contains("--keep-raw", StringComparer.OrdinalIgnoreCase)
                });
                outcomes.Add(new(url, scenario, QaStatus.Pass, result.DesignPath, result.ReferenceImagePath, null));
                Console.WriteLine($"Imported {result.Design.Name} ({result.Design.Elements.Count} elements).");
                Console.WriteLine($"design: {result.DesignPath}");
                Console.WriteLine($"reference: {result.ReferenceImagePath}");
                Console.WriteLine($"source: {result.SourcePath}");
                if (result.RawResponsePath is not null) Console.WriteLine($"raw: {result.RawResponsePath}");
            }
            catch (Exception ex)
            {
                outcomes.Add(new(url, null, QaStatus.Fail, null, null, ex.Message));
                Console.Error.WriteLine($"Failed to import {url}: {ex.Message}");
            }
        }

        if (batchMode)
        {
            var summaryPath = Path.Combine(Directory.GetCurrentDirectory(), "visual-tests", "import-summary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
            await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(outcomes, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            }));
            Console.WriteLine($"summary: {Path.GetFullPath(summaryPath)}");
        }
        return outcomes.Any(outcome => outcome.Status == QaStatus.Fail) ? 1 : 0;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
}

void EnsureNoFixtureCollision(string outputDirectory, string url)
{
    var sourcePath = Path.Combine(outputDirectory, "figma-source.json");
    if (!File.Exists(sourcePath)) return;
    var existing = Read<FigmaImportSource>(sourcePath);
    var incoming = FigmaNodeReference.Parse(url);
    if (!string.Equals(existing.FileKey, incoming.FileKey, StringComparison.Ordinal) || !string.Equals(existing.NodeId, incoming.NodeId, StringComparison.Ordinal))
        throw new InvalidOperationException($"Default fixture folder '{outputDirectory}' already belongs to Figma node {existing.NodeId}. Supply a unique --scenario for a single import; batch mode will not overwrite it.");
}

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

sealed record FigmaImportBatchOutcome(
    string Url,
    string? Component,
    QaStatus Status,
    string? DesignPath,
    string? ReferenceImagePath,
    string? Error);
