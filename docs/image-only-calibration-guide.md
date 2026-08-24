# Image-only calibration guide

Image-only mode validates the visual engine without Figma JSON or runtime metadata. It compares `baseline.png` with an implementation screenshot and reports pixels, SSIM, translation evidence, and significant mismatch regions.

```bash
dotnet run --project src/VisualQa.Cli -- compare-images --reference baseline.png --actual actual.png --output results --config visualqa.json
```

Use `VisualQa.Calibration` on Windows to render the committed WPF baseline and the 24 controlled deviations declared in `calibration/manifest.json`. The runner writes per-case reports and `calibration-summary.json`; it fails when any actual verdict or required diff region differs from the manifest.

```powershell
dotnet run --project src/VisualQa.Cli -- calibrate --manifest calibration/manifest.json --output calibration-results --config visualqa.json
```

The calibration renderer is Windows-only because it uses WPF. `compare-images` is cross-platform and can compare committed or externally captured PNGs on Windows, macOS, and Linux. Image-only findings prove a visual mismatch, but cannot prove semantic causes such as a wrong icon resource or font family; use metadata-based comparison for that explanation.
