# Color Picker Field

[![NuGet](https://img.shields.io/nuget/v/SoftConsulting.ColorPicker.svg?style=plastic)](https://www.nuget.org/packages/SoftConsulting.ColorPicker/)

A color picker field for the Sitecore Content Editor — pick a color with the native browser color
dialog, or type a hex value directly.

<img  alt="Sitecore Color Picker field example" src="https://github.com/user-attachments/assets/0be15d34-1fa6-4563-98dd-1703540b3315" />

- Optional transparency support (alpha slider, 8-digit `#RRGGBBAA` hex)
- Supports Sitecore XP **10.1+**
- Available on NuGet

## Installation

### Manually

1. Download the latest Sitecore install package (`.zip`) from the
   [GitHub Releases page](https://github.com/sergatgh/sitecore-color-picker/releases)
2. Install it the usual way, via the **Control Panel → Install a package**.

### Sitecore PowerShell

Run this from the SPE ISE to download the latest package from GitHub and install it in one step:

```powershell
$releaseUrl = "https://github.com/sergatgh/sitecore-color-picker/releases/download/1.0.0.0/SoftConsulting-ColorPicker-1.0.0.0.zip"
$packagePath = Join-Path $SitecoreTempFolder "SoftConsulting.ColorPicker.zip"

Invoke-WebRequest -Uri $releaseUrl -OutFile $packagePath
Install-Package -Path $packagePath -InstallMode Overwrite
```

## Usage

Add a field of type **Color** (listed under the "Color Picker" category in the Type dropdown) in
the Template Editor, then use its **Source** (Data Source) field to control palettes,
transparency, and rendering:

| Parameter | Example | Description | Image |
| --- | --- | --- | --- |
| `Transparency` | `Transparency=true` | Enables the alpha slider and 8-digit `#RRGGBBAA` hex entry. Omitted (or any other value) keeps the field a plain 6-digit opaque picker — the default. | <img width="354" height="89" alt="image" src="https://github.com/user-attachments/assets/e07451cb-c896-41a9-82e2-d80829ce4c6c" /> |
| `CustomColor` | `CustomColor=false` | Disables free color picking: the native color dialog is hidden and the hex textbox becomes read-only, so only the palette swatches can set the value. Omitted (or any value other than `false`/`0`) keeps free picking enabled — the default. If no palette swatches are configured either, the field shows a message asking for one of the two to be enabled instead (no transparency slider is drawn in that case). | No Image |
| `Palette` (item) | `Palette=/sitecore/system/Modules/Color Picker/Palettes/Material Design` | Path or ID of a **Palette** item. Each `name`/`value` pair in its **Colors** field becomes one swatch. | <img width="496" height="86" alt="image" src="https://github.com/user-attachments/assets/ef20521a-fcbe-48ff-9c07-d8cd575f13d7" /> |
| `Palette` (inline) | `Palette=Brand Blue:#0052CC;Brand Red:#DE350B` | Semicolon-separated `Name:RRGGBB` (or `Name:RRGGBBAA`) swatches defined directly in the Source. Wrap a name in `[...]` if it needs to contain `:`. | <img width="247" height="113" alt="image" src="https://github.com/user-attachments/assets/8bc3c9e3-2c53-4bf7-bf36-9e09a6cef93a" /> |
| `Palette` (repeated) | `Palette=palette1\|Palette=palette2` | Multiple `Palette` segments combine — swatches from each are appended in order. | <img width="427" height="152" alt="image" src="https://github.com/user-attachments/assets/e1a6424a-0379-48d0-b3fe-e19b3436d0f1" /> |
| `View` | `View=/Views/Shared/MyColor.cshtml` | Overrides the default rendering view (`~/Views/ColorPicker/Field.cshtml`). If given more than once, the last one wins. | No Image |
| Shorthand | `/sitecore/system/Modules/Color Picker/Palettes/Material Design` or `Red:#FF0000` or `Red=FF0000` | If Source has no `\|`, it's treated as a single `Palette` value. | <img width="220" height="90" alt="image" src="https://github.com/user-attachments/assets/5be58dba-dc99-4323-b009-05b862e6f3f4" /> |

Combine any of the above in one Source with `|`, e.g.:

```
Palette=/sitecore/system/Modules/Color Picker/Palettes/Material Design|Palette=Faded Blue:#0000FF80|Transparency=true
```

### Tips

#### Always keep a valid color in the field

The picker only ever saves valid hex colors, but an editor can still type anything into the
field's raw value (**View → Raw values**). To reject invalid values on save, open the template
field item (e.g. `/sitecore/templates/User Defined/.../Case Color`) and fill in:

**Validation**

```
^(#?[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?)?$
```

**Validation Text**

```
'$Value' is not a valid color. Use a hex value like #0052CC, or #0052CC80 with transparency.
```

**Color distance validators**

Keep a color close to a target color (e.g. a brand color), or away from one (e.g. too close to
the background). Create a **Validation Rule** item, e.g. under
`/sitecore/system/Settings/Validation Rules/Field Rules`, set its **Type** to one of
the validators below, fill in **Parameters**, then attach the item to a template field's
validation rules.

| Type | Algorithm | Scale |
| --- | --- | --- |
| `SoftConsulting.ColorPicker.Validators.Ciede2000DistanceValidator, SoftConsulting.ColorPicker` | CIEDE2000 — most accurate to human perception (recommended) | 0–100; ~1 is barely noticeable |
| `SoftConsulting.ColorPicker.Validators.Cie76DistanceValidator, SoftConsulting.ColorPicker` | CIE76 — Euclidean distance in L\*a\*b\* | 0–100+; ~2.3 is barely noticeable |
| `SoftConsulting.ColorPicker.Validators.EuclideanRgbDistanceValidator, SoftConsulting.ColorPicker` | Euclidean distance in RGB | 0–441.67 |

| Parameter | Example | Description |
| --- | --- | --- |
| `Color` | `Color=0052CC` | The color to measure against. Leave out the `#` (or write it as `%23`). |
| `Distance` | `Distance=10` | The acceptable distance, on the chosen algorithm's scale. Use `.` for decimals. |
| `Mode` | `Mode=Outside` | `Within` (default): the value must be at most `Distance` from `Color`. `Outside`: the value must be further than `Distance`, to avoid that color. |

For example, `Color=FFFFFF&Distance=15&Mode=Outside` with CIEDE2000 rejects colors that are
nearly white. Alpha is ignored. If `Color` or `Distance` is missing or invalid, the validator
logs a warning and passes. Sitecore's standard `Result` parameter (e.g. `Result=Warning`) still
controls the severity.

## Uninstalling

### Admin page

Open **`/sitecore/admin/ColorPicker/Uninstall.aspx`** as a Sitecore administrator. It's a 4-step
wizard that scans for and lets you remove everything the module created or touched:

1. Convert any "Color Picker" template fields to plain text fields.
2. Detach the module's validators from any fields using them. Every validator item whose
   **Type** comes from this module is detected: "Color Picker Swatch", color distance validators
   you created, and your own classes derived from them.
3. Delete custom Palette items (your own, not the two seeded samples).
4. Lists every file the package installed, ready to delete by hand, followed by an app pool
   recycle.

### Files installed by the package

- `bin\SoftConsulting.ColorPicker.dll`
- `App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Fields.config`
- `App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Services.config`
- `Views\ColorPicker\Field.cshtml` and `Views\ColorPicker\Dialog.cshtml`
- `sitecore modules\Shell\ColorPicker\ColorPickerDialog.xml` (+ `.css`/`.js`)
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
    'sitecore modules\Shell\ColorPicker',
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
