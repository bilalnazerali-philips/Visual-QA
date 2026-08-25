# QA release packaging automation

`scripts/New-QARelease.ps1` creates the self-contained Windows QA package used for image-only Visual QA checks. It is suitable for a Windows CI job and produces:

```text
qa-release/VisualQa-QA-win-x64.zip
```

The ZIP contains `VisualQa.Cli.exe`, the default `visualqa.json`, QA documentation, a sample reference/actual image pair, and a generated example report. QA users do not need a .NET SDK to run the packaged executable.

## Local build

From the repository root in PowerShell:

```powershell
.\scripts\New-QARelease.ps1
```

The script restores, builds, runs the cross-platform test suite, publishes a self-contained single-file `win-x64` CLI, validates that published executable using the bundled example, and creates the ZIP.

For a local iteration where tests have already been run, use:

```powershell
.\scripts\New-QARelease.ps1 -SkipTests
```

`-SkipTests` is intended only for local iteration, not for a release CI job.

## CI usage

Run this on a Windows agent with the .NET 8 SDK installed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\New-QARelease.ps1
```

Publish `qa-release/VisualQa-QA-win-x64.zip` as the CI artifact. A non-zero script result means restore, build, test, package validation, or ZIP creation failed.

This repository also includes [`.github/workflows/qa-release.yml`](../.github/workflows/qa-release.yml). It runs the same script on `windows-latest` after every push to `main` and can be started manually. GitHub Actions publishes the ZIP as the `VisualQa-QA-win-x64` workflow artifact.

The package is Windows x64 because it is intended for QA users running the supplied executable. The source CLI and image-only comparison engine can also be built on macOS and Linux from `VisualQa.CrossPlatform.sln`; create platform-specific packages separately when required.

## macOS package

On macOS, install the .NET 8 SDK and run PowerShell (`pwsh`), then use:

```powershell
./scripts/New-QARelease-macOS.ps1
```

This creates `qa-release/VisualQa-QA-osx-arm64.zip`, suitable for Apple Silicon Macs. For an Intel Mac, use:

```powershell
./scripts/New-QARelease-macOS.ps1 -Runtime osx-x64
```

The packaged executable is `VisualQa.Cli` (no `.exe` suffix). Run it from Terminal with `./VisualQa.Cli compare-images ...`. macOS packages support the cross-platform image-only workflow; WPF capture remains Windows-only.

The [macOS workflow](../.github/workflows/qa-release-macos.yml) validates and publishes an Intel-compatible `osx-x64` package on GitHub-hosted macOS runners.
