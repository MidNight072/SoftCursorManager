$ErrorActionPreference = 'Continue'

Write-Host ''
Write-Host '=== SoftCursorManager 卸载 ==='

# 1) stop the manager process
Get-Process SoftCursorManager -ErrorAction SilentlyContinue | ForEach-Object {
  Write-Host ('stopping process ' + $_.Id)
  Stop-Process -Id $_.Id -Force
}

# 2) remove autostart value (HKCU Run)
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$v = (Get-ItemProperty -Path $run -ErrorAction SilentlyContinue).SoftCursorManager
if ($v) {
  Remove-ItemProperty -Path $run -Name 'SoftCursorManager' -ErrorAction SilentlyContinue
  Write-Host 'removed autostart entry (HKCU Run / SoftCursorManager)'
}

# 3) remove manager install dir
$mgrDir = Join-Path $env:LOCALAPPDATA 'SoftCursorManager'
if (Test-Path $mgrDir) {
  Remove-Item -Recurse -Force $mgrDir
  Write-Host ('removed ' + $mgrDir)
}

# 4) restore pointer values to Windows default (aero) scheme
$key = 'HKCU:\Control Panel\Cursors'
$cur = '%SYSTEMROOT%\Cursors\'
$map = @(
  @('Arrow', 'aero_arrow.cur'); @('Help', 'aero_helpsel.cur')
  @('AppStarting', 'aero_working.ani'); @('Wait', 'aero_busy.ani')
  @('IBeam', 'aero_ibeam.cur'); @('No', 'aero_unavail.cur')
  @('NWPen', 'aero_pen.cur'); @('SizeAll', 'aero_move.cur')
  @('SizeNESW', 'aero_nesw.cur'); @('SizeNS', 'aero_ns.cur')
  @('SizeNWSE', 'aero_nwse.cur'); @('SizeWE', 'aero_ew.cur')
  @('UpArrow', 'aero_up.cur'); @('Hand', 'aero_link.cur')
  @('crosshair', 'aero_cross.cur'); @('Crosshair', 'aero_cross.cur')
  @('precisionhair', 'aero_cross.cur')
)
foreach ($pair in $map) {
  New-ItemProperty -Path $key -Name $pair[0] -PropertyType ExpandString -Value ($cur + $pair[1]) -Force | Out-Null
}
# values the Windows default scheme does not define: remove them
foreach ($extra in @('Pin', 'Person')) {
  Remove-ItemProperty -Path $key -Name $extra -ErrorAction SilentlyContinue
}
# scheme display name back to built-in default
try {
  [Microsoft.Win32.Registry]::SetValue('HKEY_CURRENT_USER\Control Panel\Cursors', $null, 'Windows Default')
} catch { }

# 5) remove registered scheme entry
$schemeKey = 'HKEY_CURRENT_USER\Control Panel\Cursors\Schemes\ChinatsuBig'
try {
  [Microsoft.Win32.Registry]::DeleteSubKeyTree($schemeKey, $false)
  Write-Host 'removed scheme ChinatsuBig'
} catch { }

# 6) remove installed cursor files
$curDir = Join-Path $env:LOCALAPPDATA 'Cursors\ChinatsuBig'
if (Test-Path $curDir) {
  Remove-Item -Recurse -Force $curDir
  Write-Host ('removed ' + $curDir)
}

# 7) refresh cursors
$sig = '[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, ref int c, uint d);'
$u32 = Add-Type -MemberDefinition $sig -Name U32 -Namespace Win -PassThru
$v = 0
[void]$u32::SystemParametersInfo(0x57, 0, [ref]$v, 0)

Write-Host 'Done. Cursor scheme restored to Windows default.'
Write-Host 'You can now delete the package folder itself.'
Write-Host ''
