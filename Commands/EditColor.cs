namespace SoftConsulting.ColorPicker.Commands
{
    using System;
    using Sitecore;
    using Sitecore.Diagnostics;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.Web.UI.Sheer;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>"colorpicker:edit" - the Experience Editor's "Edit color" button.</summary>
    [Serializable]
    public class EditColor : ColorPickerWebEditCommand
    {
        protected virtual string DialogControlName => "SoftConsulting.ColorPicker.Dialog";

        protected virtual string DialogWidth => "780";

        protected virtual string DialogHeight => "620";

        protected override void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            var item = this.GetItem(args);
            if (item == null)
            {
                SheerResponse.Alert("The item was not found.\n\nIt may have been deleted by another user.");
                return;
            }

            var field = item.Fields[args.Parameters["fieldid"]];
            Assert.IsNotNull(field, "field");

            if (!args.IsPostBack)
            {
                this.ShowDialog(args, field.Source, item.Database.Name);
                args.WaitForPostBack();
                return;
            }

            if (!args.HasResult)
            {
                return;
            }

            var value = ColorPickerUtils.NormalizeHex(args.Result);
            if (value == null)
            {
                return;
            }

            this.UpdateField(args.Parameters["controlid"], this.RenderFieldValue(item, field.Name, value), value);
        }

        protected virtual void ShowDialog(ClientPipelineArgs args, string fieldSource, string databaseName)
        {
            var url = new UrlString(UIUtil.GetUri("control:" + this.DialogControlName));

            // A session-backed handle, not the query string: Source can be long and contain '|', '#', ';'.
            var handle = new UrlHandle
            {
                ["value"] = args.Parameters[FieldValueParameter] ?? string.Empty,
                ["source"] = fieldSource ?? string.Empty,
                ["database"] = databaseName ?? string.Empty
            };
            handle.Add(url);

            SheerResponse.ShowModalDialog(url.ToString(), this.DialogWidth, this.DialogHeight, string.Empty, true);
        }
    }
}
