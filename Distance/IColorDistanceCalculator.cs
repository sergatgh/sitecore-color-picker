namespace SoftConsulting.ColorPicker.Distance
{
    using System.Drawing;

    public interface IColorDistanceCalculator
    {
        /// <summary>
        /// Returns a non-negative distance; 0 means identical colors. Alpha is ignored. The scale
        /// depends on the algorithm, so thresholds aren't interchangeable between implementations.
        /// </summary>
        double GetDistance(Color first, Color second);
    }
}
