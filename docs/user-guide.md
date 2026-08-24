# Visual QA user guide

## What this tool does

Visual QA compares a Figma design reference with a rendered WPF or React component. It uses two forms of evidence:

- Visual evidence: the reference and implementation screenshots.
- Structural evidence: normalized element metadata such as stable IDs, bounds, text, typography, colors, margins, and padding.

The current CLI directly evaluates structural, geometry, spacing, typography, and icon metadata when values are available. Color Delta-E, generic margin/padding, and clipping-specific metadata validation remain planned; image comparison can still reveal their visible effect.

It does not use AI, OCR, or cloud-based visual analysis.

## Choose the correct solution

Use the cross-platform solution on Windows, macOS, or Linux when working with Figma and React captures:

```bash
dotnet restore VisualQa.CrossPlatform.sln
dotnet build VisualQa.CrossPlatform.sln --no-restore
dotnet test VisualQa.CrossPlatform.sln --no-build
```

Use the Windows solution when you also need to compile or run WPF capture:

```powershell
dotnet restore VisualQa.sln
dotnet build VisualQa.sln --no-restore
dotnet test VisualQa.sln --no-build
```

WPF screenshot capture can only run on Windows. The CLI can run on macOS/Linux and compare any WPF artifacts that were captured on Windows.

## Prepare a visual test

Create one component directory under `visual-tests`:

```text
visual-tests/
  PatientInfo/
    design/
      reference.png
      design.json
    wpf/
      actual.png
      runtime.json
    react/
      actual.png
      runtime.json
```

`design.json` and `runtime.json` must contain `"schemaVersion": 1`. The expected and actual files must be captured at the same intended component dimensions. Do not pre-scale either screenshot to make it match.

For the full Figma desktop export and REST JSON acquisition workflow, see [figma-design-input-guide.md](figma-design-input-guide.md).

## Add stable element IDs

Stable IDs produce the most reliable findings.

For WPF, attach an ID to every element that should be evaluated:

```xml
<TextBlock qa:VisualQa.Id="patient-name" Text="{Binding PatientName}" />
```

For React, use `data-qa`:

```tsx
<span data-qa="patient-name">{patientName}</span>
```

The normalized design element should use the same ID. Matching falls back to name, semantic role, text, and type/position only when an ID is unavailable.

## Create captures

### WPF

Use `WpfScreenshotRenderer.Render` with fixed dimensions and DPI, and call `WpfMetadataCollector.Collect` from the same fully laid-out component instance. The repository provides these library APIs; your WPF test host coordinates them and writes `actual.png` plus `runtime.json`. Use fixed test data, a known theme/background, and installed deterministic fonts.

### React

From `react-capture`, install the JavaScript dependencies:

```bash
npm install
npx playwright install chromium
```

Serve your deterministic visual-test route, then run the capture script with the route and output directory if they differ from its defaults:

```bash
VISUALQA_URL=http://127.0.0.1:4173/visual-test/patient-info VISUALQA_OUTPUT=../visual-tests/PatientInfo/react npm run capture
```

On PowerShell, set environment variables before the command:

```powershell
$env:VISUALQA_URL = 'http://127.0.0.1:4173/visual-test/patient-info'
$env:VISUALQA_OUTPUT = '../visual-tests/PatientInfo/react'
npm.cmd run capture
```

The route must expose a component root with `data-qa-root` and the elements to capture with `data-qa`.

## Run comparisons

Compare one platform:

```bash
dotnet run --project src/VisualQa.Cli -- compare --design visual-tests --platform react --output results --config visualqa.json
```

Compare every available WPF and React capture:

```bash
dotnet run --project src/VisualQa.Cli -- compare-all --design visual-tests --output results --config visualqa.json
```

To compare screenshots without Figma JSON or runtime metadata, use image-only mode:

```bash
dotnet run --project src/VisualQa.Cli -- compare-images --reference calibration/baseline.png --actual implementation.png --output results/image-only --config visualqa.json
```

See [image-only-calibration-guide.md](image-only-calibration-guide.md) for the controlled WPF defect suite.

For the included deterministic PatientInfo demo, seed the reference and example capture PNGs first:

```bash
dotnet run --project src/VisualQa.Cli -- seed-demo visual-tests
```

## Configure tolerances

Edit `visualqa.json` rather than changing code. It controls position and size tolerances, allowed translation, pixel color tolerance, similarity thresholds, image-only thresholds, and diff-region noise filtering. Color threshold settings are reserved for the planned Delta-E validator and do not currently affect CLI results.

Start with strict values in a controlled environment. If anti-aliasing or font rasterization causes noise, first verify font, DPI, browser, theme, and capture dimensions. Increase tolerances only when the environment is known to be deterministic.

## Read the results

Each component/platform output contains:

- `report.html`: an offline, human-readable report.
- `report.json`: structured findings for CI or downstream tooling.
- `overlay.png`: reference/image overlay evidence.
- `heatmap.png` and `pixel-diff.png`: pixels exceeding the configured tolerance.
- `diff-regions.png`: significant connected mismatch regions.

Finding statuses are `Pass`, `Warning`, `Fail`, `NotEvaluated`, and `Error`. `NotEvaluated` means the expected or actual property was unavailable; it is never silently skipped.

The CLI exits with `0` for pass, `1` for QA failures, and `2` for input, schema, or configuration errors.

## CI workflow

Use this order in CI:

1. Restore and build the relevant solution.
2. Start deterministic application/test hosts.
3. Capture screenshots and runtime metadata.
4. Run `compare` or `compare-all`.
5. Publish the `results` directory as a build artifact.
6. Use the CLI exit code to pass or fail the job.

Use a Windows runner with a fixed DPI and pinned Chromium when WPF capture is involved. Pin browser and font versions for React-only jobs as well.

## Troubleshooting

- `Unsupported schemaVersion`: regenerate the artifact with the supported contract version.
- Missing elements: verify matching `VisualQa.Id`, `data-qa`, and design IDs.
- High pixel differences with valid metadata: check component crop, DPI, fonts, theme, and browser version before relaxing thresholds.
- Playwright browser installation fails with a TLS issuer error: configure the organization-approved certificate authority for Node/npm, then retry. Do not disable TLS certificate validation.
- WPF capture unavailable on macOS/Linux: perform capture on Windows and copy the generated `actual.png` and `runtime.json` into the test directory before comparison.

For the architectural model and extension guidance, see [architecture.md](architecture.md).
