namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Web;
    using System.Web.UI;
    using Sitecore.Data;
    using Sitecore.Shell.Applications.ContentEditor;
    using SoftConsulting.ColorPicker.Infrastructure;

    /// <summary>
    /// Content Editor field that lets an author pick a color from a configurable swatch
    /// palette, a native browser color picker, or by typing a hex value directly. The field
    /// value is always stored normalized as "#RRGGBB".
    ///
    /// The palette (and an optional custom rendering view) is driven by the field's Source
    /// value - see <see cref="ColorPickerSourceParser"/> for the full grammar. In short, Source
    /// is zero or more pipe-separated "Key=Value" segments:
    ///
    ///   Palette=/sitecore/settings/color-picker/palette1|Palette=[Solid Green]:#11FF11;Red:#FF0000|View=/Views/Shared/MyColor.cshtml
    ///
    /// "Palette" is repeatable and merges (an item path/ID whose "Colors" field holds a custom
    /// color list, or an inline custom color list); "View" overrides <see cref="DefaultVirtualPath"/>.
    ///
    /// Derives from the same base (Sitecore.Shell.Applications.ContentEditor.Text) as built-in
    /// simple-text-style fields, and is registered with an explicit controlSources "prefix" -
    /// the field's Control value must be "prefix:ClassName" (case-sensitive), not a bare custom
    /// tag, for Sitecore's Sheer/XML control resolver to find it.
    ///
    /// The markup, all presentation logic (css class / border style per swatch) and the inline
    /// onclick/onchange script-building (formerly BuildSyncScript/BuildHexChangeScript here)
    /// all live in Views\ColorPicker\Field.cshtml now - this class only gathers raw data
    /// (ColorPickerViewModel) and hands it to SoftConsulting.ColorPicker.Infrastructure.
    /// RazorViewRenderer, which renders that view to a string (DoRender below runs inside the
    /// Sitecore Content Editor's Sheer/WebForms pipeline, not a real MVC request, so there is no
    /// controller action to return a view from - RazorViewRenderer fakes a minimal
    /// ControllerContext from the real HttpContext.Current instead). All interaction handlers
    /// are still inlined directly on each element in that view (no shared &lt;script&gt; block)
    /// because the Content Editor frequently injects field markup via innerHTML-style updates,
    /// and browsers never execute &lt;script&gt; tags inserted that way - a shared,
    /// separately-declared function would silently never run.
    /// </summary>
    public class ColorPicker : Text
    {
        protected virtual string DefaultVirtualPath => "~/Views/ColorPicker/Field.cshtml";

        /// <summary>
        /// Gets or sets the Source value configured on the template field - see
        /// <see cref="ColorPickerSourceParser"/> for the grammar. Mirrors the same pattern used
        /// by built-in Content Editor field controls such as Custom.Source / File.Source /
        /// Icon.Source.
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

        protected virtual IColorPickerSourceParser ColorPickerSourceParser => new ColorPickerSourceParser();

        /// <summary>
        /// Builds the data handed to the rendering view - the actual Sitecore field value
        /// travels back to the server through the rendered hidden input, whose id/name must
        /// match this.ID so the standard Sitecore postback handling (LoadPostData below) picks
        /// it up like any other field.
        /// </summary>
        protected virtual ColorPickerViewModel BuildViewModel(ColorPickerSourceResult parsedSource)
        {
            var id = this.ID;
            var value = NormalizeHex(this.Value) ?? string.Empty;
            var nativeValue = string.IsNullOrEmpty(value) ? "#000000" : value;

            var model = new ColorPickerViewModel
            {
                Id = id,
                Value = value,
                NativeValue = nativeValue
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
