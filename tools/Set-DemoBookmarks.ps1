[CmdletBinding()]
param(
    [string]$ProgId = 'VisualStudio.DTE.18.0'
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    throw 'Run this installer in Windows PowerShell 5.1 (powershell.exe), which supports the Visual Studio COM automation API used here.'
}
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'NICEDA.sln'
$targets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'demo-bookmarks.json') -Raw | ConvertFrom-Json
$locations = @()

foreach ($target in $targets) {
    $path = Join-Path $root $target.file
    $matches = @(Select-String -LiteralPath $path -SimpleMatch -Pattern $target.anchor)
    if ($matches.Count -ne 1) {
        throw "Bookmark $($target.id): expected exactly one anchor in $($target.file), found $($matches.Count). Update demo-bookmarks.json."
    }
    $locations += [pscustomobject]@{
        Id = $target.id
        Title = $target.title
        Path = $path
        Line = $matches[0].LineNumber
    }
}

try {
    $dte = [Runtime.InteropServices.Marshal]::GetActiveObject($ProgId)
}
catch [Runtime.InteropServices.COMException] {
    throw "No running $ProgId instance is available. Open NICEDA.sln in Visual Studio first. For VS 2022, pass -ProgId VisualStudio.DTE.17.0."
}

if ($dte.Solution.FullName -ne $solution) {
    throw "The active Visual Studio solution is '$($dte.Solution.FullName)', not '$solution'. No bookmarks were changed."
}

# The native window supplies labels, which EnvDTE does not expose.
# SetBookmark can add duplicate rows even at an already-bookmarked line.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName Accessibility
Add-Type -AssemblyName System.Windows.Forms
if (-not ('NicedaBookmarkWindow' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'BookmarkWindowAccess.cs') -ReferencedAssemblies Accessibility
}
$dte.MainWindow.Activate()
$dte.ExecuteCommand('View.Bookmarks')
$windows = @(Get-Process devenv | Where-Object {
    $_.MainWindowTitle -like 'NICEDA -*' -and $_.MainWindowHandle -ne [IntPtr]::Zero
})
if ($windows.Count -ne 1) {
    throw 'Expected one NICEDA Visual Studio window. Close other NICEDA IDE instances before installing names.'
}
$ide = [System.Windows.Automation.AutomationElement]::FromHandle($windows[0].MainWindowHandle)

function Get-BookmarkTree {
    $trees = @($ide.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.ClassName -eq 'SysTreeView32' })
    if ($trees.Count -ne 1) {
        throw "Expected one native Bookmark Window tree, found $($trees.Count). Keep the Bookmark Window open."
    }
    return $trees[0].Current.NativeWindowHandle
}

function Get-BookmarkRows {
    $names = [NicedaBookmarkWindow]::Names((Get-BookmarkTree))
    for ($index = 0; $index -lt $names.Length; $index++) {
        if ($names[$index] -match '^Bookmark: (.+), File Location: (.+), Line Number: (\d+)$') {
            [pscustomobject]@{ Index = $index; Name = $Matches[1]; Path = $Matches[2]; Line = [int]$Matches[3] }
        }
    }
}

function Remove-BookmarkRow([int]$index) {
    [NicedaBookmarkWindow]::Select((Get-BookmarkTree), $index)
    $before = @(Get-BookmarkRows).Count
    $button = $ide.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object {
            $_.Current.Name -eq 'Delete' -and
            $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button
        } | Select-Object -First 1
    if (-not $button) { throw 'The Bookmark Window Delete button was not found.' }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    for ($attempt = 0; $attempt -lt 30 -and @(Get-BookmarkRows).Count -eq $before; $attempt++) {
        Start-Sleep -Milliseconds 100
    }
    if (@(Get-BookmarkRows).Count -ne $before - 1) {
        throw 'The selected duplicate bookmark was not removed.'
    }
}

