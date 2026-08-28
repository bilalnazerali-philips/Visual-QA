using System.Text;
using System.Text.Json;
using VisualQa.Core;

namespace VisualQa.Figma;

public sealed class FigmaSpecParser
{
    public JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

/// <summary>Converts an official Figma node response into the versioned Visual QA design contract.</summary>
public sealed class FigmaNormalizer
{
    public DesignComponentSpec Normalize(JsonElement root, string? componentName = null, IReadOnlyDictionary<string, string>? idMap = null, bool mappedNodesOnly = false)
    {
        var node = SelectedNode(root);
        var rootBounds = Bounds(node) ?? throw new InvalidDataException("The selected Figma node has no absoluteBoundingBox.");
        var elements = new List<DesignElementSpec>();
        Walk(node, rootBounds, elements, idMap, new HashSet<string>(StringComparer.Ordinal), mappedNodesOnly);
        return new DesignComponentSpec
        {
            Name = componentName ?? Get(node, "name") ?? "FigmaComponent",
            Width = rootBounds.Width,
            Height = rootBounds.Height,
            Elements = elements
        };
    }

    private static JsonElement SelectedNode(JsonElement root)
    {
        if (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in nodes.EnumerateObject())
                if (entry.Value.TryGetProperty("document", out var document)) return document;
        }
        return root.TryGetProperty("document", out var directDocument) ? directDocument : root;
    }

    private void Walk(JsonElement node, RectSpec rootBounds, List<DesignElementSpec> output, IReadOnlyDictionary<string, string>? idMap, HashSet<string> usedIds, bool mappedNodesOnly)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        var figmaId = Get(node, "id");
        var name = Get(node, "name");
        var type = Get(node, "type");
        if (figmaId is not null && (!mappedNodesOnly || (idMap?.ContainsKey(figmaId) ?? false)))
        {
            var qaId = UniqueQaId(figmaId, name, idMap, usedIds);
            var bounds = Bounds(node);
            output.Add(new DesignElementSpec
            {
                Id = qaId,
                Name = name ?? figmaId,
                Type = type,
                Role = type == "TEXT" ? "text" : null,
                Bounds = bounds is null ? null : new RectSpec(bounds.X - rootBounds.X, bounds.Y - rootBounds.Y, bounds.Width, bounds.Height),
                Text = Get(node, "characters"),
                FontFamily = Nested(node, "style", "fontFamily"),
                FontSize = Number(node, "style", "fontSize"),
                FontWeight = StringOrNumber(node, "style", "fontWeight"),
                LineHeight = Number(node, "style", "lineHeightPx"),
                LetterSpacing = Number(node, "style", "letterSpacing"),
                ForegroundColor = type == "TEXT" ? Paint(node, "fills") : null,
                BackgroundColor = type == "TEXT" ? null : Paint(node, "fills") ?? Paint(node, "backgrounds"),
                BorderColor = Paint(node, "strokes"),
                BorderThickness = Paint(node, "strokes") is null ? null : Uniform(Number(node, "strokeWeight")),
                Padding = Padding(node),
                CornerRadius = Number(node, "cornerRadius") is { } radius ? CornerRadiusSpec.Uniform(radius) : null,
                Opacity = Number(node, "opacity"),
                TextAlignment = Nested(node, "style", "textAlignHorizontal"),
                IsVisible = Bool(node, "visible") ?? true,
                IconName = IconName(type, name)
            });
        }
        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray()) Walk(child, rootBounds, output, idMap, usedIds, mappedNodesOnly);
    }

    private static string UniqueQaId(string figmaId, string? name, IReadOnlyDictionary<string, string>? idMap, HashSet<string> usedIds)
    {
        string? mapped = null;
        var isMapped = idMap is not null && idMap.TryGetValue(figmaId, out mapped) && !string.IsNullOrWhiteSpace(mapped);
        var candidate = isMapped ? mapped! : Slug(name ?? figmaId);
        if (usedIds.Add(candidate)) return candidate;
        if (isMapped) throw new InvalidDataException($"Figma ID map assigns duplicate Visual QA ID '{candidate}'.");

        var unique = $"{candidate}--{Slug(figmaId.Replace(':', '-'))}";
        var suffix = 2;
        while (!usedIds.Add(unique)) unique = $"{candidate}--{Slug(figmaId.Replace(':', '-'))}-{suffix++}";
        return unique;
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        var dashPending = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (dashPending && builder.Length > 0) builder.Append('-');
                builder.Append(character);
                dashPending = false;
            }
            else dashPending = true;
        }
        return builder.Length == 0 ? "figma-element" : builder.ToString();
    }

    private static string? IconName(string? type, string? name) =>
        type is "INSTANCE" or "COMPONENT" && name is not null &&
        (name.Contains("icon", StringComparison.OrdinalIgnoreCase) || name.Contains("arrow", StringComparison.OrdinalIgnoreCase)) ? name : null;

    private static string? Get(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? Nested(JsonElement element, string parent, string name) => element.TryGetProperty(parent, out var value) ? Get(value, name) : null;
    private static string? StringOrNumber(JsonElement element, string parent, string name) => element.TryGetProperty(parent, out var value) && value.TryGetProperty(name, out var result) ? result.ValueKind == JsonValueKind.String ? result.GetString() : result.ValueKind == JsonValueKind.Number ? result.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture) : null : null;
    private static double? Number(JsonElement element, string parent, string name) => element.TryGetProperty(parent, out var value) && value.TryGetProperty(name, out var number) && number.TryGetDouble(out var result) ? result : null;
    private static double? Number(JsonElement element, string name) => element.TryGetProperty(name, out var number) && number.TryGetDouble(out var result) ? result : null;
    private static ThicknessSpec? Uniform(double? value) => value is null ? null : new ThicknessSpec(value.Value, value.Value, value.Value, value.Value);
    private static ThicknessSpec? Padding(JsonElement element) { var top=Number(element,"paddingTop"); var right=Number(element,"paddingRight"); var bottom=Number(element,"paddingBottom"); var left=Number(element,"paddingLeft"); return top is null&&right is null&&bottom is null&&left is null?null:new ThicknessSpec(top??0,right??0,bottom??0,left??0); }
    private static bool? Bool(JsonElement element,string name) => element.TryGetProperty(name,out var value) && (value.ValueKind==JsonValueKind.True||value.ValueKind==JsonValueKind.False) ? value.GetBoolean() : null;
    private static RectSpec? Bounds(JsonElement element) => element.TryGetProperty("absoluteBoundingBox", out var bounds) && bounds.TryGetProperty("x", out var x) && bounds.TryGetProperty("y", out var y) && bounds.TryGetProperty("width", out var width) && bounds.TryGetProperty("height", out var height) ? new RectSpec(x.GetDouble(), y.GetDouble(), width.GetDouble(), height.GetDouble()) : null;

    private static string? Paint(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var paints) || paints.ValueKind != JsonValueKind.Array) return null;
        foreach (var paint in paints.EnumerateArray())
        {
            if (paint.TryGetProperty("visible", out var visible) && visible.ValueKind == JsonValueKind.False) continue;
            if (paint.TryGetProperty("color", out var color) && color.TryGetProperty("r", out var r) && color.TryGetProperty("g", out var g) && color.TryGetProperty("b", out var b))
                return $"#{ToByte(r.GetDouble()):X2}{ToByte(g.GetDouble()):X2}{ToByte(b.GetDouble()):X2}";
        }
        return null;
    }

    private static int ToByte(double channel) => Math.Clamp((int)Math.Round(channel * 255), 0, 255);
}
