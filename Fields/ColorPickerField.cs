namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Drawing;
    using Sitecore.Data.Fields;

    /// <summary>
    /// Data-API wrapper for the "Color Picker" field type. Values are stored as upper-case
    /// "#RRGGBB" or "#RRGGBBAA".
    /// </summary>
    public class ColorPickerField : CustomField
    {
        public ColorPickerField(Field innerField)
            : base(innerField)
        {
        }

        public static implicit operator ColorPickerField(Field field)
            => field != null ? new ColorPickerField(field) : null;

        /// <summary>Invalid hex strings clear the field.</summary>
        protected override void SetValue(string value)
            => InnerField.Value = NormalizeHex(value) ?? string.Empty;

        // GetValue() is not overridden: every write already stores a normalized value.

        public bool IsEmpty => string.IsNullOrEmpty(InnerField.Value);

        public byte R => IsEmpty ? (byte)0 : Convert.ToByte(InnerField.Value.Substring(1, 2), 16);

        public byte G => IsEmpty ? (byte)0 : Convert.ToByte(InnerField.Value.Substring(3, 2), 16);

        public byte B => IsEmpty ? (byte)0 : Convert.ToByte(InnerField.Value.Substring(5, 2), 16);

        /// <summary>255 when the value has no alpha suffix.</summary>
        public virtual byte A => IsEmpty || InnerField.Value.Length < 9
            ? (byte)255
            : Convert.ToByte(InnerField.Value.Substring(7, 2), 16);

        public virtual bool IsOpaque => A == 255;

        public virtual Color ToRgb()
            => IsEmpty ? Color.Empty : Color.FromArgb(A, R, G, B);

        /// <summary>
        /// 0xAARRGGBB. A <see cref="long"/> so an alpha above 0x7F doesn't make it negative.
        /// </summary>
        public virtual long ToArgb()
            => IsEmpty ? 0 : ((long)A << 24) | (uint)((R << 16) | (G << 8) | B);

        public string ToHex()
            => InnerField.Value;

        /// <summary>24-bit RGB value; alpha is dropped (use <see cref="ToArgb"/> to keep it).</summary>
        public int ToDecimal()
            => IsEmpty ? 0 : (R << 16) | (G << 8) | B;

        /// <summary>Components in [0, 1]; black when empty.</summary>
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

        /// <summary>HSV. H in [0, 360); S and B in [0, 1].</summary>
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

        /// <summary>H in [0, 360); S and L in [0, 1].</summary>
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

        public void SetFromRgb(int r, int g, int b)
        {
            ValidateComponent(r, nameof(r));
            ValidateComponent(g, nameof(g));
            ValidateComponent(b, nameof(b));
            InnerField.Value = $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>Stores the 6-digit form when <paramref name="a"/> is 255.</summary>
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

        /// <summary>HSV. H in [0, 360); S and <paramref name="brightness"/> in [0, 1].</summary>
        public void SetFromHsb(double h, double s, double brightness)
        {
            var (r, g, b) = HsbToRgb(h, s, brightness);
            SetFromRgb(r, g, b);
        }

        /// <summary>H in [0, 360); S and L in [0, 1].</summary>
        public void SetFromHsl(double h, double s, double l)
        {
            var (r, g, b) = HslToRgb(h, s, l);
            SetFromRgb(r, g, b);
        }

        private static string NormalizeHex(string value) => ColorPickerUtils.NormalizeHex(value);

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
