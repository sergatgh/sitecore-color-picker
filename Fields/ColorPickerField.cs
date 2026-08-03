namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Drawing;
    using Sitecore.Data.Fields;

    /// <summary>
    /// Data-API wrapper for the "Color Picker" field type. Use this in code to read and write
    /// color field values on Sitecore items, the same way TextField wraps single-line text.
    ///
    /// Values are stored and returned as upper-case "#RRGGBB" (opaque) or "#RRGGBBAA" (when
    /// alpha is not fully opaque) - see <see cref="A"/>/<see cref="IsOpaque"/>/<see cref="SetFromRgba"/>.
    /// The implicit cast from Sitecore.Data.Fields.Field follows the same pattern as Sitecore's
    /// built-in wrappers:
    ///   ColorPickerField color = item.Fields["Background Color"];
    ///
    /// Registered in SoftConsulting.ColorPicker.Fields.config:
    ///   &lt;fieldType name="Color Picker"
    ///              type="SoftConsulting.ColorPicker.Fields.ColorPickerField, SoftConsulting.ColorPicker" /&gt;
    /// </summary>
    public class ColorPickerField : CustomField
    {
        // -----------------------------------------------------------------------
        // Construction & implicit cast
        // -----------------------------------------------------------------------

        public ColorPickerField(Field innerField)
            : base(innerField)
        {
        }

        public static implicit operator ColorPickerField(Field field)
            => field != null ? new ColorPickerField(field) : null;

        // -----------------------------------------------------------------------
        // Value — normalise on every write via the protected virtual pair
        // -----------------------------------------------------------------------

        /// <summary>
        /// Normalizes the value to upper-case "#RRGGBB" or "#RRGGBBAA" before persisting.
        /// Null, empty, or invalid hex strings clear the field.
        /// </summary>
        protected override void SetValue(string value)
            => InnerField.Value = NormalizeHex(value) ?? string.Empty;

        // GetValue() is intentionally not overridden: the base returns InnerField.Value
        // directly, and every write already stores a normalized value, so reads are free.

        // -----------------------------------------------------------------------
        // Component properties
        // -----------------------------------------------------------------------

        /// <summary>True when no color has been stored (field value is null or empty).</summary>
        public bool IsEmpty => string.IsNullOrEmpty(InnerField.Value);

        /// <summary>Red channel, 0–255. Returns 0 when <see cref="IsEmpty"/>.</summary>
        public byte R => IsEmpty ? (byte)0 : Convert.ToByte(InnerField.Value.Substring(1, 2), 16);

        /// <summary>Green channel, 0–255. Returns 0 when <see cref="IsEmpty"/>.</summary>
        public byte G => IsEmpty ? (byte)0 : Convert.ToByte(InnerField.Value.Substring(3, 2), 16);

        /// <summary>Blue channel, 0–255. Returns 0 when <see cref="IsEmpty"/>.</summary>
        public byte B => IsEmpty ? (byte)0 : Convert.ToByte(InnerField.Value.Substring(5, 2), 16);

        /// <summary>
        /// Alpha channel, 0–255. 255 (fully opaque) when the stored value has no alpha suffix,
        /// or when <see cref="IsEmpty"/>.
        /// </summary>
        public virtual byte A => IsEmpty || InnerField.Value.Length < 9
            ? (byte)255
            : Convert.ToByte(InnerField.Value.Substring(7, 2), 16);

        /// <summary>True when the stored color has no alpha suffix, i.e. <see cref="A"/> == 255.</summary>
        public virtual bool IsOpaque => A == 255;

        // -----------------------------------------------------------------------
        // Conversion — read
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the stored color as a <see cref="System.Drawing.Color"/>, honoring alpha.
        /// Returns <see cref="Color.Empty"/> when <see cref="IsEmpty"/>.
        /// </summary>
        public virtual Color ToRgb()
            => IsEmpty ? Color.Empty : Color.FromArgb(A, R, G, B);

        /// <summary>
        /// Returns the 32-bit ARGB value (0xAARRGGBB), e.g. a 50%-alpha orange becomes
        /// 0x80FF5A00. Returned as <see cref="long"/> rather than <see cref="int"/> to avoid
        /// sign-bit ambiguity once the alpha byte can exceed 0x7F. Returns 0 when
        /// <see cref="IsEmpty"/>. Unlike <see cref="ToDecimal"/>, this includes alpha.
        /// </summary>
        public virtual long ToArgb()
            => IsEmpty ? 0 : ((long)A << 24) | (uint)((R << 16) | (G << 8) | B);

        /// <summary>
        /// Returns the stored "#RRGGBB" or "#RRGGBBAA" string. Equivalent to reading
        /// <see cref="CustomField.Value"/>. Returns an empty string when <see cref="IsEmpty"/>.
        /// </summary>
        public string ToHex()
            => InnerField.Value;

        /// <summary>
        /// Returns the 24-bit decimal integer (e.g. #FF5A00 → 16734720). RGB-only - alpha is
        /// not representable through this accessor, so a translucent color's alpha is silently
        /// dropped here; use <see cref="ToArgb"/> when alpha matters.
        /// Returns 0 when <see cref="IsEmpty"/>.
        /// </summary>
        public int ToDecimal()
            => IsEmpty ? 0 : (R << 16) | (G << 8) | B;

        /// <summary>
        /// Converts to CMYK. All four components are in [0, 1].
        /// Returns (0, 0, 0, 1) — black — when <see cref="IsEmpty"/>.
        /// </summary>
        public (double C, double M, double Y, double K) ToCmyk()
        {
            if (IsEmpty)
            {
                return (0, 0, 0, 1);
            }

            var r = R / 255.0;
            var g = G / 255.0;
            var b = B / 255.0;

            var k = 1.0 - Math.Max(r, Math.Max(g, b));

            if (k >= 1.0 - double.Epsilon)
            {
                return (0, 0, 0, 1);
            }

            var denom = 1.0 - k;
            return (
                C: (1.0 - r - k) / denom,
                M: (1.0 - g - k) / denom,
                Y: (1.0 - b - k) / denom,
                K: k
            );
        }

        /// <summary>
        /// Converts to HSB (= HSV). H is in [0, 360); S and B are in [0, 1].
        /// Returns (0, 0, 0) when <see cref="IsEmpty"/>.
        /// </summary>
        public (double H, double S, double B) ToHsb()
        {
            if (IsEmpty)
            {
                return (0, 0, 0);
            }

            var r = R / 255.0;
            var g = G / 255.0;
            var b = B / 255.0;

            var cMax  = Math.Max(r, Math.Max(g, b));
            var cMin  = Math.Min(r, Math.Min(g, b));
            var delta = cMax - cMin;

            return (
                H: ComputeHue(r, g, b, cMax, delta),
                S: cMax < double.Epsilon ? 0.0 : delta / cMax,
                B: cMax
            );
        }

        /// <summary>
        /// Converts to HSL. H is in [0, 360); S and L are in [0, 1].
        /// Returns (0, 0, 0) when <see cref="IsEmpty"/>.
        /// </summary>
        public (double H, double S, double L) ToHsl()
        {
            if (IsEmpty)
            {
                return (0, 0, 0);
            }

            var r = R / 255.0;
            var g = G / 255.0;
            var b = B / 255.0;

            var cMax  = Math.Max(r, Math.Max(g, b));
            var cMin  = Math.Min(r, Math.Min(g, b));
            var delta = cMax - cMin;
            var l     = (cMax + cMin) / 2.0;

            return (
                H: ComputeHue(r, g, b, cMax, delta),
                S: delta < double.Epsilon ? 0.0 : delta / (1.0 - Math.Abs(2.0 * l - 1.0)),
                L: l
            );
        }

        // -----------------------------------------------------------------------
        // Conversion — write
        // -----------------------------------------------------------------------

        /// <summary>
        /// Sets the field value from individual RGB byte components (each in [0, 255]).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Any component is outside [0, 255].</exception>
        public void SetFromRgb(int r, int g, int b)
        {
            ValidateComponent(r, nameof(r));
            ValidateComponent(g, nameof(g));
            ValidateComponent(b, nameof(b));
            InnerField.Value = $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// Sets the field value from individual RGBA byte components (each in [0, 255]).
        /// Stores the 6-digit opaque form when <paramref name="a"/> is 255, otherwise the
        /// 8-digit "#RRGGBBAA" form.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Any component is outside [0, 255].</exception>
        public virtual void SetFromRgba(int r, int g, int b, int a)
        {
            ValidateComponent(a, nameof(a));
            if (a == 255)
            {
                SetFromRgb(r, g, b);
                return;
            }

            ValidateComponent(r, nameof(r));
            ValidateComponent(g, nameof(g));
            ValidateComponent(b, nameof(b));
            InnerField.Value = $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        /// <summary>
        /// Validates and normalizes a hex string ("#RRGGBB"/"RRGGBB" or "#RRGGBBAA"/"RRGGBBAA",
        /// case-insensitive) then stores it as the field value.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="hex"/> is not a valid hex color.</exception>
        public void SetFromHex(string hex)
        {
            var normalized = NormalizeHex(hex);
            if (normalized == null)
            {
                throw new ArgumentException(
                    $"'{hex}' is not a valid hex color (#RRGGBB or #RRGGBBAA).", nameof(hex));
            }

            InnerField.Value = normalized;
        }

        /// <summary>
        /// Sets the field value from a 24-bit integer (e.g. 0xFF5A00 stores "#FF5A00").
        /// RGB-only - there is no alpha-aware overload of this accessor; use
        /// <see cref="SetFromRgba"/> when alpha matters.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorValue"/> is outside [0, 0xFFFFFF].</exception>
        public void SetFromDecimal(int colorValue)
        {
            if (colorValue < 0 || colorValue > 0xFFFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(colorValue),
                    "Color value must be in the range [0, 16777215].");
            }

            InnerField.Value = string.Format("#{0:X2}{1:X2}{2:X2}",
                (colorValue >> 16) & 0xFF,
                (colorValue >>  8) & 0xFF,
                 colorValue        & 0xFF);
        }

        /// <summary>
        /// Converts HSB (= HSV) to RGB and stores the result.
        /// H must be in [0, 360); S and <paramref name="brightness"/> must be in [0, 1].
        /// </summary>
        public void SetFromHsb(double h, double s, double brightness)
        {
            var (r, g, b) = HsbToRgb(h, s, brightness);
            SetFromRgb(r, g, b);
        }

        /// <summary>
        /// Converts HSL to RGB and stores the result.
        /// H must be in [0, 360); S and L must be in [0, 1].
        /// </summary>
        public void SetFromHsl(double h, double s, double l)
        {
            var (r, g, b) = HslToRgb(h, s, l);
            SetFromRgb(r, g, b);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static string NormalizeHex(string value) => ColorPickerUtils.NormalizeHex(value);

        // Shared hue calculation used by ToHsb and ToHsl. Returns H in [0, 360).
        private static double ComputeHue(double r, double g, double b, double cMax, double delta)
        {
            if (delta < double.Epsilon)
            {
                return 0.0;
            }

            double h;
            if (Math.Abs(cMax - r) < double.Epsilon)
            {
                h = 60.0 * (((g - b) / delta) % 6.0);
            }
            else if (Math.Abs(cMax - g) < double.Epsilon)
            {
                h = 60.0 * (((b - r) / delta) + 2.0);
            }
            else
            {
                h = 60.0 * (((r - g) / delta) + 4.0);
            }

            return h < 0.0 ? h + 360.0 : h;
        }

        private static (int r, int g, int b) HsbToRgb(double h, double s, double brightness)
        {
            var c = brightness * s;
            var x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            var m = brightness - c;
            return HueSectorToRgb(h, c, x, m);
        }

        private static (int r, int g, int b) HslToRgb(double h, double s, double l)
        {
            var c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            var x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            var m = l - c / 2.0;
            return HueSectorToRgb(h, c, x, m);
        }

        // Shared sector → (R,G,B) mapping used by both HsbToRgb and HslToRgb.
        private static (int r, int g, int b) HueSectorToRgb(double h, double c, double x, double m)
        {
            double r1, g1, b1;

            if      (h <  60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else              { r1 = c; g1 = 0; b1 = x; }

            return (
                Clamp((int)Math.Round((r1 + m) * 255)),
                Clamp((int)Math.Round((g1 + m) * 255)),
                Clamp((int)Math.Round((b1 + m) * 255))
            );
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

        private static void ValidateComponent(int value, string paramName)
        {
            if (value < 0 || value > 255)
            {
                throw new ArgumentOutOfRangeException(paramName,
                    $"{paramName} must be in the range [0, 255].");
            }
        }
    }
}
