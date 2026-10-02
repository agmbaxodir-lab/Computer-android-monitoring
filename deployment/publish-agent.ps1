<#
.SYNOPSIS
  Agent va Notifier'ni Windows uchun self-contained single-file qilib build qiladi.
  install-agent.ps1 shu skriptdan chiqqan ../agent/publish papkasini kutadi.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "agent\publish"
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue

dotnet publish "$root\agent\FileMonitoring.Agent\FileMonitoring.Agent.csproj" -c Release -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw "FileMonitoring.Agent build xatosi (yuqoridagi xabarlarga qarang)" }
dotnet publish "$root\agent\FileMonitoring.Agent.Notifier\FileMonitoring.Agent.Notifier.csproj" -c Release -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw "FileMonitoring.Agent.Notifier build xatosi (yuqoridagi xabarlarga qarang)" }
if (-not (Test-Path "$out\FileMonitoring.Agent.Notifier.exe")) { throw "Notifier.exe yaratilmadi" }

Write-Host "Build tayyor: $out"
