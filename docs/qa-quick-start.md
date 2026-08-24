# Visual QA screenshot comparison — QA quick start

## What this package does

Compare an approved reference screenshot with a screenshot from the implementation. The result is an offline HTML report that highlights visual differences.

This QA package supports **image-only comparison**. It does not require Figma JSON, WPF metadata, or a running application.

## Before comparing

Use screenshots with the same:

- component crop and dimensions;
- DPI/scale factor;
- theme and background;
- fonts;
- test data and UI state.

Choose an approved PNG as the reference. Do not replace approved reference images without design/QA review.

## Run a comparison

Open PowerShell in the folder containing `VisualQa.Cli.exe` and run:

```powershell
.\VisualQa.Cli.exe compare-images `
  --reference "C:\QA\references\PatientInfo-approved.png" `
  --actual "C:\QA\screenshots\PatientInfo-current.png" `
  --output "C:\QA\results\PatientInfo" `
  --config ".\visualqa.json"
```

Use quotes around paths that contain spaces.

## Read the result

Open this file after every run:

```text
<output folder>\report.html
```

It contains the final result and visual evidence:

- `PASS` — within allowed deviation.
- `WARNING` — small difference; review it.
- `FAIL` — difference exceeds allowed deviation.
- `overlay.png` — differences highlighted over the reference.
- `heatmap.png` — difference intensity.
- `diff-regions.png` — meaningful mismatch areas.

## Exit code for automation

- `0` — pass or warning.
- `1` — fail.
- `2` — incorrect command, missing input, or configuration/system problem.

## Scope and escalation

This tool says whether two screenshots differ beyond the shared policy. It does not prove the root cause, such as the exact font, margin, or icon resource that is wrong.

If the result is a warning or fail, attach `report.html`, `overlay.png`, and `diff-regions.png` to the defect or notify the responsible designer/developer.

For the complete command reference, see `cli-user-manual.md` in the package.
