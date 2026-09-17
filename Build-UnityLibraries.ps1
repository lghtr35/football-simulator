[CmdletBinding()]
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")

$simulationRoot = $PSScriptRoot
$unityPluginDirectory = Join-Path $simulationRoot ".\Build"
dotnet build (Join-Path $simulationRoot "Football.Simulation.sln") --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
New-Item -ItemType Directory -Force -Path $unityPluginDirectory | Out-Null
Get-ChildItem -Path (Join-Path $simulationRoot "src") -Directory |
    Where-Object { $_.Name -notin @("Football.Simulation.Cli", "Football.Simulation.Persistence.Sqlite") } |
    ForEach-Object {
        $assemblyPath = Join-Path $_.FullName ("bin\" + $Configuration + "\netstandard2.1\" + $_.Name + ".dll")
        Copy-Item -LiteralPath $assemblyPath -Destination $unityPluginDirectory -Force -ErrorAction Stop
    }
Write-Host "Imported simulation DLLs into $unityPluginDirectory"
