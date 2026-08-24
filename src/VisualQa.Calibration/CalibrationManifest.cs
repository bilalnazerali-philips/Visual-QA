using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using VisualQa.Core;
namespace VisualQa.Calibration;
public sealed class CalibrationManifest { public int SchemaVersion { get; init; } = 1; public int Width { get; init; } = 360; public int Height { get; init; } = 180; public double Dpi { get; init; } = 96; public string Environment { get; init; } = "Windows / WPF / Segoe UI"; public IReadOnlyList<CalibrationCase> Cases { get; init; } = []; public static CalibrationManifest Load(string path) { var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; options.Converters.Add(new JsonStringEnumConverter()); return JsonSerializer.Deserialize<CalibrationManifest>(File.ReadAllText(path), options) ?? throw new InvalidDataException("Invalid calibration manifest."); } }
public sealed class CalibrationCase { public string Id { get; init; } = ""; public string Title { get; init; } = ""; public QaStatus ExpectedStatus { get; init; } public bool RequiresDiffRegion { get; init; } public string Variant { get; init; } = ""; }
public sealed class CalibrationSummary { public int SchemaVersion { get; init; } = 1; public int TruePositives { get; init; } public int FalsePositives { get; init; } public int FalseNegatives { get; init; } public int TrueNegatives { get; init; } public double Precision { get; init; } public double Recall { get; init; } public double FalsePositiveRate { get; init; } public IReadOnlyList<CalibrationCaseResult> Cases { get; init; } = []; }
public sealed class CalibrationCaseResult { public string Id { get; init; } = ""; public QaStatus Expected { get; init; } public QaStatus Actual { get; init; } public bool Passed { get; init; } public int Regions { get; init; } }
