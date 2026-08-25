[CmdletBinding()]
param(
    [string]$OutputDirectory = "qa-release",
    [ValidateSet("osx-arm64", "osx-x64")]
    [string]$Runtime = "osx-arm64",
    [switch]$SkipTests
)

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
$releaseRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
$packageRoot = Join-Path $releaseRoot "VisualQa"
$zipPath = Join-Path $releaseRoot "VisualQa-QA-$Runtime.zip"

if (-not $releaseRoot.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside the repository: $repositoryRoot"
}

Push-Location $repositoryRoot
try {
    & $dotnetPath --version
    if ($LASTEXITCODE -ne 0) { throw "The .NET SDK is unavailable." }

    Write-Host "==> Restore cross-platform solution for $Runtime"
    & $dotnetPath restore VisualQa.CrossPlatform.sln --runtime $Runtime --tl:False
    if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

    Write-Host "==> Build cross-platform solution"
    & $dotnetPath build VisualQa.CrossPlatform.sln --configuration Release --no-restore --tl:False
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

    if (-not $SkipTests) {
        Write-Host "==> Run cross-platform tests"
        & $dotnetPath test VisualQa.CrossPlatform.sln --configuration Release --no-build --tl:False
        if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
    }

    if (Test-Path -LiteralPath $packageRoot) { Remove-Item -LiteralPath $packageRoot -Recurse -Force }
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

    Write-Host "==> Publish self-contained QA CLI ($Runtime)"
    & $dotnetPath publish src/VisualQa.Cli/VisualQa.Cli.csproj --configuration Release --runtime $Runtime --self-contained true --no-restore `
        -p:PublishSingleFile=true -p:DebugType=embedded --output $packageRoot --tl:False
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

    Copy-Item -LiteralPath "visualqa.json" -Destination $packageRoot
    Copy-Item -LiteralPath "docs/qa-quick-start.md" -Destination (Join-Path $packageRoot "QA-QUICK-START.md")
    Copy-Item -LiteralPath "docs/cli-user-manual.md" -Destination (Join-Path $packageRoot "CLI-USER-MANUAL.md")
    Copy-Item -LiteralPath "docs/documentation-status.md" -Destination (Join-Path $packageRoot "DOCUMENTATION-STATUS.md")
    $exampleDirectory = Join-Path $packageRoot "example"
    New-Item -ItemType Directory -Path $exampleDirectory -Force | Out-Null
    Copy-Item -LiteralPath "visual-tests/PatientInfo/design/reference.png" -Destination (Join-Path $exampleDirectory "approved-reference.png")
    Copy-Item -LiteralPath "visual-tests/PatientInfo/wpf/actual.png" -Destination (Join-Path $exampleDirectory "example-failing-actual.png")

    $validationOutput = Join-Path $packageRoot "example-result"
    $cliPath = Join-Path $packageRoot "VisualQa.Cli"
    Write-Host "==> Validate published package with its bundled example"
    & $cliPath compare-images `
        --reference (Join-Path $exampleDirectory "approved-reference.png") `
        --actual (Join-Path $exampleDirectory "example-failing-actual.png") `
        --output $validationOutput `
        --config (Join-Path $packageRoot "visualqa.json")
    if ($LASTEXITCODE -notin @(0, 1)) { throw "Published CLI validation failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath (Join-Path $validationOutput "report.html"))) {
        throw "Published CLI validation did not create report.html."
    }

    Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "QA release created and validated: $zipPath"
}
finally {
    Pop-Location
}
