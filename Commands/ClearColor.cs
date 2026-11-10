namespace SoftConsulting.ColorPicker.Commands
{
    using System;
    using Sitecore.Diagnostics;
    using Sitecore.Web.UI.Sheer;

    /// <summary>
    /// "colorpicker:clear" - the Experience Editor's "Clear color" chrome button. Empties the
    /// field on the page (it is saved with the rest of the page), the same way the General Link
    /// field's "Clear" button works.
    /// </summary>
    [Serializable]
    public class ClearColor : ColorPickerWebEditCommand
    {
        protected override void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (string.IsNullOrEmpty(args.Parameters[FieldValueParameter]))
            {
                return;
            }

            this.UpdateField(args.Parameters["controlid"], this.GetEmptyFieldText(), string.Empty);
        }
    }
}
