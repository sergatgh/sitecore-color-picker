namespace SoftConsulting.ColorPicker.Fields
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Shared hex-color utilities for the Color Picker field type.
    /// </summary>
    public static class ColorPickerUtils
    {
        /// <summary>
        /// Compiled pattern that matches a 6-digit hex color with an optional 2-digit alpha
        /// suffix and an optional leading "#". Accepts "#RRGGBB", "RRGGBB", "#RRGGBBAA" and
        /// "RRGGBBAA", case-insensitive.
        /// </summary>
        public static readonly Regex HexPattern =
            new Regex(@"^#?[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);

        /// <summary>
        /// Returns true when <paramref name="value"/> is a valid 6- or 8-digit hex color
        /// ("#RRGGBB"/"RRGGBB" or "#RRGGBBAA"/"RRGGBBAA", case-insensitive).
        /// </summary>
        public static bool IsValidHex(string value)
            => !string.IsNullOrEmpty(value) && HexPattern.IsMatch(value);

        /// <summary>
        /// Normalizes <paramref name="value"/> to upper-case "#RRGGBB" (opaque) or
        /// "#RRGGBBAA" (alpha other than fully opaque). An explicit "FF" alpha suffix is
        /// stripped so a fully-opaque color always normalizes to the 6-digit form, whether it
        /// was supplied with or without one - this keeps every opaque color, regardless of how
        /// it was written, comparing equal (e.g. for swatch-selection highlighting and the
        /// swatch validator's exact-match check). Returns null when <paramref name="value"/> is
        /// null, empty, or not a valid hex color.
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

        /// <summary>
        /// Returns the 6-digit "#RRGGBB" portion of a normalized 6- or 8-digit hex color,
        /// discarding any alpha suffix. <paramref name="value"/> is expected to already be
        /// normalized (e.g. via <see cref="NormalizeHex"/>); returns null for a null/empty
        /// value.
        /// </summary>
        public static string GetRgbPortion(string value)
            => string.IsNullOrEmpty(value) ? null : value.Substring(0, 7);
    }
}
