# SoftConsulting.ColorPicker

A Sitecore XP 10.1+ Content Editor field for picking colors, with optional swatch palettes and
transparency support. This DLL adds a typed data-API on top of the field type installed by
[sitecore-color-picker](https://github.com/sergatgh/sitecore-color-picker) - the package this one
extends.

## Reading and writing values

`ColorPickerField` wraps a "Color" field the same way `Sitecore.Data.Fields.TextField` wraps a
text field, via an implicit cast from `Field`:

```csharp
using SoftConsulting.ColorPicker.Fields;

ColorPickerField color = item.Fields["Background Color"];

if (!color.IsEmpty)
{
    byte r = color.R, g = color.G, b = color.B, a = color.A;   // components, alpha 255 if opaque
    Color rgb = color.ToRgb();                                  // System.Drawing.Color, alpha-aware
    string hex = color.ToHex();                                 // "#RRGGBB" or "#RRGGBBAA"
    var (h, s, l) = color.ToHsl();
    var (c, m, y, k) = color.ToCmyk();
}

item.Editing.BeginEdit();
color.SetFromHex("#0052CC");        // validates & normalizes
color.SetFromRgb(255, 90, 0);
color.SetFromRgba(255, 90, 0, 128); // 50% alpha -> stores 8-digit form
color.SetFromHsl(210, 1.0, 0.4);
item.Editing.EndEdit();
```

See [`Fields/ColorPickerField.cs`](Fields/ColorPickerField.cs) for the full member list (`ToArgb`,
`ToDecimal`, `ToHsb`, `SetFromDecimal`, `SetFromHsb`, ...). Every method is `public`/`protected
virtual`, so it can be overridden from a derived field type.

## Overriding the rendered view

By default the field renders via `~/Views/ColorPicker/Field.cshtml`. Point it at your own Razor
view instead with the `View` parameter in the field's Source:

```
View=/Views/Shared/MyColor.cshtml
```

Your view receives a `SoftConsulting.ColorPicker.Fields.ColorPickerViewModel`:

```csharp
public class ColorPickerViewModel
{
    public string Id { get; set; }                              // hidden <input> id/name to write the value to
    public string Value { get; set; }                            // current "#RRGGBB"/"#RRGGBBAA", or ""
    public string NativeValue { get; set; }                       // RGB portion, safe for <input type="color">
    public bool AllowTransparency { get; set; }                  // Source had "Transparency=true"
    public bool AllowCustomColor { get; set; }                   // false when Source had "CustomColor=false"
    public List<ColorPickerSwatchViewModel> Swatches { get; set; } // palette, if any
}

public class ColorPickerSwatchViewModel
{
    public string Name { get; set; }
    public string Hex { get; set; }
    public bool Selected { get; set; }
}
```

Minimal custom view - a hidden field plus the native picker, no palette/alpha UI:

```razor
@model SoftConsulting.ColorPicker.Fields.ColorPickerViewModel
<input type="hidden" id="@Model.Id" name="@Model.Id" value="@Model.Value" />
<input type="color" value="@Model.NativeValue"
       onchange="document.getElementById('@Model.Id').value=this.value;
                 if (typeof scForm !== 'undefined') { scForm.modified = true; }" />
```

The stock view at [`Views/ColorPicker/Field.cshtml`](Views/ColorPicker/Field.cshtml) is a fuller
reference implementation (swatches, hex entry, alpha slider). For the complete Source grammar
(palettes, transparency) and uninstall instructions, see the
[main README](https://github.com/sergatgh/sitecore-color-picker).
