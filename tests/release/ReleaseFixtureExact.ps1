param(
    [Parameter(Mandatory = $true)]
    [string] $Checker,
    [Parameter(Mandatory = $true)]
    [string] $FixtureRoot,
    [Parameter(Mandatory = $true)]
    [string] $ArtifactRoot
)

$ErrorActionPreference = "Stop"
$Checker = (Resolve-Path $Checker).Path
$FixtureRoot = (Resolve-Path $FixtureRoot).Path
New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null
$ArtifactRoot = (Resolve-Path $ArtifactRoot).Path

function Invoke-CheckerJson {
    param(
        [string] $Target,
        [string] $OutputPath,
        [string[]] $ExtraArgs = @()
    )

    & $Checker $Target --format json --output $OutputPath --fail-on danger @ExtraArgs
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 2) {
        throw "Checker runtime failure for target '$Target'."
    }

    return $exitCode
}

function Normalize-JsonDiagnostics {
    param(
        [object] $Document,
        [string] $Root
    )

    return @(
        $Document.diagnostics |
            ForEach-Object {
                [pscustomobject]@{
                    ruleId = $_.ruleId
                    severity = $_.severity
                    file = [IO.Path]::GetRelativePath($Root, $_.file).Replace("\\", "/")
                    symbol = $_.symbol
                    line = [int]$_.line
                    column = [int]$_.column
                }
            } |
            Sort-Object file, line, column, ruleId, symbol
    )
}

function Diagnostic-Key {
    param([object] $Item)

    return "$($Item.file)|$($Item.line)|$($Item.column)|$($Item.ruleId)|$($Item.symbol)"
}

function Compare-ExactDiagnostics {
    param(
        [object[]] $Expected,
        [object[]] $Actual,
        [string] $DiffPath
    )

    $messages = [System.Collections.Generic.List[string]]::new()

    $duplicates = @(
        $Actual |
            Group-Object { Diagnostic-Key $_ } |
            Where-Object Count -gt 1
    )
    if ($duplicates.Count -gt 0) {
        $messages.Add("duplicate findings:")
        foreach ($duplicate in $duplicates) {
            $messages.Add("  $($duplicate.Name) x$($duplicate.Count)")
        }
    }

    $expectedByKey = @{}
    foreach ($item in $Expected) {
        $expectedByKey[(Diagnostic-Key $item)] = $item
    }

    $actualByKey = @{}
    foreach ($item in $Actual) {
        $actualByKey[(Diagnostic-Key $item)] = $item
    }

    $missing = @($expectedByKey.Keys | Where-Object { -not $actualByKey.ContainsKey($_) } | Sort-Object)
    if ($missing.Count -gt 0) {
        $messages.Add("missing expected findings:")
        foreach ($key in $missing) {
            $messages.Add("  $key")
        }
    }

    $unexpected = @($actualByKey.Keys | Where-Object { -not $expectedByKey.ContainsKey($_) } | Sort-Object)
    if ($unexpected.Count -gt 0) {
        $messages.Add("unexpected findings:")
        foreach ($key in $unexpected) {
            $messages.Add("  $key")
        }
    }

    $common = @($expectedByKey.Keys | Where-Object { $actualByKey.ContainsKey($_) })
    $severityMismatch = @(
        foreach ($key in $common) {
            if ($expectedByKey[$key].severity -ne $actualByKey[$key].severity) {
                [pscustomobject]@{
                    key = $key
                    expected = $expectedByKey[$key].severity
                    actual = $actualByKey[$key].severity
                }
            }
        }
    )
    if ($severityMismatch.Count -gt 0) {
        $messages.Add("mismatched severity:")
        foreach ($item in $severityMismatch) {
            $messages.Add("  $($item.key): expected=$($item.expected) actual=$($item.actual)")
        }
    }

    $messages | Set-Content -Path $DiffPath
    if ($messages.Count -gt 0) {
        throw "Release fixture exact comparison failed. See $DiffPath"
    }
}

