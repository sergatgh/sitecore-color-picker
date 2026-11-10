namespace SoftConsulting.ColorPicker.Commands
{
    using System;
    using System.Collections.Specialized;
    using Sitecore;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Shell.Framework.Commands;
    using Sitecore.Sites;
    using Sitecore.Web;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.WebControls;

    /// <summary>
    /// Base class for the Experience Editor commands behind a Color Picker field's chrome toolbar
    /// buttons. Mirrors the contract Sitecore's own General Link commands
    /// (Sitecore.Shell.Applications.WebEdit.Commands.EditLink/ClearLink) use with the Experience
    /// Editor's client side:
    /// <list type="bullet">
    /// <item>The toolbar button's click, <c>chrome:field:editcontrol({"command":"..."})</c>, copies
    /// the field's current (possibly unsaved) value into the ribbon's "scPlainValue" input and
    /// posts the command with itemid/language/version/fieldid/controlid parameters.</item>
    /// <item>The command answers by writing the new rendered HTML and raw value into the
    /// "scHtmlValue"/"scPlainValue" inputs and calling <c>scSetHtmlValue(controlid, ...)</c>, which
    /// updates the field on the page and marks the page as modified.</item>
    /// </list>
    /// Deliberately derives from the Sitecore.Kernel <see cref="Command"/>, not Sitecore's
    /// WebEditCommand, so this module does not need to reference Sitecore.ExperienceEditor.dll.
    /// Must stay [Serializable]: Sheer keeps the command instance between the requests of a
    /// client pipeline (e.g. while a dialog is open).
    /// </summary>
    [Serializable]
    public abstract class ColorPickerWebEditCommand : Command
    {
        protected const string HtmlValueInputId = "scHtmlValue";

        protected const string PlainValueInputId = "scPlainValue";

        protected const string FieldValueParameter = "fieldValue";

        protected const string WebEditTextsItemPath = "/sitecore/content/Applications/WebEdit/WebEdit Texts";

        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");

            var parameters = new NameValueCollection(context.Parameters)
            {
                [FieldValueParameter] = WebUtil.GetFormValue(PlainValueInputId)
            };

            Context.ClientPage.Start(this, "Run", parameters);
        }

        /// <summary>
        /// The Sheer client pipeline step started by <see cref="Execute"/>; called again on each
        /// postback of the same pipeline (e.g. when a dialog it opened closes).
        /// </summary>
        protected abstract void Run(ClientPipelineArgs args);

        protected virtual Item GetItem(ClientPipelineArgs args)
        {
            var database = Context.ContentDatabase ?? Context.Database;
            Assert.IsNotNull(database, "database");

            var language = Language.Parse(args.Parameters["language"]);
            var version = Sitecore.Data.Version.Parse(args.Parameters["version"]);
            return database.GetItem(ID.Parse(args.Parameters["itemid"]), language, version);
        }

        /// <summary>
        /// Pushes a new value into the field on the page: <paramref name="html"/> is what the
        /// author sees, <paramref name="plainValue"/> is the raw value that gets saved.
        /// </summary>
        protected virtual void UpdateField(string controlId, string html, string plainValue)
        {
            Assert.ArgumentNotNullOrEmpty(controlId, "controlId");

            SheerResponse.SetAttribute(HtmlValueInputId, "value", html ?? string.Empty);
            SheerResponse.SetAttribute(PlainValueInputId, "value", plainValue ?? string.Empty);

            // The 3rd argument ("clearIfEmpty") is what lets an empty plain value through - without
            // it scSetHtmlValue treats an empty value as "no plain value" and saves the HTML instead.
            var clearIfEmpty = string.IsNullOrEmpty(plainValue) ? "true" : "false";
            SheerResponse.Eval("scSetHtmlValue('" + this.EscapeJsString(controlId) + "', false, " + clearIfEmpty + ")");
        }

        protected virtual string EscapeJsString(string value)
            => (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");

        /// <summary>
        /// Renders <paramref name="value"/> exactly the way the field itself renders on the page,
        /// through the renderField pipeline, so any customization of that pipeline (see
        /// <see cref="SoftConsulting.ColorPicker.Pipelines.RenderField.RenderColorPickerField"/>)
        /// is honored after an edit too.
        /// </summary>
        protected virtual string RenderFieldValue(Item item, string fieldName, string value)
        {
            Assert.ArgumentNotNull(item, "item");

            using (var renderer = new FieldRenderer())
            {
                renderer.Item = item;
                renderer.FieldName = fieldName;
                renderer.OverrideFieldValue(value ?? string.Empty);
                renderer.DisableWebEditing = true;

                // The Experience Editor posts the name of the site being edited - render in its
                // context, not the shell's, the same way EditLink does.
                var siteName = WebUtil.GetFormValue("scSite");
                var site = string.IsNullOrEmpty(siteName) ? null : SiteContextFactory.GetSiteContext(siteName);
                if (site == null)
                {
                    return renderer.RenderField().ToString();
                }

                using (new SiteContextSwitcher(site))
                {
                    return renderer.RenderField().ToString();
                }
            }
        }

        /// <summary>
        /// The "[No text in field]" placeholder the Experience Editor shows for an empty field.
        /// </summary>
        protected virtual string GetEmptyFieldText()
        {
            using (new LanguageSwitcher(WebUtil.GetCookieValue("shell", "lang", Context.Language.Name)))
            {
                var core = Factory.GetDatabase("core", false);
                var texts = core?.GetItem(WebEditTextsItemPath);
                var text = texts?["Default Text"];
                return string.IsNullOrEmpty(text) ? "[No text in field]" : text;
            }
        }
    }
}
