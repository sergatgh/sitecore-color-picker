# Color Picker Field

A color picker field for the Sitecore Content Editor — pick a color with the native browser color
dialog, or type a hex value directly.

- Optional transparency support (alpha slider, 8-digit `#RRGGBBAA` hex)
- Supports Sitecore XP **10.1+**
- Available on NuGet

## Installation

### Manually

1. Download the latest Sitecore install package (`.zip`) from the
   GitHub Releases page
2. Install it the usual way, via the **Control Panel → Install a package**.

### Sitecore PowerShell

Run this from the SPE ISE to download the latest package from GitHub and install it in one step:

```powershell
$releaseUrl = "https://github.com/sergatgh/sitecore-color-picker/releases/latest/download/SoftConsulting.ColorPicker.zip"
$packagePath = Join-Path $env:TEMP "SoftConsulting.ColorPicker.zip"

Invoke-WebRequest -Uri $releaseUrl -OutFile $packagePath
Install-Package -Path $packagePath -Install
```

## Usage

Add a field of type **Color** (listed under the "Color Picker" category in the Type dropdown) in
the Template Editor, then use its **Source** (Data Source) field to control palettes,
transparency, and rendering:

| Parameter | Example | Description |
| --- | --- | --- |
| `Transparency` | `Transparency=true` | Enables the alpha slider and 8-digit `#RRGGBBAA` hex entry. Omitted (or any other value) keeps the field a plain 6-digit opaque picker — the default. |
| `Palette` (item) | `Palette=/sitecore/system/Modules/Color Picker/Palettes/Material Design` | Path or ID of a **Palette** item. Each `name`/`value` pair in its **Colors** field becomes one swatch. |
| `Palette` (inline) | `Palette=Brand Blue:#0052CC;Brand Red:#DE350B` | Semicolon-separated `Name:RRGGBB` (or `Name:RRGGBBAA`) swatches defined directly in the Source. Wrap a name in `[...]` if it needs to contain `:`. |
| `Palette` (repeated) | `Palette=palette1\|Palette=palette2` | Multiple `Palette` segments combine — swatches from each are appended in order. |
| `View` | `View=/Views/Shared/MyColor.cshtml` | Overrides the default rendering view (`~/Views/ColorPicker/Field.cshtml`). If given more than once, the last one wins. |
| Shorthand | `/sitecore/system/Modules/Color Picker/Palettes/Material Design` or `Red:#FF0000` or `Red=FF0000` | If Source has no `\|`, it's treated as a single `Palette` value. |

Combine any of the above in one Source with `|`, e.g.:

```
Palette=/sitecore/system/Modules/Color Picker/Palettes/Material Design|Palette=Faded Blue:#0000FF80|Transparency=true
```

## Uninstalling

### Admin page

Open **`/sitecore/admin/ColorPicker/Uninstall.aspx`** as a Sitecore administrator. It's a 4-step
wizard that scans for and lets you remove everything the module created or touched:

1. Convert any "Color Picker" template fields to plain text fields.
2. Detach the "Color Picker Swatch" validator from any fields using it.
3. Delete custom Palette items (your own, not the two seeded samples).
4. Lists every file the package installed, ready to delete by hand, followed by an app pool
   recycle.

### Files installed by the package

- `bin\SoftConsulting.ColorPicker.dll`
- `App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Fields.config`
- `App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Services.config`
- `Views\ColorPicker\Field.cshtml`
- `sitecore\admin\ColorPicker\Uninstall.aspx` (+ `.css`/`.js`) and `UninstallApi.ashx`
- `sitecore modules\items\core\items.core.ColorPicker.dat` and
  `sitecore modules\items\master\items.master.ColorPicker.dat`

Run as Administrator from the website root:

```powershell
$files = @(
    'bin\SoftConsulting.ColorPicker.dll',
    'App_Config\Modules\ColorPicker',
    'Views\ColorPicker',
    'sitecore\admin\ColorPicker',
    'sitecore modules\items\core\items.core.ColorPicker.dat',
    'sitecore modules\items\master\items.master.ColorPicker.dat'
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Remove-Item $file -Force -Recurse
        Write-Host "Removed $file"
    } else {
        Write-Host "Skipped $file (not found)"
    }
}
```