function Assert-Boundaries {
    param(
        [object[]] $Actual,
        [object[]] $Expectations
    )

    foreach ($expectation in $Expectations) {
        $violations = @(
            $Actual |
                Where-Object {
                    $_.file -eq $expectation.file -and
                    $_.ruleId -in @($expectation.mustNotContain)
                }
        )
        if ($violations.Count -gt 0) {
            $found = $violations | ForEach-Object { "$($_.ruleId)@$($_.line):$($_.column)" }
            throw "Boundary '$($expectation.scenario)' produced forbidden diagnostics: $($found -join ', ')"
        }
    }
}

function Assert-RuleCountsEqual {
    param(
        [object[]] $Expected,
        [object[]] $Actual,
        [string] $Context
    )

    $expectedCounts = $Expected | Group-Object ruleId | ForEach-Object { "$($_.Name):$($_.Count)" } | Sort-Object
    $actualCounts = $Actual | Group-Object ruleId | ForEach-Object { "$($_.Name):$($_.Count)" } | Sort-Object
    if (($expectedCounts -join "|") -ne ($actualCounts -join "|")) {
        throw "$Context rule counts differ. Expected [$($expectedCounts -join ', ')], actual [$($actualCounts -join ', ')]."
    }
}

$manifestPath = Join-Path $FixtureRoot "expected-diagnostics.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$expected = @($manifest.exactDiagnostics)

$canonicalPath = Join-Path $ArtifactRoot "actual-diagnostics.json"
$canonicalExit = Invoke-CheckerJson -Target $FixtureRoot -OutputPath $canonicalPath
if ($canonicalExit -ne 1) {
    throw "Full release fixture should fail at danger threshold with exit code 1, found $canonicalExit."
}
$canonicalDocument = Get-Content $canonicalPath -Raw | ConvertFrom-Json
$actual = Normalize-JsonDiagnostics -Document $canonicalDocument -Root $FixtureRoot
Compare-ExactDiagnostics -Expected $expected -Actual $actual -DiffPath (Join-Path $ArtifactRoot "diagnostic-diff.txt")
Assert-Boundaries -Actual $actual -Expectations @($manifest.boundaryExpectations)

$sarifPath = Join-Path $ArtifactRoot "actual-diagnostics.sarif"
& $Checker $FixtureRoot --format sarif --output $sarifPath --fail-on danger
if ($LASTEXITCODE -ne 1) {
    throw "SARIF release fixture expected exit code 1, found $LASTEXITCODE."
}
$sarif = Get-Content $sarifPath -Raw | ConvertFrom-Json
$sarifItems = @(
    $sarif.runs[0].results |
        ForEach-Object {
            [pscustomobject]@{
                ruleId = $_.ruleId
            }
        }
)
Assert-RuleCountsEqual -Expected $actual -Actual $sarifItems -Context "SARIF"

$textPath = Join-Path $ArtifactRoot "actual-diagnostics.txt"
& $Checker $FixtureRoot --format text --output $textPath --fail-on danger
if ($LASTEXITCODE -ne 1) {
    throw "Text release fixture expected exit code 1, found $LASTEXITCODE."
}
$textRuleIds = @(
    [regex]::Matches((Get-Content $textPath -Raw), "OOP\d{3}") |
        ForEach-Object { [pscustomobject]@{ ruleId = $_.Value } }
)
Assert-RuleCountsEqual -Expected $actual -Actual $textRuleIds -Context "text"

$entryRoot = Join-Path $ArtifactRoot "entrypoints"
New-Item -ItemType Directory -Force -Path $entryRoot | Out-Null
Copy-Item $FixtureRoot $entryRoot -Recurse -Force
$entryFixture = Join-Path $entryRoot (Split-Path $FixtureRoot -Leaf)
$entryProject = Join-Path $entryFixture "ReleaseValidation.csproj"

$slnxPath = Join-Path $entryFixture "ReleaseValidation.slnx"
Set-Content -Path $slnxPath -Value @"
<Solution>
  <Project Path="ReleaseValidation.csproj" />
