namespace SoftConsulting.ColorPicker.Validators
{
    using System;
    using System.Runtime.Serialization;
    using SoftConsulting.ColorPicker.Distance;

    [Serializable]
    public class Cie76DistanceValidator : ColorDistanceValidator
    {
        public override string Name => "Color Picker Distance (CIE76)";

        public Cie76DistanceValidator()
            : base(new Cie76DistanceCalculator())
        {
        }

        public Cie76DistanceValidator(SerializationInfo info, StreamingContext context)
            : base(new Cie76DistanceCalculator(), info, context)
        {
        }
    }
}
