param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Requires .NET Framework 4.8 developer compiler (csc.exe).' }
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot '映贴.exe' }
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Core.dll', ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')), ('/out:' + $OutputPath))
$sources = Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $OutputPath
