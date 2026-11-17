namespace SoftConsulting.ColorPicker.Distance
{
    using System.Drawing;

    public interface ILabColorConverter
    {
        LabColor ToLab(Color color);
    }
}
