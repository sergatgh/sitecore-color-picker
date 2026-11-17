namespace SoftConsulting.ColorPicker.Validators
{
    using System;
    using System.Runtime.Serialization;
    using SoftConsulting.ColorPicker.Distance;

    [Serializable]
    public class Ciede2000DistanceValidator : ColorDistanceValidator
    {
        public override string Name => "Color Picker Distance (CIEDE2000)";

        public Ciede2000DistanceValidator()
            : base(new Ciede2000DistanceCalculator())
        {
        }

        public Ciede2000DistanceValidator(SerializationInfo info, StreamingContext context)
            : base(new Ciede2000DistanceCalculator(), info, context)
        {
        }
    }
}
