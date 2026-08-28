using System.Text.Json;
using System.Text.Json.Serialization;
using VisualQa.Core;

namespace VisualQa.Figma;

public sealed record FigmaNodeReference(string FileKey, string NodeId, string Url)
{
    public static FigmaNodeReference Parse(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Host.EndsWith("figma.com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--url must be a Figma design or file URL.", nameof(url));

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var anchor = Array.FindIndex(segments, segment => segment is "design" or "file");
        if (anchor < 0 || segments.Length <= anchor + 1 || string.IsNullOrWhiteSpace(segments[anchor + 1]))
            throw new ArgumentException("The Figma URL does not contain a file key.", nameof(url));

        var nodeId = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && string.Equals(Uri.UnescapeDataString(pair[0]), "node-id", StringComparison.OrdinalIgnoreCase))
            .Select(pair => Uri.UnescapeDataString(pair[1]))
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("The Figma URL must be a copied link to selection containing node-id.", nameof(url));

        return new FigmaNodeReference(segments[anchor + 1], nodeId.Replace('-', ':'), url);
    }
}

public static class FigmaImportUrlList
{
    /// <summary>Combines repeated command-line URLs and a one-URL-per-line file, preserving first-seen order.</summary>
    public static IReadOnlyList<string> Load(IEnumerable<string> commandLineUrls, string? urlFilePath)
    {
        var urls = new List<string>();
        urls.AddRange(commandLineUrls.Where(url => !string.IsNullOrWhiteSpace(url)).Select(url => url.Trim()));

        if (!string.IsNullOrWhiteSpace(urlFilePath))
        {
            if (!File.Exists(urlFilePath)) throw new FileNotFoundException("The Figma URL file was not found.", urlFilePath);
            foreach (var line in File.ReadLines(urlFilePath))
            {
                var url = line.Trim();
                if (url.Length == 0 || url.StartsWith('#')) continue;
                urls.Add(url);
            }
        }

        return urls.Distinct(StringComparer.Ordinal).ToArray();
    }
}

public sealed class FigmaImportOptions
{
    public required string Url { get; init; }
    public required string Scenario { get; init; }
    public required string OutputDirectory { get; init; }
    public required string Token { get; init; }
    public string? IdMapPath { get; init; }
    public bool MappedNodesOnly { get; init; }
    public bool KeepRawResponse { get; init; }
}

public sealed class FigmaImportResult
{
    public required string DesignPath { get; init; }
    public required string ReferenceImagePath { get; init; }
    public required string SourcePath { get; init; }
    public string? RawResponsePath { get; init; }
    public required DesignComponentSpec Design { get; init; }
}

public sealed class FigmaImportSource
{
    public int SchemaVersion { get; init; } = ArtifactSchema.CurrentVersion;
    public required string FigmaUrl { get; init; }
    public required string FileKey { get; init; }
    public required string NodeId { get; init; }
    public string? FigmaVersion { get; init; }
    public string? LastModified { get; init; }
    public int ImageScale { get; init; } = 1;
    public DateTimeOffset ImportedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Downloads a selected Figma node and writes Visual QA design inputs without storing the access token.</summary>
public sealed class FigmaImportService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient _httpClient;
    private readonly FigmaNormalizer _normalizer;

    public FigmaImportService(HttpClient httpClient, FigmaNormalizer? normalizer = null)
    {
        _httpClient = httpClient;
        _normalizer = normalizer ?? new FigmaNormalizer();
    }

    /// <summary>Gets a readable, filesystem-safe fixture name from the selected component and its parent component set.</summary>
    public async Task<string> SuggestFixtureNameAsync(string url, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Figma token is missing.");
        var reference = FigmaNodeReference.Parse(url);
        var uri = $"https://api.figma.com/v1/files/{Uri.EscapeDataString(reference.FileKey)}?ids={Uri.EscapeDataString(reference.NodeId)}&depth=5";
        using var request = AuthorizedGet(uri, token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "read the selected Figma component hierarchy");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("document", out var root)) return FallbackFixtureName(reference.NodeId);

