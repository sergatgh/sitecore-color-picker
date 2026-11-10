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
    /// Code-beside of the "SoftConsulting.ColorPicker.Dialog" Sheer XML control
    /// (sitecore modules\Shell\ColorPicker\ColorPickerDialog.xml) - the color picker dialog the
    /// Experience Editor's "Edit color" button opens (see
    /// <see cref="SoftConsulting.ColorPicker.Commands.EditColor"/>). The picker itself is plain
    /// HTML rendered from Views\ColorPicker\Dialog.cshtml and driven entirely client side by
    /// ColorPickerDialog.js, which keeps the picked color in the hidden <see cref="ColorValue"/>
    /// input; this class only renders it, then validates and returns that value on OK.
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

        // Sheer creates this form via reflection, outside Sitecore's DI container - see the
        // matching comment on SoftConsulting.ColorPicker.Fields.ColorPicker.
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

            // Read once: the handle is removed from the session as soon as it's read, so anything
            // OnOK needs later is kept in ServerProperties instead.
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

        /// <summary>
        /// Whether <paramref name="value"/> (already normalized) may be saved: any color, unless
        /// the field's Source restricts it to its palette ("CustomColor=false").
        /// </summary>
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

        /// <summary>
        /// The form a color is stored in for this field: with transparency off, any alpha suffix
        /// is dropped (a palette may still list "#RRGGBBAA" swatches).
        /// </summary>
        protected virtual string ToFieldColor(string hex, bool allowTransparency)
            => allowTransparency ? hex : ColorPickerUtils.GetRgbPortion(hex);

        /// <summary>
        /// Palette item references in Source resolve against the database of the item being
        /// edited (passed by the command), falling back to the content database.
        /// </summary>
        protected virtual Database GetSourceDatabase(string databaseName)
        {
            var database = string.IsNullOrEmpty(databaseName) ? null : Factory.GetDatabase(databaseName, false);
            return database ?? Context.ContentDatabase ?? Context.Database;
        }
    }
}
