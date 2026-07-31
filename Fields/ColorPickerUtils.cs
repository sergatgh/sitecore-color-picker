namespace SoftConsulting.ColorPicker.Fields
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Shared hex-color utilities for the Color Picker field type.
    /// </summary>
    public static class ColorPickerUtils
    {
        /// <summary>
        /// Compiled pattern that matches a 6-digit hex color with an optional leading "#".
        /// Accepts both "#RRGGBB" and "RRGGBB", case-insensitive.
        /// </summary>
        public static readonly Regex HexPattern =
            new Regex(@"^#?[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        /// <summary>
        /// Returns true when <paramref name="value"/> is a valid 6-digit hex color
        /// ("#RRGGBB" or "RRGGBB", case-insensitive).
        /// </summary>
        public static bool IsValidHex(string value)
            => !string.IsNullOrEmpty(value) && HexPattern.IsMatch(value);

        /// <summary>
        /// Normalizes <paramref name="value"/> to upper-case "#RRGGBB".
        /// Returns null when <paramref name="value"/> is null, empty, or not a valid hex color.
        /// </summary>
        public static string NormalizeHex(string value)
        {
            if (!IsValidHex(value))
            {
                return null;
            }

            var hex = value.StartsWith("#", System.StringComparison.Ordinal) ? value : "#" + value;
            return hex.ToUpperInvariant();
        }
    }
}
