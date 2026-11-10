namespace SoftConsulting.ColorPicker.Dialogs
{
    using System.Collections.Generic;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>
    /// Data handed to Views\ColorPicker\Dialog.cshtml, the body of the Experience Editor's color
    /// picker dialog. Like <see cref="ColorPickerViewModel"/>, every string holds a raw value and
    /// the view relies on Razor's automatic HTML-encoding.
    /// </summary>
    public class ColorPickerDialogViewModel
    {
        /// <summary>
        /// The field's current color, normalized ("#RRGGBB"/"#RRGGBBAA"), or empty when the field
        /// has no (valid) color yet.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Id of the Sheer input the dialog's script writes the picked color into, which the
        /// dialog form reads back when OK is pressed.
        /// </summary>
        public string ValueInputId { get; set; }

        /// <summary>Whether the field's Source opted into transparency ("Transparency=true").</summary>
        public bool AllowTransparency { get; set; }

        /// <summary>
        /// Whether free color picking is allowed (the default). When false ("CustomColor=false"),
        /// the dialog only offers the palette swatches.
        /// </summary>
        public bool AllowCustomColor { get; set; } = true;

        public List<ColorPickerSwatchViewModel> Swatches { get; set; } = new List<ColorPickerSwatchViewModel>();
    }
}
