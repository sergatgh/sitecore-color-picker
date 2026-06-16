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

  It also supports an optional swatch palette driven by the field's **Source** value (format:
  `Name=RRGGBB|Name2=RRGGBB2`, e.g. `Brand Blue=0052CC|Brand Red=DE350B`). **Known limitation:**
  in this Sitecore instance's Content Editor theme the swatch buttons don't currently render
  visibly even though the markup is emitted correctly (likely a CSS interaction with the
  surrounding chrome) - the native picker and hex textbox are fully functional regardless.

- **`Processors/InitializeColorPickerFieldType.cs`** - an idempotent, self-healing `initialize`
  pipeline processor. This is the **permanent provisioning mechanism** for this module (no
  Sitecore Content Serialization / package step needed): on every application start it ensures
  `/sitecore/system/Field types/Custom Field Types/Color Picker` exists in the `core` database
  with the right `Control`/`Assembly`/`Class`/icon values, creating or repairing it as needed.

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
2. Optionally set **Source** to a palette string, e.g. `Brand Blue=0052CC|Brand Red=DE350B`.
3. Open an item based on that template - the field renders a color swatch button (native picker)
   and a hex textbox; either one updates the field value, which persists on Save.
