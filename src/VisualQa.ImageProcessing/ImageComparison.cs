using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace VisualQa.ImageProcessing;

public sealed record DiffRegion(int X, int Y, int Width, int Height, int Area);
public sealed record ImageComparisonOptions(int PixelColorTolerance, int MinimumArea, bool AlignmentEnabled, int MaxTranslationPx);
public sealed record ImageComparisonResult(double Similarity, int DifferentPixels, int TotalPixels, int OffsetX, int OffsetY, double AlignmentConfidence, IReadOnlyList<DiffRegion> Regions)
{ public double DifferentPixelPercentage => TotalPixels == 0 ? 0 : DifferentPixels * 100d / TotalPixels; }

public sealed class PixelDiffEngine
{
    public ImageComparisonResult Compare(string referencePath, string actualPath, string outputDirectory, int tolerance, int minimumArea, int maxTranslation) => Compare(referencePath, actualPath, outputDirectory, new(tolerance, minimumArea, true, maxTranslation));
    public ImageComparisonResult Compare(string referencePath, string actualPath, string outputDirectory, ImageComparisonOptions options)
    {
        using var reference = Image.Load<Rgba32>(referencePath); using var actual = Image.Load<Rgba32>(actualPath);
        var offset = options.AlignmentEnabled ? FindTranslation(reference, actual, options.MaxTranslationPx) : (0, 0, 1d);
        var width = Math.Max(reference.Width, actual.Width); var height = Math.Max(reference.Height, actual.Height);
        using var diff = new Image<Rgba32>(width, height); using var overlay = new Image<Rgba32>(width, height);
        var changed = new bool[width, height]; var lumaReference = new List<double>(width * height); var lumaActual = new List<double>(width * height); var count = 0;
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        { var expected = PixelAt(reference, x, y); var observed = PixelAt(actual, x - offset.Item1, y - offset.Item2); lumaReference.Add(Luma(expected)); lumaActual.Add(Luma(observed)); var delta = MaxDelta(expected, observed); if (delta > options.PixelColorTolerance) { count++; changed[x, y] = true; diff[x, y] = new(255, (byte)Math.Min(255, delta * 2), 0); overlay[x, y] = new(255, 0, 0, 180); } else { diff[x, y] = new(0, 0, 0, 0); overlay[x, y] = expected; } }
        Directory.CreateDirectory(outputDirectory); diff.Save(Path.Combine(outputDirectory, "pixel-diff.png")); diff.Save(Path.Combine(outputDirectory, "heatmap.png")); overlay.Save(Path.Combine(outputDirectory, "overlay.png"));
        var regions = Regions(changed, options.MinimumArea); using var boxes = diff.Clone(); DrawBoxes(boxes, regions); boxes.Save(Path.Combine(outputDirectory, "diff-regions.png"));
        return new(StructuralSimilarity(lumaReference, lumaActual), count, width * height, offset.Item1, offset.Item2, offset.Item3, regions);
    }
    private static (int, int, double) FindTranslation(Image<Rgba32> reference, Image<Rgba32> actual, int maximum)
    { var bestX = 0; var bestY = 0; var bestError = Error(reference, actual, 0, 0); for (var y = -maximum; y <= maximum; y++) for (var x = -maximum; x <= maximum; x++) { var error = Error(reference, actual, x, y); if (error < bestError) { bestError = error; bestX = x; bestY = y; } } var zero = Error(reference, actual, 0, 0); return (bestX, bestY, zero == 0 ? 1 : Math.Clamp(1 - bestError / zero, 0, 1)); }
    private static double Error(Image<Rgba32> reference, Image<Rgba32> actual, int offsetX, int offsetY)
    { var width = Math.Min(reference.Width, actual.Width); var height = Math.Min(reference.Height, actual.Height); double sum = 0; var samples = 0; for (var y = 0; y < height; y += 2) for (var x = 0; x < width; x += 2) { var delta = MaxDelta(PixelAt(reference, x, y), PixelAt(actual, x - offsetX, y - offsetY)); sum += delta * delta; samples++; } return sum / Math.Max(1, samples); }
    private static double StructuralSimilarity(IReadOnlyList<double> x, IReadOnlyList<double> y)
    { var meanX = x.Average(); var meanY = y.Average(); double vx = 0, vy = 0, cov = 0; for (var i = 0; i < x.Count; i++) { vx += Math.Pow(x[i] - meanX, 2); vy += Math.Pow(y[i] - meanY, 2); cov += (x[i] - meanX) * (y[i] - meanY); } vx /= Math.Max(1, x.Count - 1); vy /= Math.Max(1, y.Count - 1); cov /= Math.Max(1, x.Count - 1); const double c1 = 6.5025, c2 = 58.5225; return Math.Clamp(((2 * meanX * meanY + c1) * (2 * cov + c2)) / ((meanX * meanX + meanY * meanY + c1) * (vx + vy + c2)), 0, 1); }
    private static Rgba32 PixelAt(Image<Rgba32> image, int x, int y) => x >= 0 && y >= 0 && x < image.Width && y < image.Height ? image[x, y] : new(0, 0, 0, 0);
    private static int MaxDelta(Rgba32 a, Rgba32 b) => Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A)));
    private static double Luma(Rgba32 p) => .2126 * p.R + .7152 * p.G + .0722 * p.B;
    private static void DrawBoxes(Image<Rgba32> image, IEnumerable<DiffRegion> regions) { foreach (var r in regions) { for (var x = r.X; x < r.X + r.Width; x++) { Set(image, x, r.Y); Set(image, x, r.Y + r.Height - 1); } for (var y = r.Y; y < r.Y + r.Height; y++) { Set(image, r.X, y); Set(image, r.X + r.Width - 1, y); } } }
    private static void Set(Image<Rgba32> image, int x, int y) { if (x >= 0 && y >= 0 && x < image.Width && y < image.Height) image[x, y] = new(0, 255, 255); }
    private static IReadOnlyList<DiffRegion> Regions(bool[,] changed, int minimum) { var width = changed.GetLength(0); var height = changed.GetLength(1); var seen = new bool[width, height]; var result = new List<DiffRegion>(); for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) { if (!changed[x, y] || seen[x, y]) continue; var queue = new Queue<(int X, int Y)>(); queue.Enqueue((x, y)); seen[x, y] = true; var minX = x; var maxX = x; var minY = y; var maxY = y; var area = 0; while (queue.Count > 0) { var p = queue.Dequeue(); area++; minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { var nx = p.X + d.Item1; var ny = p.Y + d.Item2; if (nx >= 0 && ny >= 0 && nx < width && ny < height && changed[nx, ny] && !seen[nx, ny]) { seen[nx, ny] = true; queue.Enqueue((nx, ny)); } } } if (area >= minimum) result.Add(new(minX, minY, maxX - minX + 1, maxY - minY + 1, area)); } return result; }
}

