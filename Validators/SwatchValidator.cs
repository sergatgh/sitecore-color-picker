namespace SoftConsulting.ColorPicker.Validators
{
    using System;
    using System.Runtime.Serialization;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Data.Validators;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>
    /// Field validator that restricts a Color Picker field to only the swatch colors defined
    /// in the template field's Source - see <see cref="SoftConsulting.ColorPicker.Fields.ColorPickerSourceParser"/>
    /// for the Source grammar (Palette=..., inline custom color lists, item-based palettes, etc.).
    ///
    /// When the field's Source is empty or contains no parseable swatches the validator passes
    /// without error — no restriction is applied and the author can pick any color. Attach this
    /// validator to a Color Picker template field only when you want to lock down the palette.
    ///
    /// Registration: this class is provisioned as a validator item in the core database at
    ///   /sitecore/system/Settings/Validation Rules/Field Rules/Color Picker Swatch
    /// via the <see cref="SoftConsulting.ColorPicker.Processors.InitializeSwatchValidator"/>
    /// bootstrap processor on application start.
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

            // When the current Source has transparency disabled, ignore alpha for the comparison
            // - a field value saved back when Transparency=true was in effect (e.g. a swatch
            // picked with 50% alpha) must still match its swatch's plain RGB portion once the
            // template field is reconfigured to Transparency=false, rather than failing
            // validation for a channel the field no longer edits.
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

        protected virtual IColorPickerSourceParser ColorPickerSourceParser => new ColorPickerSourceParser();

        protected override ValidatorResult GetMaxValidatorResult()
            => GetFailedResult(ValidatorResult.Error);

        // The field's swatch palette lives in the template field's Source property, not in the
        // item's field value. GetItem() (BaseValidator) gives us the item being edited; from
        // there we walk to its template and look up the field definition by FieldID.
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
