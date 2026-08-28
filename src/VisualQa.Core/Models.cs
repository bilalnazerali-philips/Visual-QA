using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisualQa.Core;

public static class ArtifactSchema { public const int CurrentVersion = 1; public static void Require(int version) { if (version != CurrentVersion) throw new InvalidDataException($"Unsupported schemaVersion '{version}'. Expected {CurrentVersion}."); } }
public enum QaStatus { Pass, Warning, Fail, NotEvaluated, Error }
public sealed record RectSpec(double X, double Y, double Width, double Height) { public double Right => X + Width; public double Bottom => Y + Height; }
public sealed record ThicknessSpec(double Top, double Right, double Bottom, double Left) { public static readonly ThicknessSpec Zero = new(0,0,0,0); }
public sealed record CornerRadiusSpec(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
 public static CornerRadiusSpec Uniform(double value) => new(value, value, value, value);
}
public sealed class CornerRadiusSpecJsonConverter : JsonConverter<CornerRadiusSpec>
{
 public override CornerRadiusSpec? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
 {
  if (reader.TokenType == JsonTokenType.Number) return CornerRadiusSpec.Uniform(reader.GetDouble());
  if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("cornerRadius must be a number or four-corner object.");
  using var document=JsonDocument.ParseValue(ref reader); var root=document.RootElement;
  double Value(string name) => root.TryGetProperty(name,out var value)&&value.TryGetDouble(out var result) ? result : throw new JsonException($"cornerRadius.{name} is required.");
  return new(Value("topLeft"),Value("topRight"),Value("bottomRight"),Value("bottomLeft"));
 }
 public override void Write(Utf8JsonWriter writer, CornerRadiusSpec value, JsonSerializerOptions options)
 {
  writer.WriteStartObject(); writer.WriteNumber("topLeft",value.TopLeft); writer.WriteNumber("topRight",value.TopRight); writer.WriteNumber("bottomRight",value.BottomRight); writer.WriteNumber("bottomLeft",value.BottomLeft); writer.WriteEndObject();
 }
}
public sealed record EnvironmentInfo(string Os, string Runtime, double? ScaleFactor = null, string? Renderer = null, IReadOnlyList<string>? Fonts = null);
public sealed class DesignComponentSpec { public int SchemaVersion { get; init; } = ArtifactSchema.CurrentVersion; public string Name { get; init; } = ""; public double? Width { get; init; } public double? Height { get; init; } public IReadOnlyList<DesignElementSpec> Elements { get; init; } = []; }
public sealed class DesignElementSpec { public string Id { get; init; } = ""; public string Name { get; init; } = ""; public string? Type { get; init; } public string? Role { get; init; } public RectSpec? Bounds { get; init; } public string? Text { get; init; } public string? FontFamily { get; init; } public double? FontSize { get; init; } public double? LineHeight { get; init; } public double? LetterSpacing { get; init; } public string? FontWeight { get; init; } public string? ForegroundColor { get; init; } public string? BackgroundColor { get; init; } public string? BorderColor { get; init; } public ThicknessSpec? BorderThickness { get; init; } public ThicknessSpec? Padding { get; init; } [JsonConverter(typeof(CornerRadiusSpecJsonConverter))] public CornerRadiusSpec? CornerRadius { get; init; } public double? Opacity { get; init; } public string? TextAlignment { get; init; } public string? FlowDirection { get; init; } public string? TextWrapping { get; init; } public string? TextTrimming { get; init; } public bool? IsClipped { get; init; } public bool? IsVisible { get; init; } public string? IconName { get; init; } public Dictionary<string,double> NumericTokens { get; init; } = []; public Dictionary<string,string> SemanticTokens { get; init; } = []; }
public sealed class RuntimeComponentSnapshot { public int SchemaVersion { get; init; } = ArtifactSchema.CurrentVersion; public string Name { get; init; } = ""; public string Platform { get; init; } = ""; public double Width { get; init; } public double Height { get; init; } public double DpiX { get; init; } = 96; public double DpiY { get; init; } = 96; public EnvironmentInfo? Environment { get; init; } public IReadOnlyList<RuntimeElementSnapshot> Elements { get; init; } = []; }
public sealed class RuntimeElementSnapshot { public string Id { get; init; } = ""; public string Type { get; init; } = ""; public string? Role { get; init; } public RectSpec Bounds { get; init; } = new(0,0,0,0); public string? Text { get; init; } public string? FontFamily { get; init; } public double? FontSize { get; init; } public double? LineHeight { get; init; } public double? LetterSpacing { get; init; } public string? FontWeight { get; init; } public string? ForegroundColor { get; init; } public string? BackgroundColor { get; init; } public string? BorderColor { get; init; } public ThicknessSpec? BorderThickness { get; init; } [JsonConverter(typeof(CornerRadiusSpecJsonConverter))] public CornerRadiusSpec? CornerRadius { get; init; } public double? Opacity { get; init; } public string? HorizontalAlignment { get; init; } public string? VerticalAlignment { get; init; } public string? FlowDirection { get; init; } public string? TextAlignment { get; init; } public string? TextWrapping { get; init; } public string? TextTrimming { get; init; } public bool? IsClipped { get; init; } public string? IconName { get; init; } public ThicknessSpec Margin { get; init; } = ThicknessSpec.Zero; public ThicknessSpec Padding { get; init; } = ThicknessSpec.Zero; public bool IsVisible { get; init; } = true; }
public sealed class QaFinding { public string Rule { get; init; } = ""; public QaStatus Status { get; init; } public string? ElementId { get; init; } public object? Expected { get; init; } public object? Actual { get; init; } public double? Delta { get; init; } public string Message { get; init; } = ""; }
public sealed class QaReport { public string Component { get; init; } = ""; public string Platform { get; init; } = ""; public QaStatus Status { get; init; } public double? Similarity { get; init; } public IReadOnlyList<QaFinding> Findings { get; init; } = []; public Dictionary<string,string> Artifacts { get; init; } = []; }
