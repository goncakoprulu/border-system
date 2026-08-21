[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "output"))
$publishRoot = [IO.Path]::GetFullPath((Join-Path $outputRoot "border-system-plesk-win-x64"))
$zipPath = [IO.Path]::GetFullPath((Join-Path $outputRoot "border-system-plesk-win-x64.zip"))
$checksumPath = "$zipPath.sha256"

function Assert-ProductionOutputPath([string]$Path, [string]$ExpectedLeaf) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $outputRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $fullPath -Leaf) -ne $ExpectedLeaf) {
        throw "Unsafe production output path: $fullPath"
    }
    return $fullPath
}

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$publishRoot = Assert-ProductionOutputPath $publishRoot "border-system-plesk-win-x64"
$zipPath = Assert-ProductionOutputPath $zipPath "border-system-plesk-win-x64.zip"
$checksumPath = Assert-ProductionOutputPath $checksumPath "border-system-plesk-win-x64.zip.sha256"

foreach ($target in @($publishRoot, $zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}

Push-Location (Join-Path $repositoryRoot "apps\web")
try {
    Invoke-Checked "npm" @("ci")
    Invoke-Checked "npm" @("run", "lint")
    Invoke-Checked "npm" @("run", "build")
} finally {
    Pop-Location
}

Invoke-Checked "dotnet" @("restore", (Join-Path $repositoryRoot "Border.slnx"))
Invoke-Checked "dotnet" @("test", (Join-Path $repositoryRoot "Border.slnx"), "--configuration", "Release", "--no-restore")
Invoke-Checked "dotnet" @(
    "publish", (Join-Path $repositoryRoot "src\Border.Api\Border.Api.csproj"),
    "--configuration", "Release", "--runtime", "win-x64", "--self-contained", "true",
    "--output", $publishRoot, "-p:DebugType=None", "-p:DebugSymbols=false"
)

$webRoot = Join-Path $publishRoot "wwwroot"
New-Item -ItemType Directory -Force -Path $webRoot | Out-Null
Copy-Item -Path (Join-Path $repositoryRoot "apps\web\out\*") -Destination $webRoot -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $publishRoot "App_Data\DataProtectionKeys") | Out-Null

Get-ChildItem -LiteralPath $publishRoot -File |
    Where-Object { $_.Name -like "appsettings.Development*.json" -or $_.Name -like "appsettings.Testing*.json" } |
    Remove-Item -Force
Get-ChildItem -LiteralPath $publishRoot -Recurse -File |
    Where-Object { $_.Extension -in @(".pdb", ".map") } |
    Remove-Item -Force
$forbiddenFiles = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object {
    $_.Name -eq ".env" -or $_.Name -like ".env.*" -or
    $_.Name -like "appsettings.Development*.json" -or $_.Name -like "appsettings.Testing*.json" -or
    $_.FullName -like "*plesk-probe*"
})
if ($forbiddenFiles.Count -gt 0) {
    throw "Forbidden deployment files found: $($forbiddenFiles.FullName -join ', ')"
}

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
"$hash *$(Split-Path $zipPath -Leaf)" | Set-Content -LiteralPath $checksumPath -Encoding ascii

[pscustomobject]@{
    ZipPath = $zipPath
    ZipBytes = (Get-Item -LiteralPath $zipPath).Length
    Sha256 = $hash
    StartupFile = "Border.Api.exe"
}
