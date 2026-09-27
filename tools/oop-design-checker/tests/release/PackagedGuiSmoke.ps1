param(
    [Parameter(Mandatory = $true)]
    [string] $Gui,
    [Parameter(Mandatory = $true)]
    [string] $ExpectedVersion
)

$ErrorActionPreference = "Stop"
$Gui = (Resolve-Path $Gui).Path

$reportedVersion = (& $Gui --version).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "GUI --version failed with exit code $LASTEXITCODE."
}
if ($reportedVersion -ne $ExpectedVersion) {
    throw "GUI version mismatch. Expected '$ExpectedVersion', found '$reportedVersion'."
}

if ($IsLinux) {
    $xvfbRun = Get-Command xvfb-run -ErrorAction Stop
    $process = Start-Process -FilePath $xvfbRun.Source -ArgumentList @("-a", $Gui) -PassThru
}
else {
    $process = Start-Process -FilePath $Gui -PassThru
}

try {
    Start-Sleep -Seconds 5
    if ($process.HasExited) {
        throw "GUI process exited during startup smoke with code $($process.ExitCode)."
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

Write-Host "GUI startup smoke passed."