</Solution>
"@

Push-Location $entryFixture
try {
    dotnet new sln --format sln --name ReleaseValidation --force | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet new sln failed." }
    dotnet sln ReleaseValidation.sln add ReleaseValidation.csproj | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet sln add failed." }
}
finally {
    Pop-Location
}

foreach ($entrypoint in @(
    $entryFixture,
    $entryProject,
    $slnxPath,
    (Join-Path $entryFixture "ReleaseValidation.sln")
)) {
    $name = [IO.Path]::GetFileName($entrypoint)
    if ([string]::IsNullOrWhiteSpace($name)) { $name = "folder" }
    $path = Join-Path $ArtifactRoot ("entry-" + ($name -replace '[^A-Za-z0-9._-]', '_') + ".json")
    $exit = Invoke-CheckerJson -Target $entrypoint -OutputPath $path
    if ($exit -ne 1) {
        throw "Entrypoint '$entrypoint' expected exit code 1, found $exit."
    }
    $document = Get-Content $path -Raw | ConvertFrom-Json
    $normalized = Normalize-JsonDiagnostics -Document $document -Root $entryFixture
    Compare-ExactDiagnostics -Expected $expected -Actual $normalized -DiffPath (Join-Path $ArtifactRoot "entrypoint-diff.txt")
}

$configRoot = Join-Path $ArtifactRoot "config-cases"
New-Item -ItemType Directory -Force -Path $configRoot | Out-Null
Copy-Item $FixtureRoot $configRoot -Recurse -Force
$configFixture = Join-Path $configRoot (Split-Path $FixtureRoot -Leaf)

Set-Content -Path (Join-Path $configFixture "oop-design-checker.json") -Value @"
{
  "disabledRules": ["OOP105"]
}
"@
$disabledPath = Join-Path $ArtifactRoot "disabled-rule.json"
Invoke-CheckerJson -Target $configFixture -OutputPath $disabledPath | Out-Null
$disabled = Get-Content $disabledPath -Raw | ConvertFrom-Json
if (@($disabled.diagnostics | Where-Object ruleId -eq "OOP105").Count -ne 0) {
    throw "disabledRules did not suppress OOP105."
}

Set-Content -Path (Join-Path $configFixture "oop-design-checker.json") -Value @"
{
  "ignoredPaths": ["**/Positive/Operation.cs"]
}
"@
$ignoredPath = Join-Path $ArtifactRoot "ignored-path.json"
Invoke-CheckerJson -Target $configFixture -OutputPath $ignoredPath | Out-Null
$ignored = Get-Content $ignoredPath -Raw | ConvertFrom-Json
if (@($ignored.diagnostics | Where-Object { [IO.Path]::GetFileName($_.file) -eq "Operation.cs" }).Count -ne 0) {
    throw "ignoredPaths did not exclude Positive/Operation.cs."
}

Set-Content -Path (Join-Path $configFixture "oop-design-checker.json") -Value @"
{
  "ruleSettings": {
    "oop304": {
      "warningDepth": 10
    }
  }
}
"@
$settingsPath = Join-Path $ArtifactRoot "rule-settings.json"
Invoke-CheckerJson -Target $configFixture -OutputPath $settingsPath | Out-Null
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
if (@($settings.diagnostics | Where-Object ruleId -eq "OOP304").Count -ne 0) {
    throw "ruleSettings.oop304.warningDepth did not suppress the depth-5 fixture diagnostic."
}

Remove-Item (Join-Path $configFixture "oop-design-checker.json") -Force
$thresholdPath = Join-Path $ArtifactRoot "threshold-warning.json"
& $Checker $configFixture --format json --output $thresholdPath --fail-on warning
if ($LASTEXITCODE -ne 1) {
    throw "warning threshold expected exit code 1, found $LASTEXITCODE."
}

Write-Host "Release fixture exact validation passed with $($actual.Count) diagnostics."
