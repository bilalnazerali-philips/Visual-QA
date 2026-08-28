# Acquiring `design.json` from Figma

## Goal

For each Visual QA component, create these design-side inputs:

```text
visual-tests/<ComponentName>/design/
  reference.png
  figma-source.json
  design.json
```

- `reference.png` is the visual reference exported from Figma.
- `figma-source.json` records the selected Figma file/node provenance without a token.
- `design.json` is the normalized Visual QA contract used by the CLI.

Visual QA never needs a Figma token at comparison time. The token is needed only when importing a design from Figma.

## 1. Prepare the Figma component

1. Put the component in a dedicated Figma frame at its intended final dimensions.
2. Use named layers that describe their role, for example `patient-avatar`, `patient-name`, and `patient-status`.
3. Use approved text styles, color styles, and component/icon instances rather than detached visual approximations.
4. Ensure the selected frame has the same width and height that the WPF test host will render.
5. Record the Figma file key and the node ID of the target frame from its URL.

A typical Figma URL includes both values:

```text
https://www.figma.com/design/<file-key>/<file-name>?node-id=<node-id>
```

The exact URL shape can vary by Figma client/version. The file key identifies the file; `node-id` identifies the frame or component being tested.

## 2. Import the selected component with Visual QA (recommended)

The CLI can acquire the selected node JSON and matching `1x` PNG automatically. In Figma, select the exact component/state and use **Copy link to selection**. Create `.visualqa/figma-token.txt` in the repository root, containing only your token, then run:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- import-figma `
  --url "<copied-link-to-selection>"
```

It derives the component folder from the Figma component set and selected variant, then creates `design.json`, `reference.png`, and `figma-source.json` under `visual-tests/<derived-name>/design`. It prints the exact location. Add `--keep-raw` only when troubleshooting an import; the raw response is ignored by Git.

To import several selected components/states, create `.visualqa/figma-import-urls.txt` with one copied link per line and run:

```powershell
dotnet run --project C:\Bilal\Visual-QA\src\VisualQa.Cli -- import-figma `
  --url-file .visualqa\figma-import-urls.txt
```

The batch result is recorded in `visual-tests/import-summary.json`.

## 3. Export `reference.png` in the Figma application (manual fallback)

1. Select the target frame, not an individual child layer.
2. In the right-side **Design** panel, find **Export**.
3. Add an export setting of **PNG** at `1x`.
4. Export it as `reference.png` into `visual-tests/<ComponentName>/design/`.

Use `1x` unless the WPF capture has intentionally been configured for another matching scale. Do not export a cropped image whose bounds differ from the component frame.

## 4. Obtain raw Figma JSON manually (fallback)

Figma's desktop application exports images directly, but raw design JSON is normally obtained using the Figma REST API. This is a local export step; the resulting file is checked into or stored alongside your Visual QA fixtures.

Create a Figma personal access token with read access to the file, then request either the complete file or the target node.

### Export the complete file

```bash
curl -H "X-Figma-Token: <FIGMA_TOKEN>" \
  "https://api.figma.com/v1/files/<FILE_KEY>" \
  -o visual-tests/<ComponentName>/design/figma-export.json
```

### Export only the target frame or component

```bash
curl -H "X-Figma-Token: <FIGMA_TOKEN>" \
  "https://api.figma.com/v1/files/<FILE_KEY>/nodes?ids=<NODE_ID>" \
  -o visual-tests/<ComponentName>/design/figma-export.json
```

On PowerShell, use `curl.exe` rather than the `curl` alias:

```powershell
curl.exe -H "X-Figma-Token: <FIGMA_TOKEN>" "https://api.figma.com/v1/files/<FILE_KEY>/nodes?ids=<NODE_ID>" -o "visual-tests/<ComponentName>/design/figma-export.json"
```

Keep the token in a secret manager or an environment variable. Never commit the token into source control, `design.json`, or CI logs.

## 5. Normalize into `design.json` manually (fallback)

The `VisualQa.Figma` library provides `FigmaSpecParser` and `FigmaNormalizer`. `visualqa import-figma` is the preferred path; use this direct library workflow only when an integration needs to control acquisition itself.

The normalized output has this shape:

```json
{
  "schemaVersion": 1,
  "name": "PatientInfo",
  "width": 300,
  "height": 72,
  "elements": [
    {
      "id": "patient-name",
      "name": "patient-name",
      "type": "TEXT",
      "bounds": { "x": 72, "y": 26, "width": 100, "height": 20 },
      "text": "Avery Brooks",
      "fontSize": 16,
      "fontWeight": "SemiBold"
    }
  ]
}
```

Review the result before running a comparison. The current normalizer derives initial IDs from layer names; make them match the WPF `VisualQa.Id` values. Figma node IDs and display names are not automatically equivalent to application QA IDs.

## 6. Map IDs deliberately

Use the same stable identifier on both sides:

```xml
<TextBlock qa:VisualQa.Id="patient-name" Text="{Binding PatientName}" />
```

```json
{ "id": "patient-name" }
```

For the best result, name the Figma layer `patient-name` before normalization. If naming cannot change, edit the normalized `id` field or maintain an import-time mapping owned by the test fixture.

## Final checklist

- The Figma frame and WPF render target have identical intended dimensions.
- `reference.png` is a `1x` export of that frame.
- `design.json` has `"schemaVersion": 1`.
- Design IDs match WPF `VisualQa.Id` values.
- Fonts, icon resources, colors, and test text are available in the WPF test environment.
- The Figma token is not committed or logged.

Then capture the WPF implementation into `wpf/actual.png` and `wpf/runtime.json`, and run the Visual QA CLI.
