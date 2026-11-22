param([Parameter(Mandatory)][string]$SdkSource)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src/SnipLite.ScrollCapture'
$output = Join-Path $PSScriptRoot 'artifacts'
$manifest = Get-Content -LiteralPath (Join-Path $project 'plugin.json') -Raw | ConvertFrom-Json
dotnet build (Join-Path $project 'SnipLite.ScrollCapture.csproj') -c Release --source (Resolve-Path -LiteralPath $SdkSource).Path --nologo
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$build = Join-Path $project 'bin/Release/net10.0-windows'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$package = Join-Path $output "$($manifest.id)-$($manifest.version).snipplugin"
$temporary = Join-Path $output ('.' + [Guid]::NewGuid().ToString('N') + '.tmp')
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $zip = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in Get-ChildItem -LiteralPath $build -File -Recurse) {
            if ($entry.Extension -eq '.pdb' -or $entry.Name -eq 'SnipLite.Plugin.Abstractions.dll') { continue }
            $relative = [IO.Path]::GetRelativePath($build, $entry.FullName).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.FullName, $relative) | Out-Null
        }
    } finally { $zip.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $package -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Get-FileHash -LiteralPath $package -Algorithm SHA256
