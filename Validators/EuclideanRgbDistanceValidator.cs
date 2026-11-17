namespace SoftConsulting.ColorPicker.Validators
{
    using System;
    using System.Runtime.Serialization;
    using SoftConsulting.ColorPicker.Distance;

    [Serializable]
    public class EuclideanRgbDistanceValidator : ColorDistanceValidator
    {
        public override string Name => "Color Picker Distance (Euclidean RGB)";

        public EuclideanRgbDistanceValidator()
            : base(new EuclideanRgbDistanceCalculator())
        {
        }

        public EuclideanRgbDistanceValidator(SerializationInfo info, StreamingContext context)
            : base(new EuclideanRgbDistanceCalculator(), info, context)
        {
        }
    }
}
