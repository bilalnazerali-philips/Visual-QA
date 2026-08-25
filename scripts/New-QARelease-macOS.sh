#!/usr/bin/env bash
set -euo pipefail

runtime="osx-arm64"
output_directory="qa-release"
skip_tests=false

usage() {
  cat <<'EOF'
Usage: bash scripts/New-QARelease-macOS.sh [options]

Options:
  --runtime <osx-arm64|osx-x64>  Target macOS architecture (default: osx-arm64)
  --output <directory>           Release output directory inside the repository (default: qa-release)
  --skip-tests                   Skip tests for local iteration only
  --help                         Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --runtime)
      runtime="${2:?A runtime is required after --runtime}"
      shift 2
      ;;
    --output)
      output_directory="${2:?A directory is required after --output}"
      shift 2
      ;;
    --skip-tests)
      skip_tests=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ "$runtime" != "osx-arm64" && "$runtime" != "osx-x64" ]]; then
  echo "Runtime must be osx-arm64 or osx-x64; received: $runtime" >&2
  exit 2
fi

command -v dotnet >/dev/null || { echo ".NET SDK is required but dotnet was not found." >&2; exit 2; }
command -v zip >/dev/null || { echo "The macOS zip command is required but was not found." >&2; exit 2; }

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_directory/.." && pwd)"

if [[ "$output_directory" = /* ]]; then
  release_root="$output_directory"
else
  release_root="$repository_root/$output_directory"
fi

# Resolve the parent without requiring GNU realpath, which macOS does not include by default.
release_parent="$(cd -- "$(dirname -- "$release_root")" 2>/dev/null && pwd)" || {
  echo "The parent of the output directory does not exist: $release_root" >&2
  exit 2
}
release_root="$release_parent/$(basename -- "$release_root")"

case "$release_root/" in
  "$repository_root/"*) ;;
  *)
    echo "Output directory must be inside the repository: $repository_root" >&2
    exit 2
    ;;
esac

package_root="$release_root/VisualQa"
zip_path="$release_root/VisualQa-QA-$runtime.zip"

cd "$repository_root"

echo "==> .NET SDK"
dotnet --version

echo "==> Restore cross-platform solution for $runtime"
dotnet restore VisualQa.CrossPlatform.sln --runtime "$runtime" --tl:False

echo "==> Build cross-platform solution"
dotnet build VisualQa.CrossPlatform.sln --configuration Release --no-restore --tl:False

if [[ "$skip_tests" == false ]]; then
  echo "==> Run cross-platform tests"
  dotnet test VisualQa.CrossPlatform.sln --configuration Release --no-build --tl:False
fi

rm -rf "$package_root"
rm -f "$zip_path"
mkdir -p "$package_root/example"

echo "==> Publish self-contained QA CLI ($runtime)"
dotnet publish src/VisualQa.Cli/VisualQa.Cli.csproj --configuration Release --runtime "$runtime" --self-contained true --no-restore \
  -p:PublishSingleFile=true -p:DebugType=embedded --output "$package_root" --tl:False

cp visualqa.json "$package_root/"
cp docs/qa-quick-start.md "$package_root/QA-QUICK-START.md"
cp docs/cli-user-manual.md "$package_root/CLI-USER-MANUAL.md"
cp docs/documentation-status.md "$package_root/DOCUMENTATION-STATUS.md"
cp visual-tests/PatientInfo/design/reference.png "$package_root/example/approved-reference.png"
cp visual-tests/PatientInfo/wpf/actual.png "$package_root/example/example-failing-actual.png"

echo "==> Validate published package with its bundled example"
set +e
"$package_root/VisualQa.Cli" compare-images \
  --reference "$package_root/example/approved-reference.png" \
  --actual "$package_root/example/example-failing-actual.png" \
  --output "$package_root/example-result" \
  --config "$package_root/visualqa.json"
comparison_exit_code=$?
set -e

# A fail result (1) is expected for this intentionally different example.
if [[ $comparison_exit_code -ne 0 && $comparison_exit_code -ne 1 ]]; then
  echo "Published CLI validation failed with exit code $comparison_exit_code." >&2
  exit "$comparison_exit_code"
fi

[[ -f "$package_root/example-result/report.html" ]] || {
  echo "Published CLI validation did not create report.html." >&2
  exit 1
}

echo "==> Create ZIP"
mkdir -p "$release_root"
(
  cd "$release_root"
  zip -rq "$(basename -- "$zip_path")" VisualQa
)

echo "QA release created and validated: $zip_path"
