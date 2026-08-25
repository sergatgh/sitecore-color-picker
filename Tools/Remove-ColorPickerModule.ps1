<#
.SYNOPSIS
    Scans for, and optionally removes, everything the SoftConsulting.ColorPicker module
    provisions - the Sitecore PowerShell Extensions (SPE) equivalent of the 4-step wizard at
    /sitecore/admin/ColorPickerUninstall.aspx.

.DESCRIPTION
    This script has no logic of its own - every line below either prints or calls straight into
    IColorPickerUsageScanner / IColorPickerUninstaller, resolved from Sitecore's own dependency
    injection container. It is the same pair of services the admin page's API
    (ColorPickerUninstallApi.ashx) uses, so the two entry points can never drift out of sync.

    Run this from the SPE Integrated Script Editor (ISE) - it needs the same in-process access to
    Sitecore's DI container the admin page gets from running inside the website itself.

.PARAMETER Scan
    Prints the usage report (Color Picker-typed fields, swatch-validator references, custom
    palettes). This also happens by default with no switches at all - pass it only for clarity.

.PARAMETER ConvertFields
    Converts every template field currently typed "Color Picker" to "Single-Line Text" (lossless
    - the field only ever stores a plain hex string), then re-scans and reprints the report.

.PARAMETER DetachValidatorReferences
    Detaches the "Color Picker Swatch" validator from every field that currently references it,
    leaving any other validators assigned to those fields untouched.

.PARAMETER DeleteCustomPalettes
    Deletes every non-seed Palette-template item the scan finds. Without this, those items are
    left in place and the Palette template is kept alongside them so they are never orphaned.

.PARAMETER Remove
    Prints the removal manifest (what will be deleted, and whether the Palette template will be
    kept), then removes the module's Sitecore items - refuses only if template fields are still
    typed "Color Picker" (combine with -ConvertFields to clear that first).

.EXAMPLE
    .\Remove-ColorPickerModule.ps1 -Scan

.EXAMPLE
    .\Remove-ColorPickerModule.ps1 -ConvertFields -DetachValidatorReferences -DeleteCustomPalettes -Remove
#>
param(
    [switch]$Scan,
    [switch]$ConvertFields,
    [switch]$DetachValidatorReferences,
    [switch]$DeleteCustomPalettes,
    [switch]$Remove
)

$scanner = [Sitecore.DependencyInjection.ServiceLocator]::ServiceProvider.GetService([SoftConsulting.ColorPicker.Uninstall.IColorPickerUsageScanner])
$uninstaller = [Sitecore.DependencyInjection.ServiceLocator]::ServiceProvider.GetService([SoftConsulting.ColorPicker.Uninstall.IColorPickerUninstaller])

if (-not $scanner -or -not $uninstaller) {
    Write-Error "Could not resolve IColorPickerUsageScanner/IColorPickerUninstaller from Sitecore's DI container - is SoftConsulting.ColorPicker installed?"
    return
}

function Write-ColorPickerReport {
    param($report)

    Write-Host ""
    Write-Host "Template fields typed 'Color Picker':" -ForegroundColor Cyan
    if ($report.ColorPickerTypedFields.Count -eq 0) {
        Write-Host "  None - safe to remove the field type." -ForegroundColor Green
    }
    else {
        $report.ColorPickerTypedFields | ForEach-Object {
            Write-Host ("  [{0}] {1} ({2}.{3})" -f $_.DatabaseName, $_.ItemPath, $_.TemplateName, $_.FieldName)
        }
    }

    Write-Host ""
    Write-Host "Fields using the 'Color Picker Swatch' validator (informational only - removed regardless):" -ForegroundColor Cyan
    if ($report.SwatchValidatorFields.Count -eq 0) {
        Write-Host "  None." -ForegroundColor Green
    }
    else {
        $report.SwatchValidatorFields | ForEach-Object {
            Write-Host ("  [{0}] {1} ({2}.{3})" -f $_.DatabaseName, $_.ItemPath, $_.TemplateName, $_.FieldName)
        }
    }

    Write-Host ""
    Write-Host "Custom Palette items (not one of the two seeded samples):" -ForegroundColor Cyan
    if ($report.CustomPalettes.Count -eq 0) {
        Write-Host "  None - safe to remove the Palette template." -ForegroundColor Green
    }
    else {
        $report.CustomPalettes | ForEach-Object {
            Write-Host ("  [{0}] {1} ({2})" -f $_.DatabaseName, $_.ItemPath, $_.Name)
        }
    }
}

function Write-BulkResult {
    param($label, $result)

    if ($result.Failures.Count -eq 0) {
        Write-Host ("{0}: {1} item(s)." -f $label, $result.Count) -ForegroundColor Green
    }
    else {
        Write-Host ("{0}: {1} item(s); {2} had a problem:" -f $label, $result.Count, $result.Failures.Count) -ForegroundColor Yellow
        $result.Failures | ForEach-Object { Write-Host "  $_" }
    }
}

$report = $scanner.Scan()
Write-ColorPickerReport $report

if ($ConvertFields) {
    if ($report.ColorPickerTypedFields.Count -eq 0) {
        Write-Host "`nNothing to convert." -ForegroundColor Green
    }
    else {
        Write-BulkResult "`nConverted" ($uninstaller.ConvertAllFieldsToSingleLineText())
        $report = $scanner.Scan()
        Write-ColorPickerReport $report
    }
}

if ($DetachValidatorReferences) {
    if ($report.SwatchValidatorFields.Count -eq 0) {
        Write-Host "`nNo validator references to detach." -ForegroundColor Green
    }
    else {
        Write-BulkResult "`nDetached" ($uninstaller.RemoveAllValidatorReferences())
        $report = $scanner.Scan()
    }
}

if ($DeleteCustomPalettes) {
    if ($report.CustomPalettes.Count -eq 0) {
        Write-Host "`nNo custom palettes to delete." -ForegroundColor Green
    }
    else {
        Write-BulkResult "`nDeleted" ($uninstaller.RemoveAllPaletteItems())
        $report = $scanner.Scan()
    }
}

if ($Remove) {
    $manifest = $uninstaller.GetRemovalManifest()
    Write-Host "`nItems that will be removed:" -ForegroundColor Cyan
    $manifest.ItemPathsToRemove | ForEach-Object { Write-Host "  $_" }
    if ($manifest.WillKeepPaletteTemplate) {
        Write-Host $manifest.KeepPaletteTemplateReason -ForegroundColor Yellow
    }

    if ($report.ColorPickerTypedFields.Count -gt 0) {
        Write-Warning "Cannot remove: template fields are still typed 'Color Picker'. Re-run with -ConvertFields first, or combine -ConvertFields -Remove."
    }
    else {
        $result = $uninstaller.RemoveModuleItems()
        if ($result.Success) {
            Write-Host ("`nRemoved {0} item(s)." -f $result.DeletedPaths.Count) -ForegroundColor Green
        }
        else {
            Write-Host ("`nRemoval finished with issues: {0}" -f $result.Message) -ForegroundColor Yellow
        }

        Write-Host "`nManual cleanup - delete these by hand, then recycle the application pool:" -ForegroundColor Cyan
        $uninstaller.GetManualCleanupFiles() | ForEach-Object { Write-Host "  $_" }
    }
}

if (-not $Scan -and -not $ConvertFields -and -not $DetachValidatorReferences -and -not $DeleteCustomPalettes -and -not $Remove) {
    Write-Host "`nPass -ConvertFields, -DetachValidatorReferences, and/or -DeleteCustomPalettes to clear each check, and -Remove to remove the module's items." -ForegroundColor DarkGray
}
