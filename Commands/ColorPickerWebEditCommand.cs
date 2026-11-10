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
    /// Base class for the Experience Editor field buttons, following the same client contract as
    /// Sitecore's EditLink/ClearLink: the current value arrives in "scPlainValue", and the
    /// command answers by filling "scHtmlValue"/"scPlainValue" and calling scSetHtmlValue.
    /// Derives from <see cref="Command"/> rather than WebEditCommand to avoid a dependency on
    /// Sitecore.ExperienceEditor.dll. Must stay [Serializable]: Sheer keeps the instance between
    /// the requests of a client pipeline.
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

        /// <summary>Called again on each postback of the pipeline (e.g. when a dialog closes).</summary>
        protected abstract void Run(ClientPipelineArgs args);

        protected virtual Item GetItem(ClientPipelineArgs args)
        {
            var database = Context.ContentDatabase ?? Context.Database;
            Assert.IsNotNull(database, "database");

            var language = Language.Parse(args.Parameters["language"]);
            var version = Sitecore.Data.Version.Parse(args.Parameters["version"]);
            return database.GetItem(ID.Parse(args.Parameters["itemid"]), language, version);
        }

        protected virtual void UpdateField(string controlId, string html, string plainValue)
        {
            Assert.ArgumentNotNullOrEmpty(controlId, "controlId");

            SheerResponse.SetAttribute(HtmlValueInputId, "value", html ?? string.Empty);
            SheerResponse.SetAttribute(PlainValueInputId, "value", plainValue ?? string.Empty);

            // Without "clearIfEmpty", scSetHtmlValue treats an empty plain value as missing and
            // saves the HTML instead.
            var clearIfEmpty = string.IsNullOrEmpty(plainValue) ? "true" : "false";
            SheerResponse.Eval("scSetHtmlValue('" + this.EscapeJsString(controlId) + "', false, " + clearIfEmpty + ")");
        }

        protected virtual string EscapeJsString(string value)
            => (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");

        protected virtual string RenderFieldValue(Item item, string fieldName, string value)
        {
            Assert.ArgumentNotNull(item, "item");

            using (var renderer = new FieldRenderer())
            {
                renderer.Item = item;
                renderer.FieldName = fieldName;
                renderer.OverrideFieldValue(value ?? string.Empty);
                renderer.DisableWebEditing = true;

                // Render in the context of the site being edited, not the shell.
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
