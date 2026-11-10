namespace SoftConsulting.ColorPicker.Pipelines.RenderField
{
    using System;
    using System.Web;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines.RenderField;

    /// <summary>
    /// By default an unknown field type renders raw and un-encoded, and the Experience Editor
    /// makes it contenteditable, so authors could type an invalid color. This HTML-encodes the
    /// value and disables inline editing, leaving the "Edit color"/"Clear color" buttons as the
    /// only way to change it. Must run before <see cref="RenderWebEditing"/>, which reads the flag.
    /// </summary>
    public class RenderColorPickerField
    {
        public virtual string FieldTypeKey { get; set; } = "color picker";

        public virtual void Process(RenderFieldArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (!this.IsColorPickerField(args))
            {
                return;
            }

            // Raw values can hold anything typed via "View > Raw values", so always encode.
            args.Result.FirstPart = this.RenderValue(args.Result.FirstPart);

            args.DisableWebEditContentEditing = true;
        }

        protected virtual bool IsColorPickerField(RenderFieldArgs args)
            => string.Equals(args.FieldTypeKey, this.FieldTypeKey, StringComparison.OrdinalIgnoreCase);

        protected virtual string RenderValue(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.HtmlEncode(value);
    }
}
