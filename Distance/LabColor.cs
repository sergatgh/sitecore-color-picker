namespace SoftConsulting.ColorPicker.Distance
{
    public struct LabColor
    {
        public LabColor(double l, double a, double b)
        {
            L = l;
            A = a;
            B = b;
        }

        public double L { get; }

        public double A { get; }

        public double B { get; }
    }
}
