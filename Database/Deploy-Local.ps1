[CmdletBinding()]
param(
    [string]$Server = 'localhost\SQLEXPRESS',
    [string]$Database = 'MediaNormalizer',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$SqlPackagePath
)

$ErrorActionPreference = 'Stop'

$databaseRoot = Split-Path -Parent $PSScriptRoot
$sqlProject = Join-Path $databaseRoot 'Database\MediaNormalizer.Database.sqlproj'
$dacpac = Join-Path $databaseRoot "Database\bin\$Configuration\MediaNormalizer.Database.dacpac"

if ([string]::IsNullOrWhiteSpace($SqlPackagePath)) {
    $sqlPackageCandidates = @()
    $command = Get-Command SqlPackage.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $sqlPackageCandidates += $command.Source
    }

    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not [string]::IsNullOrWhiteSpace($root)) {
            $sqlPackageCandidates += Get-ChildItem -Path (Join-Path $root 'Microsoft Visual Studio\*\*\Common7\IDE\Extensions\Microsoft\SQLDB\DAC\SqlPackage.exe') -File -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName
        }
    }

    $SqlPackagePath = $sqlPackageCandidates |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($SqlPackagePath) -or -not (Test-Path -LiteralPath $SqlPackagePath)) {
    throw 'SqlPackage was not found. Install Visual Studio SQL database tooling or pass -SqlPackagePath with the executable path.'
}

Write-Host "Building $sqlProject ($Configuration)..."
& dotnet build $sqlProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "The database project build failed with exit code $LASTEXITCODE."
}

$targetConnectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;Connection Timeout=10"

Write-Host "Publishing $dacpac to '$Server' as database '$Database'..."
$sqlPackageArguments = @(
    '/Action:Publish',
    "/SourceFile:$dacpac",
    "/TargetConnectionString:$targetConnectionString",
    '/p:CreateNewDatabase=True',
    '/p:BlockOnPossibleDataLoss=True'
)
& $SqlPackagePath @sqlPackageArguments

if ($LASTEXITCODE -ne 0) {
    throw "The local database deployment failed with exit code $LASTEXITCODE."
}

Write-Host "Local database '$Database' is ready on '$Server'."
