using System.Net;
using System.Text;
using System.Text.Json;
using VisualQa.Figma;
using Xunit;

namespace VisualQa.Core.Tests;

public sealed class FigmaImportTests
{
    private const string NodeJson = """
        {"version":"abc","lastModified":"2026-08-26T00:00:00Z","nodes":{"1:2":{"document":{"id":"1:2","name":"Primary button","type":"COMPONENT","absoluteBoundingBox":{"x":100,"y":200,"width":85,"height":40},"fills":[{"color":{"r":0,"g":0.262745,"b":0.541176}}],"children":[{"id":"1:3","name":"Label","type":"TEXT","absoluteBoundingBox":{"x":116,"y":208,"width":53,"height":24},"characters":"Button","fills":[{"color":{"r":1,"g":1,"b":1}}],"style":{"fontFamily":"Neue Frutiger","fontSize":16,"fontWeight":700,"lineHeightPx":24,"letterSpacing":0}}]}}}}
        """;

    [Fact]
    public void Parses_selected_node_url()
    {
        var reference = FigmaNodeReference.Parse("https://www.figma.com/design/MVQKl5zKvD9JeIbmIgfZcE/Filament?node-id=15228-79043&m=dev");

        Assert.Equal("MVQKl5zKvD9JeIbmIgfZcE", reference.FileKey);
        Assert.Equal("15228:79043", reference.NodeId);
    }

    [Fact]
    public void Normalizes_selected_node_with_root_relative_bounds_and_id_map()
    {
        using var document = JsonDocument.Parse(NodeJson);
        var design = new FigmaNormalizer().Normalize(document.RootElement, "Input.Button", new Dictionary<string, string> { ["1:3"] = "button-label" });

        Assert.Equal("Input.Button", design.Name);
        Assert.Equal(85, design.Width);
        Assert.Equal(40, design.Height);
        Assert.Equal(new VisualQa.Core.RectSpec(0, 0, 85, 40), design.Elements[0].Bounds);
        var label = Assert.Single(design.Elements, element => element.Id == "button-label");
        Assert.Equal(new VisualQa.Core.RectSpec(16, 8, 53, 24), label.Bounds);
        Assert.Equal("#FFFFFF", label.ForegroundColor);
        Assert.Equal("Neue Frutiger", label.FontFamily);
    }

    [Fact]
    public void Normalizes_only_explicitly_mapped_nodes_when_requested()
    {
        using var document = JsonDocument.Parse(NodeJson);

        var design = new FigmaNormalizer().Normalize(
            document.RootElement,
            "Input.Button",
            new Dictionary<string, string> { ["1:2"] = "button-root", ["1:3"] = "button-label" },
            mappedNodesOnly: true);

        Assert.Equal(["button-root", "button-label"], design.Elements.Select(element => element.Id));
    }

    [Fact]
    public void Makes_fallback_ids_unique_when_figma_layer_names_repeat()
    {
        const string repeatedNames = """
            {"document":{"id":"1:1","name":"Root","type":"FRAME","absoluteBoundingBox":{"x":0,"y":0,"width":10,"height":10},"children":[{"id":"1:2","name":"Vector","type":"VECTOR","absoluteBoundingBox":{"x":0,"y":0,"width":1,"height":1}},{"id":"1:3","name":"Vector","type":"VECTOR","absoluteBoundingBox":{"x":1,"y":1,"width":1,"height":1}}]}}
            """;
        using var document = JsonDocument.Parse(repeatedNames);

        var ids = new FigmaNormalizer().Normalize(document.RootElement).Elements.Select(element => element.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("vector", ids);
        Assert.Contains("vector--1-3", ids);
    }

    [Fact]
    public async Task Suggests_component_set_and_variant_fixture_name()
    {
        using var client = new HttpClient(new FakeFigmaHandler());

        var name = await new FigmaImportService(client).SuggestFixtureNameAsync(
            "https://www.figma.com/design/test-file/Filament?node-id=1-2", "test-token");

        Assert.Equal("Dialog.Caution.Solid", name);
    }

    [Fact]
    public void Loads_url_file_while_skipping_comments_blanks_and_duplicates()
    {
        var file = Path.Combine(Path.GetTempPath(), "visualqa-urls-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(file, """
                # Dialog states

                https://www.figma.com/design/file/Dialog?node-id=1-2
                https://www.figma.com/design/file/Dialog?node-id=3-4
                https://www.figma.com/design/file/Dialog?node-id=1-2
                """);

            var urls = FigmaImportUrlList.Load(["https://www.figma.com/design/file/Dialog?node-id=3-4"], file);

            Assert.Equal([
                "https://www.figma.com/design/file/Dialog?node-id=3-4",
                "https://www.figma.com/design/file/Dialog?node-id=1-2"
            ], urls);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Url_file_requires_an_existing_file() =>
        Assert.Throws<FileNotFoundException>(() => FigmaImportUrlList.Load([], "not-a-real-figma-url-file.txt"));

    [Fact]
    public async Task Imports_design_reference_and_provenance_without_persisting_the_token()
    {
        var handler = new FakeFigmaHandler();
        using var client = new HttpClient(handler);
        var output = Path.Combine(Path.GetTempPath(), "visualqa-figma-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new FigmaImportService(client).ImportAsync(new FigmaImportOptions
            {
                Url = "https://www.figma.com/design/test-file/Filament?node-id=1-2",
                Scenario = "Input.Button",
                OutputDirectory = output,
                Token = "test-token"
            });

            Assert.True(File.Exists(result.DesignPath));
            Assert.True(File.Exists(result.ReferenceImagePath));
            Assert.True(File.Exists(result.SourcePath));
            Assert.False(File.Exists(Path.Combine(output, "raw-figma-node.json")));
            Assert.DoesNotContain("test-token", File.ReadAllText(result.SourcePath));
            Assert.Equal("test-token", handler.NodeToken);
            Assert.Contains("use_absolute_bounds=true", handler.ImageQuery);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(result.ReferenceImagePath));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private sealed class FakeFigmaHandler : HttpMessageHandler
    {
        public string? NodeToken { get; private set; }
        public string ImageQuery { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "api.figma.com" && request.RequestUri.AbsolutePath.EndsWith("/nodes"))
            {
                NodeToken = request.Headers.GetValues("X-Figma-Token").Single();
                return Task.FromResult(JsonResponse(NodeJson));
            }
            if (request.RequestUri!.Host == "api.figma.com" && request.RequestUri.AbsolutePath == "/v1/files/test-file")
                return Task.FromResult(JsonResponse("""
                    {"document":{"id":"0:0","name":"Document","type":"DOCUMENT","children":[{"id":"1:0","name":"Dialog","type":"CANVAS","children":[{"id":"1:1","name":"Dialog","type":"COMPONENT_SET","children":[{"id":"1:2","name":"Signal=Caution, Translucency=Solid","type":"COMPONENT"}]}]}]}}
                    """));
            if (request.RequestUri!.Host == "api.figma.com" && request.RequestUri.AbsolutePath.Contains("/images/"))
            {
                ImageQuery = request.RequestUri.Query;
                return Task.FromResult(JsonResponse("{\"images\":{\"1:2\":\"https://download.example/reference.png\"}}"));
            }
            if (request.RequestUri!.Host == "download.example")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }
}
