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
    /// Content Editor field that lets an author pick a color from a configurable swatch
    /// palette, a native browser color picker, or a typed hex value; the stored value is always
    /// normalized to "#RRGGBB" (opaque) or "#RRGGBBAA" (with alpha). The palette and an optional
    /// custom view are driven by the field's Source value - see <see cref="ColorPickerSourceParser"/>
    /// for the grammar. All markup and script live in Views\ColorPicker\Field.cshtml, rendered
    /// via <see cref="SoftConsulting.ColorPicker.Infrastructure.RazorViewRenderer"/>; this class
    /// only builds the <see cref="ColorPickerViewModel"/> handed to it.
    /// </summary>
    public class ColorPicker : Text
    {
        protected virtual string DefaultVirtualPath => "~/Views/ColorPicker/Field.cshtml";

        /// <summary>
        /// The Source value configured on the template field - see
        /// <see cref="ColorPickerSourceParser"/> for the grammar. Mirrors the pattern used by
        /// built-in field controls such as Custom.Source / File.Source / Icon.Source.
        /// </summary>
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
                // Never leave the field area completely blank - log the real cause and fall
                // back to a plain editable textbox carrying the raw value, so authoring can
                // still continue while the issue gets fixed. This also covers Razor view
                // compile/render failures, not just C#-side errors.
                Sitecore.Diagnostics.Log.Error("ColorPicker field failed to render: " + ex, this);
                output.Write(
                    "<input type=\"text\" id=\"{0}\" name=\"{0}\" value=\"{1}\" />",
                    this.ID, HttpUtility.HtmlEncode(this.Value ?? string.Empty));
            }
        }

        // The Content Editor's Sheer/XML control factory creates this class via reflection
        // against its parameterless constructor, not through Sitecore's DI container, so the
        // parser cannot be constructor-injected here the way it is for the resolve="true"
        // initialize-pipeline processors - it is instead pulled from Sitecore's dependency
        // resolver (ServiceLocator) at the point of use. See
        // SoftConsulting.ColorPicker.Infrastructure.ColorPickerServicesConfigurator for the
        // registration and SoftConsulting.ColorPicker.Validators.SwatchValidator for the same
        // pattern applied to a validator, which is likewise built outside the container.
        protected virtual IColorPickerSourceParser ColorPickerSourceParser
            => ServiceLocator.ServiceProvider.GetService<IColorPickerSourceParser>() ?? new ColorPickerSourceParser();

        /// <summary>
        /// Builds the data handed to the rendering view. The rendered hidden input's id/name
        /// must match this.ID so <see cref="LoadPostData"/> picks the value up like any other
        /// field.
        /// </summary>
        protected virtual ColorPickerViewModel BuildViewModel(ColorPickerSourceResult parsedSource)
        {
            var id = this.ID;
            var value = NormalizeHex(this.Value) ?? string.Empty;

            // The native <input type="color"> cannot represent alpha at all (HTML spec
            // limitation - it only ever accepts/emits 6-digit "#rrggbb"), so it is always fed
            // just the RGB portion, regardless of whether the field value itself carries an
            // alpha suffix.
            var nativeValue = string.IsNullOrEmpty(value) ? "#000000" : value.Substring(0, 7);

            var model = new ColorPickerViewModel
            {
                Id = id,
                Value = value,
                NativeValue = nativeValue,
                AllowTransparency = parsedSource.AllowTransparency
            };

            foreach (var swatch in parsedSource.Swatches)
            {
                swatch.Selected = string.Equals(swatch.Hex, value, StringComparison.OrdinalIgnoreCase);
                model.Swatches.Add(swatch);
            }

            return model;
        }

        // The Content Editor edits against the content database (typically "master"), so
        // Palette item references in Source are resolved against it, same as built-in
        // Source-driven controls (e.g. droptree/droplink) resolve their item paths.
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
