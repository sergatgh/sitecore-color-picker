namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Web;
    using System.Web.UI;
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Data;
    using Sitecore.DependencyInjection;
    using Sitecore.Shell.Applications.ContentEditor;
    using SoftConsulting.ColorPicker.Infrastructure;

    /// <summary>
    /// Content Editor control for the Color Picker field. Markup lives in
    /// Views\ColorPicker\Field.cshtml; Source grammar is described on <see cref="ColorPickerSourceParser"/>.
    /// </summary>
    public class ColorPicker : Text
    {
        protected virtual string DefaultVirtualPath => "~/Views/ColorPicker/Field.cshtml";

        public string Source
        {
            get => this.GetViewStateString("Source");
            set => this.SetViewStateString("Source", value);
        }

        protected override void DoRender(HtmlTextWriter output)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(output, "output");

            try
            {
                var parsedSource = ColorPickerSourceParser.Parse(this.Source, this.GetSourceDatabase());
                var model = this.BuildViewModel(parsedSource);
                var virtualPath = string.IsNullOrEmpty(parsedSource.ViewPath) ? this.DefaultVirtualPath : parsedSource.ViewPath;
                var html = RazorViewRenderer.RenderViewToString(virtualPath, model);
                output.Write(html);
            }
            catch (Exception ex)
            {
                // Fall back to a plain textbox (also on Razor compile errors) so authoring isn't blocked.
                Sitecore.Diagnostics.Log.Error("ColorPicker field failed to render: " + ex, this);
                output.Write(
                    "<input type=\"text\" id=\"{0}\" name=\"{0}\" value=\"{1}\"{2} />",
                    this.ID, HttpUtility.HtmlEncode(this.Value ?? string.Empty),
                    this.IsReadOnly() ? " readonly=\"readonly\"" : string.Empty);
            }
        }

        // The Content Editor creates field controls via reflection, so the parser can't be
        // constructor-injected and is resolved from the ServiceLocator instead.
        protected virtual IColorPickerSourceParser ColorPickerSourceParser
            => ServiceLocator.ServiceProvider.GetService<IColorPickerSourceParser>() ?? new ColorPickerSourceParser();

        // The view's input id/name must equal this.ID so LoadPostData receives the value.
        protected virtual ColorPickerViewModel BuildViewModel(ColorPickerSourceResult parsedSource)
        {
            var id = this.ID;
            var value = NormalizeHex(this.Value) ?? string.Empty;

            // <input type="color"> only supports "#rrggbb", so it never gets the alpha suffix.
            var nativeValue = string.IsNullOrEmpty(value) ? "#000000" : value.Substring(0, 7);

            var model = new ColorPickerViewModel
            {
                Id = id,
                Value = value,
                NativeValue = nativeValue,
                AllowTransparency = parsedSource.AllowTransparency,
                AllowCustomColor = parsedSource.AllowCustomColor,
                ReadOnly = this.IsReadOnly()
            };

            foreach (var swatch in parsedSource.Swatches)
            {
                swatch.Selected = string.Equals(swatch.Hex, value, StringComparison.OrdinalIgnoreCase);
                model.Swatches.Add(swatch);
            }

            return model;
        }

        // EditorFormatter already folds all edit-permission checks (access rights, locks,
        // protected item, read-only database) into ReadOnly.
        protected virtual bool IsReadOnly() => this.ReadOnly || this.Disabled;

        private Database GetSourceDatabase()
            => Sitecore.Context.ContentDatabase ?? Sitecore.Context.Database;

        protected override bool LoadPostData(string value)
        {
            var normalized = NormalizeHex(value) ?? this.Value;
            return base.LoadPostData(normalized);
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.Value = NormalizeHex(this.Value) ?? this.Value;
        }

        private static string NormalizeHex(string value) => ColorPickerUtils.NormalizeHex(value);
    }
}
