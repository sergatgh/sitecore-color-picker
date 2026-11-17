namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Drawing;
    using SoftConsulting.ColorPicker.Distance;

    /// <summary>
    /// Distance between the field's value and another color. All methods return null when the
    /// field is empty, and ignore alpha on both colors.
    /// </summary>
    public static class ColorPickerFieldDistanceExtensions
    {
        private static readonly IColorDistanceCalculator EuclideanRgbCalculator = new EuclideanRgbDistanceCalculator();
        private static readonly IColorDistanceCalculator Cie76Calculator = new Cie76DistanceCalculator();
        private static readonly IColorDistanceCalculator Ciede2000Calculator = new Ciede2000DistanceCalculator();

        public static double? GetDistance(this ColorPickerField field, Color color, IColorDistanceCalculator calculator)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (calculator == null)
            {
                throw new ArgumentNullException(nameof(calculator));
            }

            return field.IsEmpty ? (double?)null : calculator.GetDistance(field.ToRgb(), color);
        }

        /// <summary>Range 0 to ~441.67.</summary>
        public static double? GetEuclideanRgbDistance(this ColorPickerField field, Color color)
            => field.GetDistance(color, EuclideanRgbCalculator);

        /// <summary>Delta E; around 2.3 is a just-noticeable difference, black to white is 100.</summary>
        public static double? GetCie76Distance(this ColorPickerField field, Color color)
            => field.GetDistance(color, Cie76Calculator);

        /// <summary>Delta E; around 1 is a just-noticeable difference, black to white is 100.</summary>
        public static double? GetCiede2000Distance(this ColorPickerField field, Color color)
            => field.GetDistance(color, Ciede2000Calculator);
    }
}
