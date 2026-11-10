namespace SoftConsulting.ColorPicker.Validators
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Data.Validators;
    using Sitecore.DependencyInjection;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>
    /// Restricts the value to the swatches in the template field's Source. Passes when Source
    /// has no swatches.
    /// </summary>
    [Serializable]
    public class SwatchValidator : StandardValidator
    {
        public override string Name => "Color Picker Swatch";

        public SwatchValidator()
        {
        }

        public SwatchValidator(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override ValidatorResult Evaluate()
        {
            var value = ControlValidationValue;
            if (string.IsNullOrEmpty(value))
            {
                return ValidatorResult.Valid;
            }

            var item = GetItem();
            var source = GetTemplateFieldSource(item);
            if (string.IsNullOrEmpty(source))
            {
                return ValidatorResult.Valid;
            }

            var parsedSource = ColorPickerSourceParser.Parse(source, item?.Database);
            if (parsedSource.Swatches.Count == 0)
            {
                return ValidatorResult.Valid;
            }

            var normalizedValue = ColorPickerUtils.NormalizeHex(value);
            if (normalizedValue == null)
            {
                Text = GetText(
                    "The field \"{0}\" does not contain a valid hex color.",
                    GetFieldDisplayName());
                return GetFailedResult(ValidatorResult.Error);
            }

            // With transparency off, ignore alpha: a value saved while it was on must still match.
            var compareValue = parsedSource.AllowTransparency
                ? normalizedValue
                : ColorPickerUtils.GetRgbPortion(normalizedValue);

            foreach (var swatch in parsedSource.Swatches)
            {
                var swatchCompareValue = parsedSource.AllowTransparency
                    ? swatch.Hex
                    : ColorPickerUtils.GetRgbPortion(swatch.Hex);

                if (string.Equals(swatchCompareValue, compareValue, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidatorResult.Valid;
                }
            }

            Text = GetText(
                "The value of \"{0}\" must be one of the configured swatch colors.",
                GetFieldDisplayName());
            return GetFailedResult(ValidatorResult.Error);
        }

        // Validators are created via serialization, so the parser can't be constructor-injected.
        protected virtual IColorPickerSourceParser ColorPickerSourceParser
            => ServiceLocator.ServiceProvider.GetService<IColorPickerSourceParser>() ?? new ColorPickerSourceParser();

        protected override ValidatorResult GetMaxValidatorResult()
            => GetFailedResult(ValidatorResult.Error);

        private string GetTemplateFieldSource(Item item)
        {
            if (ID.IsNullOrEmpty(FieldID))
            {
                return null;
            }

            var templateField = item?.Template?.GetField(FieldID);
            return templateField?.Source;
        }
    }
}
