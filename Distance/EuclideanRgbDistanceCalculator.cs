namespace SoftConsulting.ColorPicker.Distance
{
    using System;
    using System.Drawing;

    /// <summary>
    /// Straight-line distance in RGB space, from 0 to ~441.67 (black to white). Cheap, but doesn't
    /// match human perception well.
    /// </summary>
    public class EuclideanRgbDistanceCalculator : IColorDistanceCalculator
    {
        public virtual double GetDistance(Color first, Color second)
        {
            double dr = first.R - second.R;
            double dg = first.G - second.G;
            double db = first.B - second.B;

            return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
        }
    }
}
