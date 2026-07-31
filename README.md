# Color Picker Field

A custom Sitecore Content Editor field that lets an author pick a color via a native browser
color picker or by typing a hex value directly. The field value is stored as `#RRGGBB`.

Targets Sitecore XP **10.4.1** (.NET Framework 4.8.1).

## How it works

- **`Fields/ColorPicker.cs`** - the field control, deriving from
  `Sitecore.Shell.Applications.ContentEditor.Text`. Renders a native `<input type="color">`
  (clicking it opens the browser/OS color dialog) plus a hex textbox kept in sync via inline,
  self-contained JavaScript (no shared `<script>` block, since the Content Editor frequently
  injects field markup via `innerHTML`, which never executes injected `<script>` tags).

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
  Palette=/sitecore/settings/color-picker/palette1|Palette=[Solid Green]:#11FF11;[Green]:#00FF00;Red:#FF0000|View=/Views/Shared/MyColor.cshtml
  ```

  - **`Palette`** (repeatable, merges): either the path/ID of a **Palette** item (based on the
    `/sitecore/templates/Foundation/Color Picker/Palette` template - see below) whose **`Colors`**
    field holds the swatches, or an inline custom color list. Every `Palette` segment's swatches
    are appended in order, so multiple palettes - item-based, inline, or a mix - combine into one
    palette.
  - Custom color list syntax: semicolon-separated `Name:RRGGBB` entries, e.g.
    `[Solid Green]:#11FF11;[Green]:#00FF00;Red:#FF0000`. `:` divides the name (left) from the
    color (right). Wrapping the name in `[...]` lets it contain `:` itself; without brackets the
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
   `name` = `RRGGBB` pair per swatch).
3. Open an item based on that template - the field renders a color swatch button (native picker)
   and a hex textbox; either one updates the field value, which persists on Save.