public sealed class ColorDistanceCalculator { public double DeltaE(string a, string b) { var x = Rgb(a); var y = Rgb(b); return Math.Sqrt(Math.Pow(x.r - y.r, 2) + Math.Pow(x.g - y.g, 2) + Math.Pow(x.b - y.b, 2)); } private static (double r, double g, double b) Rgb(string c) { c = c.Trim().TrimStart('#'); if (c.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) { var n = System.Text.RegularExpressions.Regex.Matches(c, "[0-9.]+").Select(x => double.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); return (n[0], n[1], n[2]); } return (Convert.ToInt32(c[..2], 16), Convert.ToInt32(c.Substring(2, 2), 16), Convert.ToInt32(c.Substring(4, 2), 16)); } }
public static class DemoArtifactSeeder { public static void SeedPatientInfo(string root) { foreach (var platform in new[] { "design", "wpf", "react" }) Directory.CreateDirectory(Path.Combine(root, "PatientInfo", platform)); using var image = new Image<Rgba32>(300, 72, new Rgba32(255, 255, 255)); for (var y = 12; y < 60; y++) for (var x = 16; x < 64; x++) image[x, y] = new Rgba32(70, 130, 180); image.Save(Path.Combine(root, "PatientInfo", "design", "reference.png")); image.Save(Path.Combine(root, "PatientInfo", "wpf", "actual.png")); image.Save(Path.Combine(root, "PatientInfo", "react", "actual.png")); } }
