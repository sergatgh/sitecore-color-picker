namespace SoftConsulting.ColorPicker.Distance
{
    using System;
    using System.Drawing;

    /// <summary>
    /// CIE76 Delta E: Euclidean distance in L*a*b*. Around 2.3 is a just-noticeable difference;
    /// black to white is 100.
    /// </summary>
    public class Cie76DistanceCalculator : IColorDistanceCalculator
    {
        public Cie76DistanceCalculator()
            : this(new LabColorConverter())
        {
        }

        public Cie76DistanceCalculator(ILabColorConverter labColorConverter)
        {
            LabColorConverter = labColorConverter ?? throw new ArgumentNullException(nameof(labColorConverter));
        }

        protected ILabColorConverter LabColorConverter { get; }

        public virtual double GetDistance(Color first, Color second)
        {
            var lab1 = LabColorConverter.ToLab(first);
            var lab2 = LabColorConverter.ToLab(second);

            var dl = lab1.L - lab2.L;
            var da = lab1.A - lab2.A;
            var db = lab1.B - lab2.B;

            return Math.Sqrt((dl * dl) + (da * da) + (db * db));
        }
    }
}
