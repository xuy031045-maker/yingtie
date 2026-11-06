param([string]$OutputPath, [switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Requires .NET Framework 4.8 developer compiler (csc.exe).' }
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot '映贴.exe' }
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Core.dll', ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')), ('/out:' + $OutputPath))
$sources = Get-ChildItem -LiteralPath $projectRoot -Filter '*.cs' | Where-Object { $Test -or $_.Name -ne 'SmokeTest.cs' } | Select-Object -ExpandProperty FullName
$arguments += '/win32icon:' + (Join-Path $projectRoot 'assets\app.ico')
if ($Test) { $arguments += '/define:TESTS' }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $OutputPath
