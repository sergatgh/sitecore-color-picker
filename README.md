# Color Picker Field

A custom Sitecore Content Editor field that lets an author pick a color via a native browser
color picker, or by typing a hex value directly. Transparency editing (an alpha slider and
8-digit hex entry) is opt-in per field via `Transparency=true` in the field's **Source** - see
below; by default the field is a plain opaque color picker. When enabled, the field value is
stored as `#RRGGBB` (opaque) or `#RRGGBBAA` (when alpha is less than fully opaque).

Supports Sitecore XP **9.2–10.4** (.NET Framework **4.7.1**, matching Sitecore 9.2's own minimum
framework) — compiled against the Sitecore 9.2 API baseline (the module only uses long-stable
Kernel APIs, plus `Sitecore.DependencyInjection`, which requires Sitecore 8.2 Update-6/9.0+ and is
fully standard from 9.2 on) and validated on 10.4.1.

## How it works

- **`Fields/ColorPicker.cs`** - the field control, deriving from
  `Sitecore.Shell.Applications.ContentEditor.Text`. Renders a native `<input type="color">`
  (clicking it opens the browser/OS color dialog), a hex textbox, and a preview swatch, all kept
  in sync via inline, self-contained JavaScript (no shared `<script>` block, since the Content
  Editor frequently injects field markup via `innerHTML`, which never executes injected
  `<script>` tags). When the field's Source sets `Transparency=true` (see below), an
  alpha/transparency slider and a checkerboard-backed preview are added, and the hex textbox
  accepts 8-digit `#RRGGBBAA` values; otherwise the field only ever edits/stores plain 6-digit
  opaque colors. The native color input is an HTML platform element that cannot represent alpha
  at all, so it is always wired to the RGB portion only; when transparency is enabled, its
  `onchange` handler is careful to preserve whatever alpha is already stored rather than
  resetting it to opaque.

  The markup, all presentation logic (per-swatch css class / border style) and the inline
  onclick/onchange script-building all live in **`Views/ColorPicker/Field.cshtml`** (the script
  builders are declared in a Razor `@functions` block in that file). `ColorPicker.cs` only
  gathers raw data into a `ColorPickerViewModel` (`Fields/ColorPickerViewModel.cs` - id, value,
  and each swatch's name/hex/selected flag, nothing presentation-related) and renders the view to
  a string via **`Infrastructure/RazorViewRenderer.cs`**. Since this control runs inside the
  Sitecore Content Editor's Sheer/WebForms pipeline rather than a real MVC request, there's no
  controller action to return a view from - `RazorViewRenderer` renders the `.cshtml` directly
  through `System.Web.Mvc`'s Razor view machinery, using a minimal faked `ControllerContext` built
  from the real `HttpContext.Current` (always present, since `DoRender` runs during an actual
  ASP.NET postback). This introduces a runtime dependency on `System.Web.Mvc.dll` being present in
  the deployed site's bin (previously this control had none) - if it's missing, the field falls
  back to the plain textbox below (caught by `DoRender`'s existing try/catch) with a logged error,
  rather than crashing.

  It also supports an optional swatch palette, and an optional override of the rendering view
  itself, both driven by the field's **Source** value and parsed by
  **`Fields/ColorPickerSourceParser.cs`**. Source is zero or more pipe (`|`)-separated
  `Key=Value` segments (keys are case-insensitive):

  ```
  Palette=/sitecore/settings/color-picker/palette1|Palette=[Solid Green]:#11FF11;[Green]:#00FF00;Red:#FF0000|View=/Views/Shared/MyColor.cshtml|Transparency=true
  ```

  - **`Transparency`**: `true` (or `1`) shows the alpha/transparency slider and allows 8-digit
    `#RRGGBBAA` hex entry; omitted, or any other value, keeps the field a plain opaque color
    picker (6-digit hex only) - this is the default.
  - **`Palette`** (repeatable, merges): either the path/ID of a **Palette** item (based on the
    `/sitecore/templates/Foundation/Color Picker/Palette` template - see below) whose **`Colors`**
    field holds the swatches, or an inline custom color list. Every `Palette` segment's swatches
    are appended in order, so multiple palettes - item-based, inline, or a mix - combine into one
    palette.
  - Custom color list syntax: semicolon-separated `Name:RRGGBB` (optionally `Name:RRGGBBAA` with
    an alpha suffix) entries, e.g.
    `[Solid Green]:#11FF11;[Green]:#00FF00;Red:#FF0000;Faded Blue:#0000FF80`. `:` divides the name
    (left) from the color (right). Wrapping the name in `[...]` lets it contain `:` itself; without brackets the
    name must not contain `:` - such an entry is ambiguous and is skipped, same as an entry
    whose color half doesn't parse as a hex color.
  - **`View`**: a virtual path to a Razor view that overrides the field's default rendering view
    (`~/Views/ColorPicker/Field.cshtml`) - use this to render the palette/picker your own way.
    The last `View` segment wins if more than one is given.
  - Any other `Key=Value` segment is treated as one legacy literal swatch (`Name=RRGGBB`), for
    backward compatibility with the field's original Source format.
  - If Source has neither `|` nor `=` at all, it's treated as a single `Palette` value (an item
    path/ID, or an inline custom color list) - e.g. Source can just be
    `/sitecore/settings/color-picker/palette1` or `Red:#FF0000;Green:#00FF00`.

  Malformed or unresolvable pieces (a `Palette` item that doesn't exist, an unparsable color, an
  ambiguous name) are skipped individually rather than failing the whole Source. **Known
  limitation:** in this Sitecore instance's Content Editor theme the swatch buttons don't
  currently render visibly even though the markup is emitted correctly (likely a CSS interaction
  with the surrounding chrome) - the native picker and hex textbox are fully functional
  regardless.

- **`Processors/InitializeColorPickerFieldType.cs`** - an idempotent, self-healing `initialize`
  pipeline processor. This is the **permanent provisioning mechanism** for this module (no
  Sitecore Content Serialization / package step needed): on every application start it ensures
  `/sitecore/system/Field types/Custom Field Types/Color Picker` exists in the `core` database
  with the right `Control`/`Assembly`/`Class`/icon values, creating or repairing it as needed.

- **`Processors/InitializeColorPickerPalette.cs`** - another idempotent, self-healing `initialize`
  pipeline processor, running in the content database (e.g. `master`). It provisions:
  - The **`Palette`** template at `/sitecore/templates/Foundation/Color Picker/Palette`, with a
    single **`Colors`** field of type **"Name Value List"** - each name/value pair in that field
    is one swatch (`name` -> `RRGGBB`), read via `Sitecore.Data.Fields.NameValueListField` (the
    same API Sitecore's own field editor uses) rather than by hand-parsing the field's raw,
    URL-encoded value.
  - A **`Color Picker`** folder at `/sitecore/system/Modules/Color Picker`, with a **`Palettes`**
    subfolder (leaving room for other kinds of items under `Color Picker` later) containing two
    ready-made Palette items - **Material Design** and **Flat UI Colors** - so a fresh instance
    has usable palettes out of the box. These are only created once; later edits an author makes
    to them are left alone on subsequent app starts.

- **`App_Config/Include/SoftConsulting.ColorPicker.Fields.config`** - registers the
  `custom:ColorPicker` control tag (`controlSources`, with an explicit `prefix`) so Sitecore's
  Sheer/XML control resolver can instantiate the field control, plus a `<fieldTypes>` entry
  mirroring how built-in types register their data-API field wrapper.

- **`App_Config/Include/SoftConsulting.ColorPicker.Initialize.config`** - patches the bootstrap
  processor into the `<initialize>` pipeline.

## Deploying

Build and publish the project (the included `FolderProfile` publish profile targets a local
`C:\inetpub\wwwroot\<site>` folder) and recycle the application. On the next app start, the
field type item is created automatically - no manual Content Editor setup, package install, or
serialization push required.

## Using the field

1. In the Template Editor (master database), add a field and set its **Type** to **"Color
   Picker"** (listed under the "Custom Field Types" category).
2. Optionally set **Source** to a palette string - see the Source grammar above, e.g.
   `Palette=Brand Blue:#0052CC;Brand Red:#DE350B` or a path to a Palette item, e.g.
   `Palette=/sitecore/system/Modules/Color Picker/Palettes/Material Design` (a couple of these are
   provisioned automatically - see `InitializeColorPickerPalette.cs` above - or create your own
   under the `Palette` template, filling in the **`Colors`** Name Value List field with one
   `name` = `RRGGBB` pair per swatch). Add `Transparency=true` to the Source if this field should
   also support transparency - it's off by default.
3. Open an item based on that template - the field renders a color swatch button (native picker)
   and a hex textbox; either updates the field value, which persists on Save. If `Transparency`
   is enabled, an alpha slider is also shown, and the preview swatch gets a checkerboard backdrop
   so transparency is visible.

## Uninstalling

This module has no Sitecore package/installer to reverse - every item it provisions
(`InitializeColorPickerFieldType.cs`, `InitializeSwatchValidator.cs`,
`InitializeColorPickerPalette.cs` above) is created idempotently on app start, so removing it
safely needs its own tool rather than an anti-package or a manual item hunt.

Open **`/sitecore/admin/ColorPickerUninstall.aspx`** as a Sitecore administrator (any other user
gets a 403 from both the page and the JSON API behind it - neither depends on any Sitecore item,
only this authentication check, so the tool can never end up "half removed" the way a Control
Panel button that points at a deleted item could). It's a 4-step wizard, each step scanning and
letting you act one row (or all rows) at a time before you move on:

1. **Template fields typed "Color Picker"** - removing the field type while any of these exist
   would leave them unable to resolve their editor. **Convert** (per field, or all at once)
   bulk-changes Type to "Single-Line Text" - lossless, since the field only ever stores a plain
   hex string (`#RRGGBB`/`#RRGGBBAA`).
2. **Fields using the "Color Picker Swatch" validator** - detected via Sitecore's own **Link
   Database** (`Sitecore.Globals.LinkDatabase.GetReferrers`), the same index Sitecore's UI uses
   for "this item is referenced elsewhere". Not blocking - the validator item is removed in step
   4 regardless - but **Remove link** (per field, or all at once) detaches just that reference,
   leaving any other validators assigned to the same field untouched. *If this step looks
   incomplete, the Link Database may be stale - rebuild it via Control Panel → Indexing → Rebuild
   Link Database, then re-scan.*
3. **Custom Palette items** - items based on the `Palette` template other than the two seeded
   samples. Deleting the `Palette` template while these exist would orphan them, so it's kept in
   place unless you remove them here first (per item, or all at once).
4. **Preview and removal** - a manifest of exactly what will be deleted (and, if custom palettes
   were left in step 3, a note that the `Palette` template will be kept so they aren't orphaned),
   then a confirmation prompt before the actual, bottom-up, idempotent removal (mirroring how the
   bootstrap processors create things top-down): the field type item and its category folder in
   `core`; the swatch validator item and its folder; the two sample Palette items and the
   module's `Palettes`/`Color Picker` folders; and, unless custom palettes were left in place,
   the `Colors` field, `Data` section, `Palette` template, and their folders (including
   `Foundation`, but only if nothing else lives under it).

What it **cannot** do is delete the module's own files - `SoftConsulting.ColorPicker.dll`, the
`App_Config\Modules\ColorPicker` includes, `Views\ColorPicker`, and the admin
page/API/assets themselves are all loaded/held open by the very application process serving the
removal request, so nothing running inside that process can delete them. Once step 4 finishes,
the page prints the exact file paths (computed at request time, not hardcoded) to delete by hand,
followed by an application pool recycle.

Each Content Editor link in the wizard uses the classic deep-link format
(`/sitecore/shell/Applications/Content Editor.aspx?sc_bw=1&id=...&db=...`) - stable across recent
Sitecore versions, but worth confirming against your own instance if a link doesn't behave as
expected.

**Sitecore PowerShell Extensions**: `Tools/Remove-ColorPickerModule.ps1` is a thin wrapper around
the same two services (`IColorPickerUsageScanner`/`IColorPickerUninstaller`), for anyone who'd
rather drive the scan/convert/detach/remove flow from the SPE ISE than the admin page - see the
script's own header comment for its `-Scan`/`-ConvertFields`/`-DetachValidatorReferences`/
`-DeleteCustomPalettes`/`-Remove` switches.
