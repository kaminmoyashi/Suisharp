$ErrorActionPreference = 'Stop'

$runningDemo = Get-Process -Name 'Suisharp.Demo' -ErrorAction SilentlyContinue
if ($runningDemo) {
    $processIds = ($runningDemo | ForEach-Object { $_.Id }) -join ', '
    throw "Suisharp.Demo is already running (PID $processIds). Stop it with Ctrl+C, then run this script again."
}

$command = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $command) {
    throw 'The .NET 10 SDK was not found. Install it and make the dotnet command available on PATH.'
}
$dotnet = $command.Source

$demoDirectory = Join-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath 'samples') -ChildPath 'Suisharp.Demo'
$project = Join-Path -Path $demoDirectory -ChildPath 'Suisharp.Demo.csproj'
$config = Join-Path -Path $PSScriptRoot -ChildPath 'NuGet.Config'
$demo = $demoDirectory

& $dotnet restore $project --configfile $config --disable-parallel
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet run --no-restore --project $demo -- --urls 'http://127.0.0.1:5080'
exit $LASTEXITCODE