function Rename-BookmarkRow($row, [string]$label) {
    $row = Get-BookmarkRows | Where-Object {
        $_.Name -eq $row.Name -and $_.Path -eq $row.Path -and $_.Line -eq $row.Line
    } | Select-Object -First 1
    if (-not $row) { throw "Bookmark row changed before renaming '$label'." }
    [NicedaBookmarkWindow]::Select((Get-BookmarkTree), $row.Index)
    $dte.ExecuteCommand('OtherContextMenus.BookmarkWindow.Rename')
    $edit = $ide.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.ClassName -eq 'Edit' -and $_.Current.Name -eq $row.Name } |
        Select-Object -First 1
    if (-not $edit -or $edit.Current.IsOffscreen -or -not $edit.Current.IsEnabled) {
        throw "Bookmark rename control is not available. No text was typed for '$label'."
    }
    $escaped = $label -replace '([+^%~(){}\[\]])', '{$1}'
    [System.Windows.Forms.SendKeys]::SendWait("^a$escaped{ENTER}")
    if (-not (Get-BookmarkRows | Where-Object { $_.Name -eq $label })) {
        throw "Could not verify native name '$label'."
    }
}

# Upgrade the unnamed markers created by the original installer.
foreach ($row in @(Get-BookmarkRows | Where-Object { $_.Name -match '^Bookmark\d+$' })) {
    $closest = $locations | Where-Object { $_.Path -eq $row.Path } |
        Sort-Object { [Math]::Abs($_.Line - $row.Line) } | Select-Object -First 1
    if ($closest) { Rename-BookmarkRow $row "$($closest.Id) - $($closest.Title)" }
}

foreach ($location in $locations) {
    $label = "$($location.Id) - $($location.Title)"
    $rows = @(Get-BookmarkRows | Where-Object {
        $_.Name -like "$($location.Id) - *" -or
        ($_.Path -eq $location.Path -and $_.Line -eq $location.Line)
    })
    # Remove only duplicate markers at this demo location, not other user bookmarks.
    foreach ($duplicate in ($rows | Select-Object -Skip 1 | Sort-Object Index -Descending)) {
        Remove-BookmarkRow $duplicate.Index
    }
    $row = Get-BookmarkRows | Where-Object {
        $_.Name -like "$($location.Id) - *" -or
        ($_.Path -eq $location.Path -and $_.Line -eq $location.Line)
    } | Select-Object -First 1
    if ($row -and ($row.Path -ne $location.Path -or $row.Line -ne $location.Line)) {
        Remove-BookmarkRow $row.Index
        $row = $null
    }
    if (-not $row) {
        $null = $dte.ItemOperations.OpenFile($location.Path)
        $dte.ActiveDocument.Selection.GotoLine($location.Line, $false)
        $dte.ActiveDocument.Selection.SetBookmark()
        $dte.ExecuteCommand('View.Bookmarks')
        $row = Get-BookmarkRows | Where-Object {
            $_.Path -eq $location.Path -and $_.Line -eq $location.Line
        } | Select-Object -First 1
    }
    if (-not $row) { throw "Native bookmark $($location.Id) could not be located." }
    if ($row.Name -ne $label) {
        Rename-BookmarkRow $row $label
        $renamed = @(Get-BookmarkRows | Where-Object { $_.Name -eq $label })
        if ($renamed.Count -ne 1) { throw "Could not verify native name '$label'." }
    }
}

$seen = @{}
$dte.ExecuteCommand('Edit.NextBookmark')
$origin = "$($dte.ActiveDocument.FullName):$($dte.ActiveDocument.Selection.ActivePoint.Line)"
$seen[$origin] = $true
$returnedToOrigin = $false
for ($i = 0; $i -lt 500; $i++) {
    $dte.ExecuteCommand('Edit.NextBookmark')
    $key = "$($dte.ActiveDocument.FullName):$($dte.ActiveDocument.Selection.ActivePoint.Line)"
    $seen[$key] = $true
    if ($key -eq $origin) {
        $returnedToOrigin = $true
        break
    }
}
if (-not $returnedToOrigin) {
    throw 'Could not verify the complete bookmark navigation cycle. Existing bookmarks were not cleared.'
}
foreach ($location in $locations) {
    if (-not $seen.ContainsKey("$($location.Path):$($location.Line)")) {
        throw "Visual Studio navigation did not find bookmark $($location.Id)."
    }
}

$null = $dte.ItemOperations.OpenFile($locations[0].Path)
$dte.ActiveDocument.Selection.GotoLine($locations[0].Line, $false)
$dte.ExecuteCommand('View.Bookmarks')
$locations | Select-Object Id, Title, Line, Path
Write-Output "Verified $($locations.Count) named demo bookmarks. Visual Studio saves them in local solution state on normal solution/IDE close. Unrelated bookmarks and unsaved edits were preserved."
