namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.UI;
    using Sitecore.Shell.Applications.ContentEditor;

    /// <summary>
    /// Content Editor field that lets an author pick a color from a configurable swatch
    /// palette (driven by the field's Source value, e.g. "Brand Blue=0052CC|Brand Red=DE350B"),
    /// a native browser color picker, or by typing a hex value directly.
    /// The field value is always stored normalized as "#RRGGBB".
    ///
    /// Derives from the same base (Sitecore.Shell.Applications.ContentEditor.Text) as built-in
    /// simple-text-style fields, and is registered with an explicit controlSources "prefix" -
    /// the field's Control value must be "prefix:ClassName" (case-sensitive), not a bare custom
    /// tag, for Sitecore's Sheer/XML control resolver to find it.
    ///
    /// All interaction handlers are inlined directly on each element (no shared
    /// &lt;script&gt; block) because the Content Editor frequently injects field markup via
    /// innerHTML-style updates, and browsers never execute &lt;script&gt; tags inserted that
    /// way - a shared, separately-declared function would silently never run.
    /// </summary>
    public class ColorPicker : Text
    {
        private static readonly Regex HexPattern = new Regex("^#?[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        /// <summary>
        /// Gets or sets the Source value configured on the template field (e.g.
        /// "Brand Blue=0052CC|Brand Red=DE350B") - mirrors the same pattern used by built-in
        /// Content Editor field controls such as Custom.Source / File.Source / Icon.Source.
        /// </summary>
        public string Source
        {
            get { return this.GetViewStateString("Source"); }
            set { this.SetViewStateString("Source", value); }
        }

        protected override void DoRender(HtmlTextWriter output)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(output, "output");

            try
            {
                string id = this.ID;
                string value = NormalizeHex(this.Value) ?? string.Empty;
                string nativeValue = string.IsNullOrEmpty(value) ? "#000000" : value;

                output.Write(
                    "<div id=\"{0}_wrapper\" class=\"scColorPickerField\" style=\"display:flex;align-items:center;gap:6px;\">",
                    id);

                // The actual Sitecore field value travels back to the server through this
                // hidden input - its id/name must match this.ID so the standard Sitecore
                // postback handling (LoadPostData below) picks it up like any other field.
                output.Write(
                    "<input type=\"hidden\" id=\"{0}\" name=\"{0}\" value=\"{1}\" />",
                    id, HttpUtility.HtmlEncode(value));

                RenderSwatches(output, id, value);

                // The native browser color input is itself the "interactive button that opens a
                // color picker dialog" - clicking it opens the OS/browser color dialog directly.
                // Sized and bordered explicitly so it reads as a clickable control, not decoration.
                output.Write(
                    "<input type=\"color\" id=\"{0}_native\" value=\"{1}\" " +
                    "title=\"Choose a color\" " +
                    "style=\"width:36px;height:28px;padding:0;border:1px solid #b9c0c7;cursor:pointer;\" " +
                    "onchange=\"{2}\" />",
                    id, HttpUtility.HtmlAttributeEncode(nativeValue), BuildSyncScript(id, "this.value"));

                output.Write(
                    "<input type=\"text\" id=\"{0}_hex\" class=\"scColorPickerHex\" maxlength=\"7\" value=\"{1}\" " +
                    "placeholder=\"#RRGGBB\" style=\"width:80px;\" " +
                    "onchange=\"{2}\" />",
                    id,
                    HttpUtility.HtmlEncode(value),
                    BuildHexChangeScript(id));

                output.Write("</div>");
            }
            catch (Exception ex)
            {
                // Never leave the field area completely blank - log the real cause and fall
                // back to a plain editable textbox carrying the raw value, so authoring can
                // still continue while the issue gets fixed.
                Sitecore.Diagnostics.Log.Error("ColorPicker field failed to render: " + ex, this);
                output.Write(
                    "<input type=\"text\" id=\"{0}\" name=\"{0}\" value=\"{1}\" />",
                    this.ID, HttpUtility.HtmlEncode(this.Value ?? string.Empty));
            }
        }

        private void RenderSwatches(HtmlTextWriter output, string id, string currentValue)
        {
            if (string.IsNullOrEmpty(this.Source))
            {
                return;
            }

            NameValueCollection palette = Sitecore.StringUtil.ParseNameValueCollection(this.Source, '=', '|');
            if (palette == null || palette.Count == 0)
            {
                return;
            }

            output.Write("<span class=\"scColorPickerSwatches\" style=\"display:inline-flex;gap:4px;\">");

            foreach (string name in palette.AllKeys)
            {
                string hex = NormalizeHex(palette[name]);
                if (hex == null)
                {
                    continue;
                }

                bool selected = string.Equals(hex, currentValue, StringComparison.OrdinalIgnoreCase);
                string border = selected ? "2px solid #1e88e5" : "1px solid #b9c0c7";

                // A <button>, not an <a>, since anchor tags are prone to being specially
                // styled/reset by Content Editor chrome CSS (this is what was silently hiding
                // the swatches even though they were rendering with correct inline styles).
                output.Write(
                    "<button type=\"button\" class=\"scColorPickerSwatch{0}\" title=\"{1} ({2})\" " +
                    "style=\"display:inline-block;width:22px;height:22px;padding:0;background-color:{2};border:{3};cursor:pointer;\" " +
                    "onclick=\"{4}\"></button>",
                    selected ? " scColorPickerSwatchSelected" : string.Empty,
                    HttpUtility.HtmlAttributeEncode(name),
                    hex,
                    border,
                    BuildSyncScript(id, "'" + hex + "'"));
            }

            output.Write("</span>");
        }

        /// <summary>
        /// Builds a self-contained inline script that writes <paramref name="hexExpression"/>
        /// (a JS expression, either a quoted literal or "this.value") into the hidden field,
        /// the native color input and the hex textbox, then notifies the Content Editor that
        /// the form was modified.
        /// </summary>
        private static string BuildSyncScript(string id, string hexExpression)
        {
            return string.Format(
                "var v={1};" +
                "var h=document.getElementById('{0}');" +
                "var n=document.getElementById('{0}_native');" +
                "var x=document.getElementById('{0}_hex');" +
                "if(h){{h.value=v;}}" +
                "if(n){{n.value=v;}}" +
                "if(x){{x.value=v;}}" +
                "if(h){{var ev;try{{ev=new Event('change',{{bubbles:true}});}}catch(e){{ev=document.createEvent('Event');ev.initEvent('change',true,true);}}h.dispatchEvent(ev);}}" +
                "if(typeof scForm!=='undefined'&&scForm&&('modified' in scForm)){{scForm.modified=true;}}",
                id, hexExpression);
        }

        private static string BuildHexChangeScript(string id)
        {
            return
                "var v=this.value;" +
                "if(v&&v.charAt(0)!=='#'){v='#'+v;}" +
                "if(/^#[0-9A-Fa-f]{6}$/.test(v)){" + BuildSyncScript(id, "v") + "}" +
                "else{this.value=document.getElementById('" + id + "').value;}";
        }

        protected override bool LoadPostData(string value)
        {
            string normalized = NormalizeHex(value) ?? this.Value;
            return base.LoadPostData(normalized);
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.Value = NormalizeHex(this.Value) ?? this.Value;
        }

        private static string NormalizeHex(string value)
        {
            if (string.IsNullOrEmpty(value) || !HexPattern.IsMatch(value))
            {
                return null;
            }

            string hex = value.StartsWith("#", StringComparison.Ordinal) ? value : "#" + value;
            return hex.ToUpperInvariant();
        }
    }
}
