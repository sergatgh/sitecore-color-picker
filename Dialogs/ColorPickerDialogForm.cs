namespace SoftConsulting.ColorPicker.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.UI;
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.DependencyInjection;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Web;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Pages;
    using Sitecore.Web.UI.Sheer;
    using SoftConsulting.ColorPicker.Fields;
    using SoftConsulting.ColorPicker.Infrastructure;

    /// <summary>
    /// Code-beside of ColorPickerDialog.xml. The picker runs client side (ColorPickerDialog.js)
    /// and writes the picked color into <see cref="ColorValue"/>; this class validates it on OK.
    /// </summary>
    public class ColorPickerDialogForm : DialogForm
    {
        private const string AllowTransparencyKey = "ColorPicker.AllowTransparency";
        private const string AllowCustomColorKey = "ColorPicker.AllowCustomColor";
        private const string PaletteColorsKey = "ColorPicker.PaletteColors";

        // Bound by Sheer to the controls with the same IDs in ColorPickerDialog.xml.
        protected Edit ColorValue;

        protected Border PickerHost;

        protected virtual string DialogViewPath => "~/Views/ColorPicker/Dialog.cshtml";

        // Sheer creates this form via reflection, so the parser can't be constructor-injected.
        protected virtual IColorPickerSourceParser ColorPickerSourceParser
            => ServiceLocator.ServiceProvider.GetService<IColorPickerSourceParser>() ?? new ColorPickerSourceParser();

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);

            if (Context.ClientPage.IsEvent)
            {
                return;
            }

            // The handle is removed from the session once read, so what OnOK needs goes to ServerProperties.
            var handle = UrlHandle.Get();
            var parsedSource = this.ColorPickerSourceParser.Parse(handle["source"], this.GetSourceDatabase(handle["database"]));
            var model = this.BuildViewModel(handle["value"], parsedSource);

            this.ServerProperties[AllowTransparencyKey] = model.AllowTransparency;
            this.ServerProperties[AllowCustomColorKey] = model.AllowCustomColor;
            this.ServerProperties[PaletteColorsKey] = string.Join("|", model.Swatches.Select(s => this.ToFieldColor(s.Hex, model.AllowTransparency)));

            this.ColorValue.Value = model.Value;
            this.PickerHost.Controls.Add(new LiteralControl(this.RenderPicker(model)));
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");

            var allowTransparency = this.ServerProperties[AllowTransparencyKey] as bool? ?? false;
            var value = ColorPickerUtils.NormalizeHex(this.ColorValue.Value);
            if (value == null)
            {
                SheerResponse.Alert(Translate.Text("Pick a color first, or press Cancel to keep the current value."));
                return;
            }

            value = this.ToFieldColor(value, allowTransparency);

            if (!this.IsAllowedColor(value))
            {
                SheerResponse.Alert(Translate.Text("This field only accepts the colors of its palette."));
                return;
            }

            SheerResponse.SetDialogValue(value);
            base.OnOK(sender, args);
        }

        protected virtual ColorPickerDialogViewModel BuildViewModel(string value, ColorPickerSourceResult parsedSource)
        {
            var model = new ColorPickerDialogViewModel
            {
                AllowTransparency = parsedSource.AllowTransparency,
                AllowCustomColor = parsedSource.AllowCustomColor,
                ValueInputId = this.ColorValue.ID
            };

            var normalized = ColorPickerUtils.NormalizeHex(value);
            model.Value = normalized == null ? string.Empty : this.ToFieldColor(normalized, model.AllowTransparency);

            foreach (var swatch in parsedSource.Swatches)
            {
                swatch.Selected = string.Equals(this.ToFieldColor(swatch.Hex, model.AllowTransparency), model.Value, StringComparison.OrdinalIgnoreCase);
                model.Swatches.Add(swatch);
            }

            return model;
        }

        protected virtual string RenderPicker(ColorPickerDialogViewModel model)
        {
            try
            {
                return RazorViewRenderer.RenderViewToString(this.DialogViewPath, model);
            }
            catch (Exception ex)
            {
                Log.Error("ColorPicker dialog failed to render: " + ex, this);
                return "<p class=\"cp-error\">" + Translate.Text("The color picker could not be displayed. See the Sitecore log for details.") + "</p>";
            }
        }

        protected virtual bool IsAllowedColor(string value)
        {
            var allowCustomColor = this.ServerProperties[AllowCustomColorKey] as bool? ?? true;
            if (allowCustomColor)
            {
                return true;
            }

            var palette = (this.ServerProperties[PaletteColorsKey] as string ?? string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            return palette.Contains(value, StringComparer.OrdinalIgnoreCase);
        }

        // With transparency off the alpha is dropped, even from "#RRGGBBAA" palette swatches.
        protected virtual string ToFieldColor(string hex, bool allowTransparency)
            => allowTransparency ? hex : ColorPickerUtils.GetRgbPortion(hex);

        protected virtual Database GetSourceDatabase(string databaseName)
        {
            var database = string.IsNullOrEmpty(databaseName) ? null : Factory.GetDatabase(databaseName, false);
            return database ?? Context.ContentDatabase ?? Context.Database;
        }
    }
}
