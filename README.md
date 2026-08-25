# Visual QA

Deterministic, explainable design-conformance checks for local Figma REST exports, WPF, and React. It does not use AI, OCR, or cloud visual analysis.

See [the high-level architecture](docs/architecture.md) for the adapter boundaries, data flow, operating-system support, and CI model.
See [the user guide](docs/user-guide.md) for setup, capture, comparison, reporting, and troubleshooting instructions.
See [the Figma design-input guide](docs/figma-design-input-guide.md) for obtaining `reference.png`, raw Figma JSON, and normalized `design.json`.
See [the CLI user manual](docs/cli-user-manual.md) for every supported command, its options, outputs, exit codes, and examples.
See [the documentation validation status](docs/documentation-status.md) for code-backed capability and limitation evidence.
See [QA release packaging automation](docs/qa-release-automation.md) to produce the self-contained Windows QA ZIP locally or in CI.

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

Windows 11 with a fixed-DPI WPF host and Chromium is the supported baseline for WPF capture. On macOS/Linux, the CLI can run Figma-to-React comparison and compare any already-captured WPF artifacts. Capture PNG and JSON from the same component instance. Missing data is recorded as `NotEvaluated`; policy may make it blocking. The Figma library parses and normalizes local REST file/node JSON; a caller must serialize the resulting contract because there is not yet an `import-figma` CLI command.

Known limits: rendering must use controlled fonts/themes; alignment is intentionally translation-only; browser/WPF captures need a live host process; image-only comparison detects visual differences but cannot identify semantic causes such as an incorrect icon resource or exact font-family mismatch.

If Playwright's browser install fails with a local issuer error, configure the organization-approved CA for Node/npm and rerun `npm.cmd exec playwright -- install chromium`; do not disable TLS validation.
