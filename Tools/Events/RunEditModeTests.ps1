param(
    [string]$UnityEditor = 'G:\Unity\6000.3.10f1\Editor\Unity.exe',
    [switch]$BuildUiPrefab
)
$ErrorActionPreference = 'Stop'
$eventProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$eventLogs = Join-Path $eventProject 'Logs/EventSystemVerification'
[IO.Directory]::CreateDirectory($eventLogs) | Out-Null
if (!(Test-Path -LiteralPath $UnityEditor)) { throw "Unity editor not found: $UnityEditor" }
$eventArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $eventProject + '"'))
if ($BuildUiPrefab) {
    $eventArguments += @('-quit', '-executeMethod', 'MusicMap.Events.Editor.EventUiPrefabBuilder.BuildDefault', '-logFile', ('"' + (Join-Path $eventLogs 'ui-build.log') + '"'))
} else {
    $eventArguments += @('-runTests', '-testPlatform', 'EditMode', '-testFilter', 'MusicMap.Events.Tests', '-testResults', ('"' + (Join-Path $eventLogs 'editmode-results.xml') + '"'), '-logFile', ('"' + (Join-Path $eventLogs 'unity-tests.log') + '"'))
}
$eventProcess = Start-Process -FilePath $UnityEditor -ArgumentList $eventArguments -WindowStyle Hidden -PassThru -Wait
Write-Output "Unity exit code: $($eventProcess.ExitCode)"
if (!$BuildUiPrefab -and (Test-Path -LiteralPath (Join-Path $eventLogs 'editmode-results.xml'))) {
    [xml]$eventResults = Get-Content -LiteralPath (Join-Path $eventLogs 'editmode-results.xml') -Raw
    $eventResults.'test-run' | Select-Object result, total, passed, failed
    $eventResults.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { Write-Output $_.fullname; Write-Output $_.failure.InnerText }
}
exit $eventProcess.ExitCode
