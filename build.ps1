param(
    [string]$GamePath = $env:VINTAGE_STORY,
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$journalRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($GamePath) -or -not (Test-Path -LiteralPath (Join-Path $GamePath 'VintagestoryAPI.dll'))) {
    throw 'Pass -GamePath pointing to Vintage Story 1.22.7, or set VINTAGE_STORY.'
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$journalMetadata = Get-Content -LiteralPath (Join-Path $journalRoot 'modinfo.json') -Raw | ConvertFrom-Json
[xml]$journalProject = Get-Content -LiteralPath (Join-Path $journalRoot 'BetterJournal.csproj') -Raw
$journalVersion = $journalMetadata.version
if ($journalVersion -ne [string]$journalProject.Project.PropertyGroup.Version) { throw 'Project and mod versions differ.' }
& $DotnetPath build (Join-Path $journalRoot 'BetterJournal.csproj') -c Release -t:Rebuild --nologo "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
& $DotnetPath run --project (Join-Path $journalRoot 'tests\BetterJournal.Tests.csproj') -c Release "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Compatibility checks failed.' }
$journalInputs = @(
    (Join-Path $journalRoot 'bin\Release\net10.0\BetterJournal.dll'),
    (Join-Path $journalRoot 'modinfo.json'),
    (Join-Path $journalRoot 'modicon.png'),
    (Join-Path $journalRoot 'assets'),
    (Join-Path $journalRoot 'LICENSE'),
    (Join-Path $journalRoot 'THIRD_PARTY_NOTICES.md')
)
foreach ($journalInput in $journalInputs) {
    if (-not (Test-Path -LiteralPath $journalInput)) { throw "Missing package input: $journalInput" }
}
$journalDist = Join-Path $journalRoot 'dist'
New-Item -ItemType Directory -Path $journalDist -Force | Out-Null
$journalZip = Join-Path $journalDist "$($journalMetadata.modid)-$journalVersion.zip"
Compress-Archive -LiteralPath $journalInputs -DestinationPath $journalZip -Force
$journalHash = (Get-FileHash -LiteralPath $journalZip -Algorithm SHA256).Hash.ToLowerInvariant()
$journalChecksum = "$journalHash  $([IO.Path]::GetFileName($journalZip))" + [Environment]::NewLine
[IO.File]::WriteAllText($journalZip + '.sha256', $journalChecksum, [Text.UTF8Encoding]::new($false))
Write-Output "Packaged mod: $journalZip"
Write-Output "Checksum: $journalZip.sha256"
