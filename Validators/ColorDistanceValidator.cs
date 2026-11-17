namespace SoftConsulting.ColorPicker.Validators
{
    using System;
    using System.Drawing;
    using System.Globalization;
    using System.Runtime.Serialization;
    using Sitecore.Data.Validators;
    using Sitecore.Diagnostics;
    using SoftConsulting.ColorPicker.Distance;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>
    /// Compares the field value against a target color, configured on the validator item's
    /// Parameters field, e.g. "Color=0052CC&amp;Distance=10&amp;Mode=Outside". Alpha is ignored.
    /// </summary>
    [Serializable]
    public abstract class ColorDistanceValidator : StandardValidator
    {
        public const string ColorParameterName = "Color";
        public const string DistanceParameterName = "Distance";
        public const string ModeParameterName = "Mode";

        protected ColorDistanceValidator(IColorDistanceCalculator distanceCalculator)
        {
            DistanceCalculator = distanceCalculator ?? throw new ArgumentNullException(nameof(distanceCalculator));
        }

        protected ColorDistanceValidator(IColorDistanceCalculator distanceCalculator, SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            DistanceCalculator = distanceCalculator ?? throw new ArgumentNullException(nameof(distanceCalculator));
        }

        /// <summary>Hex with or without '#'. Write it without '#' in Parameters, or encode it as %23.</summary>
        public virtual string TargetColor => GetParameter(ColorParameterName);

        /// <summary>Invariant-culture number on the calculator's scale; null when missing, negative or not a number.</summary>
        public virtual double? AcceptableDistance
            => double.TryParse(GetParameter(DistanceParameterName), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance)
               && distance >= 0
                ? distance
                : (double?)null;

        /// <summary>Defaults to <see cref="ColorDistanceMode.Within"/> when missing or unrecognized.</summary>
        public virtual ColorDistanceMode Mode
            => Enum.TryParse(GetParameter(ModeParameterName), true, out ColorDistanceMode mode)
               && Enum.IsDefined(typeof(ColorDistanceMode), mode)
                ? mode
                : ColorDistanceMode.Within;

        // Validators are created via reflection/serialization, so derived classes pass it to the constructor.
        [field: NonSerialized]
        protected IColorDistanceCalculator DistanceCalculator { get; }

        protected override ValidatorResult Evaluate()
        {
            var value = ControlValidationValue;
            if (string.IsNullOrEmpty(value))
            {
                return ValidatorResult.Valid;
            }

            if (!TryParseColor(value, out var color))
            {
                Text = GetText(
                    "The field \"{0}\" does not contain a valid hex color.",
                    GetFieldDisplayName());
                return GetFailedResult(ValidatorResult.Error);
            }

            var acceptableDistance = AcceptableDistance;
            if (!TryParseColor(TargetColor, out var targetColor) || acceptableDistance == null)
            {
                return HandleInvalidConfiguration();
            }

            var distance = DistanceCalculator.GetDistance(color, targetColor);
            if (IsAcceptable(distance, acceptableDistance.Value, Mode))
            {
                return ValidatorResult.Valid;
            }

            Text = GetFailureText(distance, acceptableDistance.Value, targetColor);
            return GetFailedResult(ValidatorResult.Error);
        }

        protected virtual bool IsAcceptable(double distance, double acceptableDistance, ColorDistanceMode mode)
            => mode == ColorDistanceMode.Outside
                ? distance > acceptableDistance
                : distance <= acceptableDistance;

        protected virtual string GetFailureText(double distance, double acceptableDistance, Color targetColor)
        {
            var text = Mode == ColorDistanceMode.Outside
                ? "The color of \"{0}\" is too close to {1}: the distance is {2}, but must be greater than {3}."
                : "The color of \"{0}\" is too far from {1}: the distance is {2}, but must be at most {3}.";

            return GetText(
                text,
                GetFieldDisplayName(),
                ToHex(targetColor),
                FormatDistance(distance),
                FormatDistance(acceptableDistance));
        }

        // A misconfigured validator item shouldn't block editors from saving, so it only logs.
        protected virtual ValidatorResult HandleInvalidConfiguration()
        {
            Log.Warn(
                $"ColorPicker: validator '{Name}' needs a valid '{ColorParameterName}' hex and a non-negative '{DistanceParameterName}' in its Parameters - skipping validation.",
                this);
            return ValidatorResult.Valid;
        }

        protected virtual bool TryParseColor(string value, out Color color)
        {
            color = Color.Empty;

            var normalized = ColorPickerUtils.NormalizeHex(value?.Trim());
            if (normalized == null)
            {
                return false;
            }

            var rgb = Convert.ToInt32(ColorPickerUtils.GetRgbPortion(normalized).Substring(1), 16);
            color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        // Parameter keys typed by admins shouldn't be case-sensitive.
        protected virtual string GetParameter(string name)
        {
            foreach (var key in Parameters.Keys)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Parameters[key];
                }
            }

            return null;
        }

        protected virtual string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        protected virtual string FormatDistance(double distance) => distance.ToString("0.##", CultureInfo.InvariantCulture);

        protected override ValidatorResult GetMaxValidatorResult()
            => GetFailedResult(ValidatorResult.Error);
    }
}
