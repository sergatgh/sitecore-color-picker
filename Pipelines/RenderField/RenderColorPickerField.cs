namespace SoftConsulting.ColorPicker.Pipelines.RenderField
{
    using System;
    using System.Web;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines.RenderField;

    /// <summary>
    /// renderField processor for Color Picker fields (e.g. <c>@Html.Sitecore().Field("Case Color")</c>).
    /// Out of the box, a field of an unknown type like "Color Picker" is rendered as its raw,
    /// un-encoded value, and in the Experience Editor that value is wrapped in a contenteditable
    /// span, so an author can type anything into it - including an invalid color. This processor
    /// HTML-encodes the value and turns contenteditable off, the same way the built-in
    /// GetLinkFieldValue processor does for General Link fields. In the Experience Editor the hex
    /// value is then shown read-only, and selecting it opens the field's chrome toolbar, whose
    /// "Edit color"/"Clear color" buttons (core items under
    /// /sitecore/system/Field types/Color Picker/Color Picker/WebEdit Buttons) are the
    /// only way to change it.
    /// Must run before <see cref="RenderWebEditing"/>, which wraps the field for editing and reads
    /// the flag this processor sets - see App_Config\Modules\ColorPicker\SoftConsulting.ColorPicker.Fields.config.
    /// </summary>
    public class RenderColorPickerField
    {
        /// <summary>
        /// Field type key (the lower-cased field type name) this processor handles. Can be set
        /// from config to support a renamed or additional field type.
        /// </summary>
        public virtual string FieldTypeKey { get; set; } = "color picker";

        public virtual void Process(RenderFieldArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (!this.IsColorPickerField(args))
            {
                return;
            }

            // GetFieldValue has already put the raw value (or a caller-supplied override) into
            // FirstPart - only re-encode it here. Stored values are normally a normalized hex
            // color already, but the raw value can still be anything an author typed via
            // "View > Raw values", so it is never written out un-encoded.
            args.Result.FirstPart = this.RenderValue(args.Result.FirstPart);

            // Only read by RenderWebEditing, so it's harmless outside the Experience Editor.
            args.DisableWebEditContentEditing = true;
        }

        protected virtual bool IsColorPickerField(RenderFieldArgs args)
            => string.Equals(args.FieldTypeKey, this.FieldTypeKey, StringComparison.OrdinalIgnoreCase);

        protected virtual string RenderValue(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : HttpUtility.HtmlEncode(value);
    }
}
