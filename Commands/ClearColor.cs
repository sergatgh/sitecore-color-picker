namespace SoftConsulting.ColorPicker.Commands
{
    using System;
    using Sitecore.Diagnostics;
    using Sitecore.Web.UI.Sheer;

    /// <summary>"colorpicker:clear" - the Experience Editor's "Clear color" button.</summary>
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
