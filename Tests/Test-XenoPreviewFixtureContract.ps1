param(
    [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixture = Join-Path $root 'Developer\XenoPreview.TestFixture'
$about = Get-Content -Raw -LiteralPath (Join-Path $fixture 'About\About.xml')
$project = Get-Content -Raw -LiteralPath (Join-Path $fixture 'Source\XenoPreview.TestFixture.csproj')
$actions = Get-Content -Raw -LiteralPath (Join-Path $fixture 'Source\XenoPreviewDebugActions.cs')

foreach ($version in @('1.4', '1.5', '1.6'))
{
    if ($about -notmatch "<li>$([regex]::Escape($version))</li>")
    {
        throw "Fixture About.xml does not declare $version."
    }
    if ($project -notmatch [regex]::Escape($version))
    {
        throw "Fixture project does not declare exact configuration $version."
    }
}

if ($actions -notmatch '#error XenoPreview fixture must define exactly one')
{
    throw 'Fixture source is missing its exact-version preprocessor guard.'
}
if ($about -notmatch 'Developer-only' -or $about -notmatch 'Never include')
{
    throw 'Fixture metadata does not declare developer-only/non-shipping intent.'
}

if ($PackageRoot)
{
    $package = [System.IO.Path]::GetFullPath($PackageRoot)
    if (Test-Path -LiteralPath (Join-Path $package 'Developer'))
    {
        throw "Shipping package contains Developer: $package"
    }
    if (Get-ChildItem -LiteralPath $package -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like '*TestFixture*' })
    {
        throw "Shipping package contains fixture artifacts: $package"
    }
}

Write-Output 'XenoPreview fixture contract valid: exact versions 1.4, 1.5, 1.6; developer-only; shipping exclusion asserted.'
