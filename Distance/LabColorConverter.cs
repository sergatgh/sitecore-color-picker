namespace SoftConsulting.ColorPicker.Distance
{
    using System;
    using System.Drawing;

    /// <summary>Converts sRGB to CIE L*a*b* via XYZ under the D65 reference white.</summary>
    public class LabColorConverter : ILabColorConverter
    {
        protected const double WhiteX = 0.95047;
        protected const double WhiteY = 1.0;
        protected const double WhiteZ = 1.08883;

        public virtual LabColor ToLab(Color color)
        {
            var r = ToLinear(color.R);
            var g = ToLinear(color.G);
            var b = ToLinear(color.B);

            var x = (0.4124564 * r) + (0.3575761 * g) + (0.1804375 * b);
            var y = (0.2126729 * r) + (0.7151522 * g) + (0.0721750 * b);
            var z = (0.0193339 * r) + (0.1191920 * g) + (0.9503041 * b);

            var fx = LabCompand(x / WhiteX);
            var fy = LabCompand(y / WhiteY);
            var fz = LabCompand(z / WhiteZ);

            return new LabColor((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        // Removes the sRGB gamma curve.
        protected virtual double ToLinear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        protected virtual double LabCompand(double value)
        {
            const double epsilon = 216.0 / 24389.0;
            const double kappa = 24389.0 / 27.0;

            return value > epsilon ? Math.Pow(value, 1.0 / 3.0) : ((kappa * value) + 16) / 116;
        }
    }
}
