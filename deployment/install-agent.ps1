<#
.SYNOPSIS
  FileMonitoring Agent'ni o'rnatish/yangilash/o'chirish skripti.
  Administrator sifatida ishga tushirilishi shart.

.EXAMPLE
  .\install-agent.ps1 -ServerUrl "https://filemon.company.local" -EnrollmentToken "XXXX"
  .\install-agent.ps1 -Uninstall
#>
param(
  [string]$ServerUrl,
  [string]$EnrollmentToken,
  [string]$InstallDir = "$env:ProgramFiles\FileMonitoringAgent",
  [switch]$Uninstall
)

$ServiceName = "FileMonitoringAgent"
$ErrorActionPreference = "Stop"

function Assert-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $p = New-Object Security.Principal.WindowsPrincipal($id)
  if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ushbu skript Administrator sifatida ishga tushirilishi kerak."
  }
}

Assert-Admin

if ($Uninstall) {
  Write-Host "Agent o'chirilmoqda..."
  sc.exe stop $ServiceName | Out-Null
  sc.exe delete $ServiceName | Out-Null
  Get-ScheduledTask -TaskName "FileMonitoringAgentNotifier" -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false
  Remove-Item -Recurse -Force $InstallDir -ErrorAction SilentlyContinue
  Write-Host "Agent o'chirildi. (ProgramData\FileMonitoringAgent ichidagi lokal navbat qo'lda o'chiriladi.)"
  exit 0
}

if (-not $ServerUrl -or -not $EnrollmentToken) { throw "-ServerUrl va -EnrollmentToken majburiy." }

Write-Host "1/5: Kataloglar tayyorlanmoqda..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

Write-Host "2/5: Fayllar nusxalanmoqda..."
# Ushbu skript ../agent/publish papkasidagi self-contained build'ni kutadi (README'ga qarang).
$publishDir = Join-Path $PSScriptRoot "..\agent\publish"
Copy-Item -Recurse -Force "$publishDir\*" $InstallDir

Write-Host "3/5: Konfiguratsiya yozilmoqda..."
$cfgPath = Join-Path $InstallDir "appsettings.Production.json"
@{
  Agent = @{ ServerUrl = $ServerUrl; EnrollmentToken = $EnrollmentToken; AllowInsecureHttp = $false }
} | ConvertTo-Json -Depth 5 | Set-Content -Path $cfgPath -Encoding UTF8

Write-Host "4/5: Windows Service o'rnatilmoqda..."
$exe = Join-Path $InstallDir "FileMonitoring.Agent.exe"
sc.exe create $ServiceName binPath= "`"$exe`"" start= auto DisplayName= "File Monitoring Agent" | Out-Null
sc.exe description $ServiceName "Korxona fayl monitoring agenti. Faqat metadata yuboradi, fayl mazmunini o'qimaydi." | Out-Null
sc.exe start $ServiceName | Out-Null

Write-Host "5/5: Toast yordamchisi (logon'da ishga tushadi) ro'yxatdan o'tkazilmoqda..."
$notifierExe = Join-Path $InstallDir "FileMonitoring.Agent.Notifier.exe"
$action = New-ScheduledTaskAction -Execute $notifierExe
$trigger = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -GroupId "Users" -RunLevel Limited
Register-ScheduledTask -TaskName "FileMonitoringAgentNotifier" -Action $action -Trigger $trigger -Principal $principal -Force | Out-Null

Write-Host "O'rnatish tugadi. Service holati:"
Get-Service $ServiceName
