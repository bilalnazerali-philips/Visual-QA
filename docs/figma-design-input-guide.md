# Acquiring `design.json` from Figma

## Goal

For each Visual QA component, create these design-side inputs:

```text
visual-tests/<ComponentName>/design/
  reference.png
  figma-export.json
  design.json
```

- `reference.png` is the visual reference exported from Figma.
- `figma-export.json` is the local raw Figma REST file/node export.
- `design.json` is the normalized Visual QA contract used by the CLI.

Visual QA never needs a Figma token at comparison time. Export the source JSON once, keep it locally, and normalize it into the test directory.

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

## 2. Export `reference.png` in the Figma application

1. Select the target frame, not an individual child layer.
2. In the right-side **Design** panel, find **Export**.
3. Add an export setting of **PNG** at `1x`.
4. Export it as `reference.png` into `visual-tests/<ComponentName>/design/`.

Use `1x` unless the WPF capture has intentionally been configured for another matching scale. Do not export a cropped image whose bounds differ from the component frame.

## 3. Obtain `figma-export.json`

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

## 4. Normalize into `design.json`

The `VisualQa.Figma` library provides `FigmaSpecParser` and `FigmaNormalizer`. Use them from an integration utility or test host to read `figma-export.json` and serialize its `DesignComponentSpec` result as `design.json`. The repository does not yet provide a `visualqa import-figma` command.

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

## 5. Map IDs deliberately

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
