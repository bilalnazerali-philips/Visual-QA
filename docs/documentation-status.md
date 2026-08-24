# Documentation validation status

Documentation in this repository was checked against the source implementation on 2026-08-24.

## Verified implementation evidence

- The supported CLI commands and their required options are defined in `src/VisualQa.Cli/Program.cs`: `compare`, `compare-all`, `compare-images`, `calibrate`, and `seed-demo`.
- `compare` and `compare-all` require a design folder plus `reference.png`, `design.json`, `actual.png`, and `runtime.json`; they invoke structural, geometry, spacing, typography, and icon validators.
- `compare-images` requires only `--reference`, `--actual`, and `--output`; it creates the report and four PNG evidence artifacts.
- `calibrate` delegates to the Windows-only `VisualQa.Calibration` project, which renders the WPF baseline and the 24 cases declared in `calibration/manifest.json`.
- Image-only thresholds are implemented by `VisualQa.Configuration.ImageOnlyOptions` and loaded from `visualqa.json`.
- The image engine provides pixel difference, connected regions, global SSIM, and optional bounded translation-only alignment. It does not scale, rotate, warp, or perspective-correct images.
- `VisualQa.Figma` contains parser/normalizer classes, but no `import-figma` CLI command.
- The WPF capture project exposes rendering and metadata-collection APIs. A consuming WPF test host must coordinate those APIs; the repository does not yet include a generic application-host command.

## Documented limitations

- Color threshold values are configured but no dedicated Delta-E color validator is currently invoked by the CLI.
- Generic margin/padding and clipping-specific metadata rules are not yet implemented as independent validators.
- Image-only comparison identifies visible mismatch, not the semantic root cause.
- React browser capture depends on a local Playwright/Chromium installation and a served test route.

## Validation commands

The following commands validated the current code paths:

```powershell
dotnet build VisualQa.sln --no-restore
dotnet test VisualQa.sln --no-build
dotnet run --project src/VisualQa.Cli -- calibrate --manifest calibration/manifest.json --output calibration-results --config visualqa.json
dotnet run --project src/VisualQa.Cli -- compare-images --reference calibration/baseline.png --actual calibration-results/03/actual.png --output results/image-only --config visualqa.json
```
