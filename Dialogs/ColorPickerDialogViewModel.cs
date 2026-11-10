namespace SoftConsulting.ColorPicker.Dialogs
{
    using System.Collections.Generic;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>
    /// Model for Views\ColorPicker\Dialog.cshtml. Strings hold raw values; Razor encodes them.
    /// </summary>
    public class ColorPickerDialogViewModel
    {
        public string Value { get; set; }

        /// <summary>Id of the input the dialog script writes the picked color into.</summary>
        public string ValueInputId { get; set; }

        public bool AllowTransparency { get; set; }

        public bool AllowCustomColor { get; set; } = true;

        public List<ColorPickerSwatchViewModel> Swatches { get; set; } = new List<ColorPickerSwatchViewModel>();
    }
}
