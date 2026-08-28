# Visual QA CLI user manual

## Run the CLI

The CLI is currently run from the repository source tree. Replace `visualqa` in the examples below with:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli --
```

For example:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- compare-images ...
```

Run all commands from `C:\Bilal\Visual-QA`, or use absolute paths for every input and output.

## Commands

### `import-figma`

Use this once per Figma component/state to acquire the two design inputs required by `compare`: the normalized `design.json` and the exact Figma `reference.png`.

```text
import-figma --url <figma-link-to-selection> [--scenario <name>] [--output <design-folder>] [--id-map <map.json>] [--token-file <local-secret-file>] [--keep-raw]
```

Before running it, create `.visualqa/figma-token.txt` below the directory from which you run the command, and place only a Figma personal access token in that file. Do not put the token in the command, configuration file, or source control.

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- import-figma `
  --url "https://www.figma.com/design/<file-key>/<file-name>?node-id=<selected-node-id>&m=dev"
```

With only `--url`, the CLI derives a readable name from the Figma component set and selected variant, writes to `visual-tests/<derived-component-name>/design`, and prints every output path. For example, the Figma Dialog variant `Signal=Caution, Translucency=Solid` becomes `visual-tests/Dialog.Caution.Solid/design`.

If the default token file is missing, the command exits with code `2` and tells you the exact file path to create. `.visualqa/` is ignored by Git. Use `--token-file` to use a different local secret file; it overrides the default.

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- import-figma `
  --url "<figma-link-to-selection>" `
  --output C:\VisualQaFixtures\CustomComponent\design `
  --token-file C:\Secrets\figma-token.txt
```

Copy the URL with **Copy link to selection** after selecting one component or state in Figma. The command calls the official Figma node and image APIs at `1x`, requesting the selected node's absolute bounds so the reference canvas aligns with the design geometry. It writes `design.json`, `reference.png`, and non-secret provenance in `figma-source.json`. `--keep-raw` also writes `raw-figma-node.json` for troubleshooting; it is ignored by Git. An optional `--id-map` is a JSON object mapping Figma node IDs to stable runtime QA IDs, for example `{ "15228:79044": "button-label" }`.

This import has no role during a normal offline comparison. It is an acquisition step. It normalizes selected-node geometry, text, available typography, primary solid colors, and basic icon candidates; unavailable Figma fields remain unavailable rather than being guessed.

#### Batch import with `--url-file`

Use a text file containing one copied Figma link to selection per line. Blank lines and lines beginning with `#` are ignored; duplicate URLs are imported only once.

```text
# Dialog fixture states
https://www.figma.com/design/<file-key>/Filament?node-id=<first-node-id>&m=dev
https://www.figma.com/design/<file-key>/Filament?node-id=<second-node-id>&m=dev
```

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- import-figma `
  --url-file .visualqa\figma-import-urls.txt
```

Each URL uses the same dynamic name and default output rules as a single import. Imports run sequentially to avoid unnecessary Figma API pressure. The command continues through individual import failures, prints each result, and writes `visual-tests/import-summary.json`. It returns `0` when all URLs import, `1` when one or more URLs fail, and `2` for invalid input such as a missing token or URL file.

You may also repeat `--url` in one command. Do not combine batch mode with `--scenario`, `--output`, or `--id-map`, because those options apply to one component only.

### `compare-images`

Use this for visual-only comparison. It needs only two PNG images and does not require Figma JSON or runtime metadata.

```text
compare-images --reference <approved.png> --actual <implementation.png> --output <results-folder> [--config <visualqa.json>]
```

Example:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- compare-images `
  --reference C:\Bilal\Visual-QA\calibration\baseline.png `
  --actual C:\Bilal\Visual-QA\calibration-results\03\actual.png `
  --output C:\Bilal\Visual-QA\manual-results\case-03 `
  --config C:\Bilal\Visual-QA\visualqa.json
```

Arguments:

- `--reference` — approved design, baseline, or golden PNG.
- `--actual` — implementation screenshot PNG.
- `--output` — directory where Visual QA writes its report and image evidence.
- `--config` — optional policy file; defaults to built-in values if omitted.

Result files:

```text
report.html        Open this first.
report.json        Structured result for automation.
overlay.png        Visual evidence with mismatches highlighted.
heatmap.png        Difference intensity image.
pixel-diff.png     Pixels outside the configured color tolerance.
diff-regions.png   Significant connected mismatch regions.
```

Use this command when you want the answer: “Do these screenshots differ beyond the allowed tolerance?” It cannot tell you the semantic cause of a mismatch.

### `compare`

Use this for one platform’s design-conformance comparison. It needs design and runtime metadata in addition to screenshots.

```text
compare --design <visual-tests-folder> --platform <wpf|react> --output <results-folder> [--config <visualqa.json>]
```

Example:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- compare `
  --design C:\Bilal\Visual-QA\visual-tests `
  --platform wpf `
  --output C:\Bilal\Visual-QA\results\wpf `
  --config C:\Bilal\Visual-QA\visualqa.json
```

