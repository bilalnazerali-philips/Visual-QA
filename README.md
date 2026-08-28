# Visual QA

Deterministic, explainable design-conformance checks for local Figma REST exports, WPF, and React. It does not use AI, OCR, or cloud visual analysis.

See [the high-level architecture](docs/architecture.md) for the adapter boundaries, data flow, operating-system support, and CI model.
See [the user guide](docs/user-guide.md) for setup, capture, comparison, reporting, and troubleshooting instructions.
See [the Figma design-input guide](docs/figma-design-input-guide.md) for obtaining `reference.png`, raw Figma JSON, and normalized `design.json`.
See [the CLI user manual](docs/cli-user-manual.md) for every supported command, its options, outputs, exit codes, and examples.
See [the documentation validation status](docs/documentation-status.md) for code-backed capability and limitation evidence.
See [QA release packaging automation](docs/qa-release-automation.md) to produce the self-contained Windows QA ZIP locally or in CI.
See [the Visual QA strategy](docs/visual-qa-strategy.md) for the stakeholder-level purpose, fixture model, rollout plan, and Mermaid diagrams.
See [snapshot testing versus Visual QA](docs/snapshot-testing-vs-visual-qa.md) for why both approaches are useful and how to use them together.

## Image-only calibration

Run an image-only comparison without Figma or runtime metadata:

```bash
dotnet run --project src/VisualQa.Cli -- compare-images --reference calibration/baseline.png --actual calibration-results/03/actual.png --output results/image-only --config visualqa.json
```

On Windows, regenerate all controlled WPF variations and validate their expected results:

```powershell
dotnet run --project src/VisualQa.Cli -- calibrate --manifest calibration/manifest.json --output calibration-results --config visualqa.json
```

The approved baseline is [calibration/baseline.png](calibration/baseline.png); do not refresh it without reviewing the change.

## Run

1. On Windows (including WPF capture), use `VisualQa.sln`. On macOS/Linux, use `VisualQa.CrossPlatform.sln`; it excludes the Windows-only WPF adapter.
2. `dotnet restore VisualQa.CrossPlatform.sln`
3. `dotnet build VisualQa.CrossPlatform.sln --no-restore`
4. `dotnet test VisualQa.CrossPlatform.sln --no-build`
5. `dotnet run --project src/VisualQa.Cli -- seed-demo visual-tests` creates deterministic local PNG fixtures for the included demo.
6. In `react-capture`, run your platform's `npm install`, `npx playwright install chromium`, then serve the visual-test route and run `npm run capture`.
7. `dotnet run --project src/VisualQa.Cli -- compare-all --design visual-tests --output results --config visualqa.json`

Before a Figma-backed comparison, acquire each selected Figma component/state with `import-figma`. It uses a copied Figma link to selection and writes `design.json`, a matching `reference.png`, and non-secret provenance:

```powershell
dotnet run --project src/VisualQa.Cli -- import-figma --url "<figma-link-to-selection>"
```

The command reads `.visualqa/figma-token.txt` and derives a folder name from the selected Figma component/variant. By default it writes to `visual-tests/<derived-component-name>/design` under the current working directory and prints the exact paths. `--scenario`, `--output`, and `--token-file` remain optional overrides.

To import a batch, create a text file with one Figma copied-link-to-selection URL per line (blank lines and `#` comments are allowed), then run `dotnet run --project src/VisualQa.Cli -- import-figma --url-file .visualqa/figma-import-urls.txt`. The command writes `visual-tests/import-summary.json`.

Windows 11 with a fixed-DPI WPF host and Chromium is the supported baseline for WPF capture. On macOS/Linux, the CLI can run Figma-to-React comparison and compare any already-captured WPF artifacts. Capture PNG and JSON from the same component instance. Missing data is recorded as `NotEvaluated`; policy may make it blocking. The Figma import performs network acquisition only; `compare`, `compare-all`, and `compare-images` remain offline.

Known limits: rendering must use controlled fonts/themes; alignment is intentionally translation-only; browser/WPF captures need a live host process; image-only comparison detects visual differences but cannot identify semantic causes such as an incorrect icon resource or exact font-family mismatch.

If Playwright's browser install fails with a local issuer error, configure the organization-approved CA for Node/npm and rerun `npm.cmd exec playwright -- install chromium`; do not disable TLS validation.
