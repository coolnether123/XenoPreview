param(
    [Parameter(Mandatory = $true)][ValidateSet('1.4', '1.5', '1.6')][string]$Version,
    [Parameter(Mandatory = $true)][string]$RimWorldManagedDir,
    [Parameter(Mandatory = $true)][string]$XenoPreviewAssembly,
    [Parameter(Mandatory = $true)][string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$project = Join-Path $repository 'Developer\XenoPreview.TestFixture\Source\XenoPreview.TestFixture.csproj'
$managed = [System.IO.Path]::GetFullPath($RimWorldManagedDir)
$shipping = [System.IO.Path]::GetFullPath($XenoPreviewAssembly)
$output = [System.IO.Path]::GetFullPath($OutputRoot)

if (-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll') -PathType Leaf))
{
    throw "Managed runtime is not valid: $managed"
}
if (-not (Test-Path -LiteralPath $shipping -PathType Leaf))
{
    throw "Shipping XenoPreview assembly is not valid: $shipping"
}

[System.IO.Directory]::CreateDirectory($output) | Out-Null
$resultPath = Join-Path $output "fixture-$Version-build.json"
$buildOutput = Join-Path $output "build-$Version"
$intermediate = Join-Path $output "obj-$Version"

$arguments = @(
    'build', $project,
    '--configuration', $Version,
    '--property', ('RimWorldManagedDir=' + $managed),
    '--property', ('XenoPreviewAssembly=' + $shipping),
    '--property', ('OutputPath=' + ($buildOutput + '\')),
    '--property', ('BaseIntermediateOutputPath=' + ($intermediate + '\')),
    '--property', ('IntermediateOutputPath=' + ($intermediate + '\'))
)

& dotnet @arguments | Out-File -LiteralPath $resultPath -Encoding utf8
if ($LASTEXITCODE -ne 0)
{
    throw "XenoPreview fixture build failed for $Version. See $resultPath"
}

$assembly = Join-Path $buildOutput 'XenoPreview.TestFixture.dll'
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf))
{
    throw "Fixture assembly was not produced: $assembly"
}

[pscustomobject]@{
    Version = $Version
    Assembly = $assembly
    Sha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    ManagedDir = $managed
    ShippingAssembly = $shipping
    BuildOutput = $resultPath
} | ConvertTo-Json -Depth 4