        var hierarchy = new List<JsonElement>();
        if (!FindHierarchy(root, reference.NodeId, hierarchy)) return FallbackFixtureName(reference.NodeId);
        var selectedName = StringValue(hierarchy[^1], "name") ?? reference.NodeId;
        var componentSet = hierarchy.LastOrDefault(node => StringValue(node, "type") == "COMPONENT_SET");
        var componentSetName = StringValue(componentSet, "name");
        var variantValues = selectedName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Select(parts => parts.Length == 2 ? parts[1] : parts[0])
            .Select(ToNamePart)
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(componentSetName)) parts.Add(ToNamePart(componentSetName));
        parts.AddRange(variantValues);
        return parts.Count > 0 ? string.Join('.', parts) : FallbackFixtureName(reference.NodeId);
    }

    public async Task<FigmaImportResult> ImportAsync(FigmaImportOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Token))
            throw new InvalidOperationException("Figma token is missing. Set VISUALQA_FIGMA_TOKEN in the command environment.");

        var reference = FigmaNodeReference.Parse(options.Url);
        var nodeUri = $"https://api.figma.com/v1/files/{Uri.EscapeDataString(reference.FileKey)}/nodes?ids={Uri.EscapeDataString(reference.NodeId)}";
        using var nodeRequest = AuthorizedGet(nodeUri, options.Token);
        using var nodeResponse = await _httpClient.SendAsync(nodeRequest, cancellationToken);
        await EnsureSuccess(nodeResponse, "download the selected Figma node");
        var rawJson = await nodeResponse.Content.ReadAsStringAsync(cancellationToken);
        using var rawDocument = JsonDocument.Parse(rawJson);

        var idMap = LoadIdMap(options.IdMapPath);
        if (options.MappedNodesOnly && idMap is null)
            throw new InvalidOperationException("--mapped-only requires --id-map so design elements have stable Visual QA IDs.");
        var design = _normalizer.Normalize(rawDocument.RootElement, options.Scenario, idMap, options.MappedNodesOnly);
        var imageUri = $"https://api.figma.com/v1/images/{Uri.EscapeDataString(reference.FileKey)}?ids={Uri.EscapeDataString(reference.NodeId)}&format=png&scale=1&use_absolute_bounds=true";
        using var imageRequest = AuthorizedGet(imageUri, options.Token);
        using var imageResponse = await _httpClient.SendAsync(imageRequest, cancellationToken);
        await EnsureSuccess(imageResponse, "request the Figma reference image");
        using var imageDocument = JsonDocument.Parse(await imageResponse.Content.ReadAsStringAsync(cancellationToken));
        var downloadUrl = imageDocument.RootElement.TryGetProperty("images", out var images) &&
                          images.TryGetProperty(reference.NodeId, out var imageUrl) && imageUrl.ValueKind == JsonValueKind.String
            ? imageUrl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(downloadUrl)) throw new InvalidDataException("Figma did not return a PNG URL for the selected node.");

        var imageBytes = await _httpClient.GetByteArrayAsync(downloadUrl, cancellationToken);
        Directory.CreateDirectory(options.OutputDirectory);
        var designPath = Path.Combine(options.OutputDirectory, "design.json");
        var referencePath = Path.Combine(options.OutputDirectory, "reference.png");
        var sourcePath = Path.Combine(options.OutputDirectory, "figma-source.json");
        await File.WriteAllTextAsync(designPath, JsonSerializer.Serialize(design, Json), cancellationToken);
        await File.WriteAllBytesAsync(referencePath, imageBytes, cancellationToken);
        await File.WriteAllTextAsync(sourcePath, JsonSerializer.Serialize(new FigmaImportSource
        {
            FigmaUrl = options.Url,
            FileKey = reference.FileKey,
            NodeId = reference.NodeId,
            FigmaVersion = StringValue(rawDocument.RootElement, "version"),
            LastModified = StringValue(rawDocument.RootElement, "lastModified")
        }, Json), cancellationToken);

        string? rawPath = null;
        if (options.KeepRawResponse)
        {
            rawPath = Path.Combine(options.OutputDirectory, "raw-figma-node.json");
            await File.WriteAllTextAsync(rawPath, rawJson, cancellationToken);
        }

        return new FigmaImportResult
        {
            DesignPath = designPath,
            ReferenceImagePath = referencePath,
            SourcePath = sourcePath,
            RawResponsePath = rawPath,
            Design = design
        };
    }

    private static HttpRequestMessage AuthorizedGet(string uri, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("X-Figma-Token", token);
        return request;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        var details = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Figma could not {operation}: {(int)response.StatusCode} {response.ReasonPhrase}. {details}".Trim());
    }

    private static IReadOnlyDictionary<string, string>? LoadIdMap(string? idMapPath)
    {
        if (string.IsNullOrWhiteSpace(idMapPath)) return null;
        if (!File.Exists(idMapPath)) throw new FileNotFoundException("The Figma ID map file was not found.", idMapPath);
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(idMapPath));
        return map ?? throw new InvalidDataException("The Figma ID map must be a JSON object of Figma node IDs to Visual QA IDs.");
    }

    private static string? StringValue(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool FindHierarchy(JsonElement node, string id, List<JsonElement> hierarchy)
    {
        if (node.ValueKind != JsonValueKind.Object) return false;
        hierarchy.Add(node);
        if (StringValue(node, "id") == id) return true;
        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray())
                if (FindHierarchy(child, id, hierarchy)) return true;
        hierarchy.RemoveAt(hierarchy.Count - 1);
        return false;
    }

    private static string FallbackFixtureName(string nodeId) => $"Figma.{ToNamePart(nodeId)}";

    private static string ToNamePart(string value)
    {
        var parts = value.Split([' ', '.', '/', '\\', ':', '=', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
            .Where(part => part.Length > 0);
        return string.Join('.', parts);
    }
}
