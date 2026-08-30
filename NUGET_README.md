# Color Picker Field for Sitecore

A custom Sitecore Content Editor field that lets an author pick a color via a native browser color
picker, or by typing a hex value directly - with optional swatch palettes and optional
transparency (8-digit `#RRGGBBAA`) support. Supports Sitecore XP **9.2-10.4**.

## After installing this package

Installing drops these files into your project (alongside the compiled reference):

- `App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Fields.config`
- `App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Initialize.config.disabled`
- `Views\ColorPicker\Field.cshtml` and `Views\ColorPicker\Web.config`
- `sitecore\admin\ColorPicker\Uninstall.aspx` (+ `.css`/`.js`) and `UninstallApi.ashx`

1. **Enable the bootstrap config** - rename
   `SoftConsulting.ColorPicker.Initialize.config.disabled` to
   `SoftConsulting.ColorPicker.Fields.config`'s sibling, dropping the `.disabled` suffix (i.e.
   `SoftConsulting.ColorPicker.Initialize.config`), once you're ready for it to provision the
   field type/palettes on the next application start. It ships disabled so installing the package
   into a project never silently activates it as a side effect of a build.
2. **Publish/deploy** the project so the DLL and the files above reach your Sitecore site's web
   root.
3. **Recycle the app pool.** On the next start, `/sitecore/system/Field types/Custom Field
   Types/Color Picker` is created automatically in `core` - no package install or serialization
   push needed.
4. Add a field of type **"Color Picker"** in the Template Editor. See the main
   [README](https://github.com/) (TODO: project URL) for the full Source grammar (palettes,
   transparency, view overrides) and uninstall instructions.

## Requirements

This package intentionally declares **no NuGet dependencies** - everything below is something
every Sitecore XP 9.2-10.4 site already has in its `bin`, so installing this package won't add or
downgrade any of them. Declaring them as NuGet dependencies instead would make `Install-Package`
add its own (older, pinned) copies as Copy-Local references, which get deployed on top of - and
for unsigned assemblies like `Sitecore.Kernel`, silently override - the newer versions your site
already runs.

- `Sitecore.Kernel >= 9.2.0` and the DI container it ships (`Sitecore.DependencyInjection`), so
  any Sitecore XP **9.2 or later** instance works; this is compiled against the Sitecore 9.2 API
  baseline specifically because it's the oldest XP release still on Sitecore's support lifecycle.
- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 2.1.1) - ships with Sitecore's own
  DI container since 9.1.
- `Newtonsoft.Json` (>= 13.0.1).
- `System.Web.Mvc`/Razor/WebPages (5.2.4/3.2.9) - used only to render `Field.cshtml` server-side
  inside the Content Editor's Sheer/WebForms pipeline.
- `Microsoft.Web.Infrastructure` (>= 2.0.0) and `System.ValueTuple` (>= 4.5.0) - transitive
  dependencies of the above.

If any of these are somehow missing or older than listed on your site, update them yourself before
installing this package; don't rely on this package to bring them in.
