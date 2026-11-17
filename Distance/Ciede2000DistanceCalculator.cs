namespace SoftConsulting.ColorPicker.Distance
{
    using System;
    using System.Drawing;

    /// <summary>
    /// CIEDE2000 Delta E, the most perceptually accurate of the CIE formulas. Around 1 is a
    /// just-noticeable difference; black to white is 100.
    /// </summary>
    public class Ciede2000DistanceCalculator : IColorDistanceCalculator
    {
        protected const double Pow25To7 = 6103515625.0;

        public Ciede2000DistanceCalculator()
            : this(new LabColorConverter())
        {
        }

        public Ciede2000DistanceCalculator(ILabColorConverter labColorConverter)
        {
            LabColorConverter = labColorConverter ?? throw new ArgumentNullException(nameof(labColorConverter));
        }

        protected ILabColorConverter LabColorConverter { get; }

        protected virtual double LightnessWeight => 1.0;

        protected virtual double ChromaWeight => 1.0;

        protected virtual double HueWeight => 1.0;

        public virtual double GetDistance(Color first, Color second)
            => GetDistance(LabColorConverter.ToLab(first), LabColorConverter.ToLab(second));

        // Follows Sharma, Wu & Dalal (2005), "The CIEDE2000 Color-Difference Formula".
        protected virtual double GetDistance(LabColor lab1, LabColor lab2)
        {
            var c1 = Math.Sqrt((lab1.A * lab1.A) + (lab1.B * lab1.B));
            var c2 = Math.Sqrt((lab2.A * lab2.A) + (lab2.B * lab2.B));
            var cMean7 = Math.Pow((c1 + c2) / 2, 7);
            var g = 0.5 * (1 - Math.Sqrt(cMean7 / (cMean7 + Pow25To7)));

            var a1Prime = (1 + g) * lab1.A;
            var a2Prime = (1 + g) * lab2.A;
            var c1Prime = Math.Sqrt((a1Prime * a1Prime) + (lab1.B * lab1.B));
            var c2Prime = Math.Sqrt((a2Prime * a2Prime) + (lab2.B * lab2.B));
            var h1Prime = GetHueAngle(lab1.B, a1Prime);
            var h2Prime = GetHueAngle(lab2.B, a2Prime);
            var chromaProduct = c1Prime * c2Prime;

            var deltaLPrime = lab2.L - lab1.L;
            var deltaCPrime = c2Prime - c1Prime;
            var deltaHueAngle = GetHueDifference(h1Prime, h2Prime, chromaProduct);
            var deltaHPrime = 2 * Math.Sqrt(chromaProduct) * Math.Sin(ToRadians(deltaHueAngle / 2));

            var lMeanPrime = (lab1.L + lab2.L) / 2;
            var cMeanPrime = (c1Prime + c2Prime) / 2;
            var hMeanPrime = GetMeanHue(h1Prime, h2Prime, chromaProduct);

            var t = 1
                - (0.17 * Math.Cos(ToRadians(hMeanPrime - 30)))
                + (0.24 * Math.Cos(ToRadians(2 * hMeanPrime)))
                + (0.32 * Math.Cos(ToRadians((3 * hMeanPrime) + 6)))
                - (0.20 * Math.Cos(ToRadians((4 * hMeanPrime) - 63)));

            var deltaTheta = 30 * Math.Exp(-Math.Pow((hMeanPrime - 275) / 25, 2));
            var cMeanPrime7 = Math.Pow(cMeanPrime, 7);
            var rc = 2 * Math.Sqrt(cMeanPrime7 / (cMeanPrime7 + Pow25To7));
            var lOffsetSquared = (lMeanPrime - 50) * (lMeanPrime - 50);
            var sl = 1 + ((0.015 * lOffsetSquared) / Math.Sqrt(20 + lOffsetSquared));
            var sc = 1 + (0.045 * cMeanPrime);
            var sh = 1 + (0.015 * cMeanPrime * t);
            var rt = -Math.Sin(ToRadians(2 * deltaTheta)) * rc;

            var lightness = deltaLPrime / (LightnessWeight * sl);
            var chroma = deltaCPrime / (ChromaWeight * sc);
            var hue = deltaHPrime / (HueWeight * sh);

            return Math.Sqrt((lightness * lightness) + (chroma * chroma) + (hue * hue) + (rt * chroma * hue));
        }

        // Degrees in [0, 360); an achromatic color gets 0.
        protected virtual double GetHueAngle(double b, double aPrime)
        {
            if (b == 0 && aPrime == 0)
            {
                return 0;
            }

            var degrees = ToDegrees(Math.Atan2(b, aPrime));
            return degrees < 0 ? degrees + 360 : degrees;
        }

        protected virtual double GetHueDifference(double h1, double h2, double chromaProduct)
        {
            if (chromaProduct == 0)
            {
                return 0;
            }

            var difference = h2 - h1;
            if (difference > 180)
            {
                return difference - 360;
            }

            return difference < -180 ? difference + 360 : difference;
        }

        protected virtual double GetMeanHue(double h1, double h2, double chromaProduct)
        {
            if (chromaProduct == 0)
            {
                return h1 + h2;
            }

            if (Math.Abs(h1 - h2) <= 180)
            {
                return (h1 + h2) / 2;
            }

            return h1 + h2 < 360 ? (h1 + h2 + 360) / 2 : (h1 + h2 - 360) / 2;
        }

        protected virtual double ToRadians(double degrees) => degrees * Math.PI / 180;

        protected virtual double ToDegrees(double radians) => radians * 180 / Math.PI;
    }
}
