using System.Text.Json;
using VisualQa.Core;
namespace VisualQa.Configuration;
public sealed class VisualQaOptions { public GeometryOptions Geometry { get; init; } = new(); public AlignmentOptions Alignment { get; init; } = new(); public VisualOptions Visual { get; init; } = new(); public ImageOnlyOptions ImageOnly { get; init; } = new(); public ColorOptions Color { get; init; } = new(); public RegionOptions Regions { get; init; } = new(); public bool WarningsBlock { get; init; } public bool RequireMetadata { get; init; } public QaTolerances Tolerances => new(Geometry.PositionTolerancePx,Geometry.SizeTolerancePx,Geometry.SpacingTolerancePx,Color.DeltaEPass,Color.DeltaEWarning); public static VisualQaOptions Load(string? path) => path is null?new():JsonSerializer.Deserialize<VisualQaOptions>(File.ReadAllText(path), JsonOptions.Value) ?? new(); }
public sealed class GeometryOptions { public double PositionTolerancePx { get; init; }=1; public double SizeTolerancePx { get; init; }=1; public double SpacingTolerancePx { get; init; }=1; }
public sealed class AlignmentOptions { public bool Enabled { get; init; }=true; public int MaxTranslationPx { get; init; }=10; }
public sealed class VisualOptions { public int PixelColorTolerance { get; init; }=8; public double SsimPass { get; init; }=.98; public double SsimWarning { get; init; }=.95; }
public sealed class ImageOnlyOptions { public double PassDifferentPixelPercentage { get; init; } = 0; public double WarningDifferentPixelPercentage { get; init; } = 1; public double SsimPass { get; init; } = .999; public double SsimWarning { get; init; } = .97; public bool AllowTranslationAlignment { get; init; } = false; }
public sealed class ColorOptions { public double DeltaEPass { get; init; }=1.5; public double DeltaEWarning { get; init; }=3; }
public sealed class RegionOptions { public int MinimumArea { get; init; }=12; public int MergeDistancePx { get; init; }=3; }
public static class JsonOptions { public static readonly JsonSerializerOptions Value=new(){PropertyNameCaseInsensitive=true,WriteIndented=true}; }