Required directory layout for each component:

```text
<visual-tests-folder>
  <ComponentName>
    design
      reference.png
      design.json
    wpf                         # when --platform wpf
      actual.png
      runtime.json
```

For React, replace the `wpf` directory with `react` and use `--platform react`.

This command evaluates available structural, geometry, spacing, typography, icon, and screenshot evidence. It writes one report directory per component/platform and a top-level `summary.json`. Color threshold values in `visualqa.json` are not yet evaluated by a dedicated Delta-E validator.

### `compare-all`

Use this when a test directory contains both WPF and React captures.

```text
compare-all --design <visual-tests-folder> --output <results-folder> [--config <visualqa.json>]
```

Example:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- compare-all `
  --design C:\Bilal\Visual-QA\visual-tests `
  --output C:\Bilal\Visual-QA\results\all-platforms `
  --config C:\Bilal\Visual-QA\visualqa.json
```

The command compares each available component’s `wpf` and `react` capture folders. It does not compare WPF directly with React.

### `calibrate`

Windows-only. This is a self-test of the Visual QA image engine, not a test of your application.

```text
calibrate [--manifest <calibration/manifest.json>] [--output <calibration-results>] [--config <visualqa.json>]
```

Example:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- calibrate `
  --manifest C:\Bilal\Visual-QA\calibration\manifest.json `
  --output C:\Bilal\Visual-QA\calibration-results `
  --config C:\Bilal\Visual-QA\visualqa.json
```

It renders a fixed WPF baseline and 24 intentional deviations, checks them against the expected outcomes in the manifest, and writes `calibration-summary.html` and `calibration-summary.json`.

Do not use `--refresh-baseline` unless you deliberately approve a new baseline image and review its source-control change.

### `seed-demo`

Creates PNG fixtures for the included `PatientInfo` sample.

```text
seed-demo [<visual-tests-folder>]
```

Example:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- seed-demo C:\Bilal\Visual-QA\visual-tests
```

It is for demonstration only; it does not capture a real WPF or React application.

## Understand status and exit codes

Finding/report status:

- `Pass` — within the configured tolerance.
- `Warning` — a small visual deviation or incomplete non-blocking evidence.
- `Fail` — an unacceptable mismatch.
- `NotEvaluated` — data required for that check was unavailable.
- `Error` — comparison could not complete correctly.

Process exit codes:

- `0` — comparison passed or produced warnings only; calibration passed all declared cases.
- `1` — one or more QA failures; calibration expectation mismatch.
- `2` — invalid command, missing required input, unsupported schema, unsupported operating system, or system/configuration failure.

## Configure allowed deviation

Use `visualqa.json` to set the tolerance policy. Key settings include:

- `visual.pixelColorTolerance` — per-channel pixel difference ignored as rasterization noise.
- `imageOnly.passDifferentPixelPercentage` / `warningDifferentPixelPercentage` — allowed changed-pixel percentage for image-only mode.
- `imageOnly.ssimPass` / `ssimWarning` — structural-similarity thresholds.
- `imageOnly.allowTranslationAlignment` — whether crop translation alignment is allowed; no scaling, rotation, warping, or perspective correction is ever performed.
- `geometry` and `color` — metadata comparison tolerance settings.

Use a controlled capture environment before relaxing thresholds: fixed dimensions, DPI, fonts, theme, browser/runtime version, and test data.

## Platform support

- `compare-images`, `compare`, and `compare-all` can run on Windows, macOS, and Linux when their input artifacts already exist.
- WPF capture and `calibrate` require Windows.
- React capture requires a working Playwright/Chromium environment.

## Figma import boundary

`import-figma` is the only CLI command that contacts Figma. It is an acquisition step, not part of a normal offline comparison. `compare` and `compare-all` consume the generated files locally. The importer normalizes selected-node geometry, text, available typography, primary solid colors, and basic icon candidates. Use `--id-map` for stable WPF/React metadata matching where Figma layer names differ from application QA IDs.
