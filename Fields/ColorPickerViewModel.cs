namespace SoftConsulting.ColorPicker.Fields
{
    using System.Collections.Generic;

    /// <summary>
    /// Data handed to Views\ColorPicker\Field.cshtml. All string properties hold raw,
    /// un-encoded values - the view relies entirely on Razor's automatic HTML-encoding of
    /// "@expression" output, so pre-encoding here would double-encode. All presentation
    /// decisions (css class, border style, inline script text) are made in the view itself,
    /// not here - this model carries only data.
    /// </summary>
    public class ColorPickerViewModel
    {
        public string Id { get; set; }

        public string Value { get; set; }

        public string NativeValue { get; set; }

        /// <summary>
        /// Whether the field's Source opted into transparency editing ("Transparency=true").
        /// When false (the default), the view renders a plain opaque color picker - no alpha
        /// slider, no 8-digit hex entry.
        /// </summary>
        public bool AllowTransparency { get; set; }

        /// <summary>
        /// Whether free color picking is allowed (the default). When false ("CustomColor=false"
        /// in Source), the view omits the native color dialog and makes the hex textbox
        /// read-only, so only the palette swatches can set the value.
        /// </summary>
        public bool AllowCustomColor { get; set; } = true;

        /// <summary>
        /// Whether the current user may not edit this field (no write access to the item, item
        /// locked by someone else, field-level write denied, protected item, etc. - whatever
        /// made the Content Editor flag the field control as ReadOnly/Disabled). When true, the
        /// view renders every interactive element (swatches, native color dialog, hex textbox,
        /// alpha slider) disabled, while still showing the current value.
        /// </summary>
        public bool ReadOnly { get; set; }

        public List<ColorPickerSwatchViewModel> Swatches { get; set; } = new List<ColorPickerSwatchViewModel>();
    }

    /// <summary>
    /// One swatch button in the optional palette driven by the field's Source value.
    /// </summary>
    public class ColorPickerSwatchViewModel
    {
        public string Name { get; set; }

        public string Hex { get; set; }

        public bool Selected { get; set; }
    }
}
