namespace SoftConsulting.ColorPicker.Fields
{
    using System.Text.RegularExpressions;

    public static class ColorPickerUtils
    {
        public static readonly Regex HexPattern =
            new Regex(@"^#?[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);

        public static bool IsValidHex(string value)
            => !string.IsNullOrEmpty(value) && HexPattern.IsMatch(value);

        /// <summary>
        /// Upper-case "#RRGGBB" or "#RRGGBBAA", or null if invalid. An "FF" alpha is stripped so
        /// every opaque color has a single canonical form.
        /// </summary>
        public static string NormalizeHex(string value)
        {
            if (!IsValidHex(value))
            {
                return null;
            }

            var hex = value.StartsWith("#", System.StringComparison.Ordinal) ? value : "#" + value;
            hex = hex.ToUpperInvariant();

            if (hex.Length == 9 && hex.Substring(7, 2) == "FF")
            {
                hex = hex.Substring(0, 7);
            }

            return hex;
        }

        /// <summary>Expects an already normalized value.</summary>
        public static string GetRgbPortion(string value)
            => string.IsNullOrEmpty(value) ? null : value.Substring(0, 7);
    }
}
