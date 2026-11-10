namespace SoftConsulting.ColorPicker.Fields
{
    using System.Collections.Generic;

    /// <summary>
    /// Model for Views\ColorPicker\Field.cshtml. Strings hold raw values - Razor encodes them,
    /// so pre-encoding here would double-encode.
    /// </summary>
    public class ColorPickerViewModel
    {
        public string Id { get; set; }

        public string Value { get; set; }

        /// <summary>RGB-only value for the native color input, which can't hold alpha.</summary>
        public string NativeValue { get; set; }

        public bool AllowTransparency { get; set; }

        public bool AllowCustomColor { get; set; } = true;

        public bool ReadOnly { get; set; }

        public List<ColorPickerSwatchViewModel> Swatches { get; set; } = new List<ColorPickerSwatchViewModel>();
    }

    public class ColorPickerSwatchViewModel
    {
        public string Name { get; set; }

        public string Hex { get; set; }

        public bool Selected { get; set; }
    }
}
