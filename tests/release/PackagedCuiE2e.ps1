param(
    [Parameter(Mandatory = $true)]
    [string] $Checker,
    [Parameter(Mandatory = $true)]
    [string] $Root,
    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion
)

$ErrorActionPreference = "Stop"
$Checker = (Resolve-Path $Checker).Path
New-Item -ItemType Directory -Force -Path $Root | Out-Null

function Assert-ExitCode {
    param(
        [int] $Expected,
        [string] $Context
    )

    if ($LASTEXITCODE -ne $Expected) {
        throw "$Context expected exit code $Expected, found $LASTEXITCODE."
    }
}

function Read-Json {
    param([string] $Path)

    if (-not (Test-Path $Path)) {
        throw "Expected output file was not created: $Path"
    }

    return Get-Content $Path -Raw | ConvertFrom-Json
}

$reportedVersion = (& $Checker --version).Trim()
Assert-ExitCode 0 "--version"
if ($reportedVersion -ne $ExpectedVersion) {
    throw "Packaged checker version mismatch. Expected $ExpectedVersion, found '$reportedVersion'."
}

$cleanProject = Join-Path $Root "clean"
New-Item -ItemType Directory -Force -Path $cleanProject | Out-Null
Set-Content -Path (Join-Path $cleanProject "Clean.csproj") -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
"@
Set-Content -Path (Join-Path $cleanProject "Sample.cs") -Value "internal sealed class Sample { private int _value; public int Value => _value; }"

$cleanJsonPath = Join-Path $Root "clean.json"
& $Checker $cleanProject --format json --output $cleanJsonPath --fail-on danger
Assert-ExitCode 0 "folder analysis without --config"
$cleanJson = Read-Json $cleanJsonPath
if ($cleanJson.version -ne 1) {
    throw "Expected JSON version 1, found '$($cleanJson.version)'."
}
if (@($cleanJson.diagnostics).Count -ne 0) {
    throw "Expected clean fixture to have no diagnostics."
}
if (
    $cleanJson.summary.danger -ne 0
    -or $cleanJson.summary.warning -ne 0
    -or $cleanJson.summary.attention -ne 0
) {
    throw "Expected clean fixture summary to contain only zero counts."
}

$dangerProject = Join-Path $Root "danger"
New-Item -ItemType Directory -Force -Path $dangerProject | Out-Null
Set-Content -Path (Join-Path $dangerProject "Danger.csproj") -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
"@
$dangerSource = Join-Path $dangerProject "Bad.cs"
Set-Content -Path $dangerSource -Value "internal sealed class Bad { public int Value; }"

$dangerJsonPath = Join-Path $Root "danger.json"
& $Checker $dangerProject --format json --output $dangerJsonPath --fail-on danger
Assert-ExitCode 1 "danger fixture"
$dangerJson = Read-Json $dangerJsonPath
$oop106 = @($dangerJson.diagnostics | Where-Object { $_.ruleId -eq "OOP106" })
if ($oop106.Count -ne 1) {
    throw "Expected exactly one OOP106 diagnostic, found $($oop106.Count)."
}
if ($oop106[0].severity -ne "danger") {
    throw "Expected OOP106 severity 'danger', found '$($oop106[0].severity)'."
}
if ($oop106[0].symbol -ne "Bad.Value") {
    throw "Expected OOP106 symbol 'Bad.Value', found '$($oop106[0].symbol)'."
}
if ([IO.Path]::GetFileName($oop106[0].file) -ne "Bad.cs") {
    throw "Expected OOP106 file Bad.cs, found '$($oop106[0].file)'."
}
if ($oop106[0].line -ne 1 -or $oop106[0].column -lt 1) {
    throw "Expected OOP106 to point at line 1 with a positive column."
}
if ($dangerJson.summary.danger -lt 1) {
    throw "Expected danger summary count to be at least 1."
}

$dangerSarifPath = Join-Path $Root "danger.sarif"
& $Checker $dangerProject --format sarif --output $dangerSarifPath --fail-on danger
Assert-ExitCode 1 "danger SARIF fixture"
$dangerSarif = Read-Json $dangerSarifPath
if ($dangerSarif.version -ne "2.1.0" -or @($dangerSarif.runs).Count -ne 1) {
    throw "Expected SARIF 2.1.0 with exactly one run."
}
$sarifOop106 = @($dangerSarif.runs[0].results | Where-Object { $_.ruleId -eq "OOP106" })
if ($sarifOop106.Count -ne 1) {
    throw "Expected exactly one OOP106 SARIF result, found $($sarifOop106.Count)."
}
if ($sarifOop106[0].level -ne "error") {
    throw "Expected OOP106 SARIF level 'error', found '$($sarifOop106[0].level)'."
}
$physicalLocation = $sarifOop106[0].locations[0].physicalLocation
if ($physicalLocation.region.startLine -ne 1 -or $physicalLocation.region.startColumn -lt 1) {
    throw "Expected OOP106 SARIF location to point at line 1 with a positive column."
}
if (-not $physicalLocation.artifactLocation.uri.EndsWith("/Bad.cs")) {
    throw "Expected OOP106 SARIF URI to point to Bad.cs."
}

$looseJsonPath = Join-Path $Root "loose.json"
& $Checker $dangerSource --format json --output $looseJsonPath --fail-on danger
Assert-ExitCode 1 "loose C# source"
$looseJson = Read-Json $looseJsonPath
if (@($looseJson.diagnostics | Where-Object { $_.ruleId -eq "OOP106" }).Count -ne 1) {
    throw "Expected loose C# analysis to report OOP106."
}

$slnxPath = Join-Path $cleanProject "Smoke.slnx"
Set-Content -Path $slnxPath -Value @"
<Solution>
  <Project Path="Clean.csproj" />
</Solution>
"@
& $Checker $slnxPath --fail-on danger
Assert-ExitCode 0 ".slnx analysis"

Push-Location $cleanProject
try {
    dotnet new sln --format sln --name Smoke --force | Out-Null
    Assert-ExitCode 0 "dotnet new sln"
    dotnet sln Smoke.sln add Clean.csproj | Out-Null
    Assert-ExitCode 0 "dotnet sln add"
}
finally {
    Pop-Location
}
& $Checker (Join-Path $cleanProject "Smoke.sln") --fail-on danger
Assert-ExitCode 0 ".sln analysis"

$configRoot = Join-Path $Root "config-auto"
$configProject = Join-Path $configRoot "app"
New-Item -ItemType Directory -Force -Path $configProject | Out-Null
Set-Content -Path (Join-Path $configRoot "oop-design-checker.json") -Value @"
{
  "disabledRules": ["OOP106"]
}
"@
Set-Content -Path (Join-Path $configProject "Configured.csproj") -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
"@
Set-Content -Path (Join-Path $configProject "Configured.cs") -Value "internal sealed class Configured { public int Value; }"
$configJsonPath = Join-Path $Root "configured.json"
& $Checker $configProject --format json --output $configJsonPath --fail-on danger
Assert-ExitCode 0 "auto-discovered configuration"
$configJson = Read-Json $configJsonPath
if (@($configJson.diagnostics | Where-Object { $_.ruleId -eq "OOP106" }).Count -ne 0) {
    throw "Auto-discovered configuration did not disable OOP106."
}

& $Checker (Join-Path $Root "missing-target")
Assert-ExitCode 2 "missing target"

exit 0
