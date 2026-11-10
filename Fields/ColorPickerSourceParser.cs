namespace SoftConsulting.ColorPicker.Fields
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Text;
    using Sitecore.Data;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;

    public interface IColorPickerSourceParser
    {
        ColorPickerSourceResult Parse(string source, Database database);
    }

    /// <summary>
    /// Source grammar: pipe-separated "Key=Value" segments - repeatable "Palette" (item path/ID
    /// or inline "Name:RRGGBB;..."), "View", "Transparency", "CustomColor". A Source with
    /// neither '|' nor '=' is a single Palette value. Invalid pieces are skipped individually.
    /// </summary>
    public class ColorPickerSourceParser : IColorPickerSourceParser
    {
        private const string PaletteColorsFieldName = "Colors";

        public virtual ColorPickerSourceResult Parse(string source, Database database)
        {
            var result = new ColorPickerSourceResult();
            if (string.IsNullOrWhiteSpace(source))
            {
                return result;
            }

            if (source.IndexOf('|') < 0 && source.IndexOf('=') < 0)
            {
                AppendPaletteSwatches(source.Trim(), database, result.Swatches);
                return result;
            }

            foreach (var rawSegment in source.Split('|'))
            {
                var segment = rawSegment?.Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                var eqIndex = segment.IndexOf('=');
                if (eqIndex <= 0)
                {
                    continue;
                }

                var key = segment.Substring(0, eqIndex).Trim();
                var value = segment.Substring(eqIndex + 1).Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (string.Equals(key, "Palette", StringComparison.OrdinalIgnoreCase))
                {
                    AppendPaletteSwatches(value, database, result.Swatches);
                }
                else if (string.Equals(key, "View", StringComparison.OrdinalIgnoreCase))
                {
                    result.ViewPath = value;
                }
                else if (string.Equals(key, "Transparency", StringComparison.OrdinalIgnoreCase))
                {
                    result.AllowTransparency = ParseBoolean(value);
                }
                else if (string.Equals(key, "CustomColor", StringComparison.OrdinalIgnoreCase))
                {
                    result.AllowCustomColor = ParseBoolean(value, true);
                }
                else
                {
                    // Legacy format: a bare "Name=RRGGBB" swatch, not a recognized parameter.
                    var legacyHex = ColorPickerUtils.NormalizeHex(value);
                    if (legacyHex != null)
                    {
                        result.Swatches.Add(new ColorPickerSwatchViewModel { Name = key, Hex = legacyHex });
                    }
                }
            }

            return result;
        }

        protected virtual void AppendPaletteSwatches(string value, Database database, List<ColorPickerSwatchViewModel> swatches)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (LooksLikeItemReference(value))
            {
                var item = ResolvePaletteItem(value, database);
                if (item == null)
                {
                    Log.Warn("ColorPicker: Palette item '" + value + "' could not be resolved, skipping.", this);
                    return;
                }

                AppendCustomColorList(BuildColorListFromNameValueListField(item), swatches);
                return;
            }

            AppendCustomColorList(value, swatches);
        }

        protected virtual bool LooksLikeItemReference(string value)
            => value[0] == '/' || value[0] == '{';

        protected virtual Item ResolvePaletteItem(string value, Database database)
        {
            if (database == null)
            {
                return null;
            }

            if (value[0] == '{' && ID.TryParse(value, out var id))
            {
                return database.GetItem(id);
            }

            return database.GetItem(value);
        }

        // Converts the URL-encoded Name Value List into the inline "[Name]:RRGGBB;..." syntax so
        // it goes through the same validation as an inline list. Names are bracketed because
        // they may contain ':'.
        protected virtual string BuildColorListFromNameValueListField(Item item)
        {
            var field = item?.Fields[PaletteColorsFieldName];
            var collection = field == null ? new NameValueCollection() : new NameValueListField(field).NameValues;
            if (collection.Count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            foreach (var name in collection.AllKeys)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(';');
                }

                builder.Append('[').Append(name).Append("]:").Append(collection[name]);
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }

        protected virtual void AppendCustomColorList(string value, List<ColorPickerSwatchViewModel> swatches)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (var rawEntry in value.Split(';'))
            {
                var entry = rawEntry.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                if (!TryParseColorEntry(entry, out var name, out var hex))
                {
                    continue;
                }

                var normalizedHex = ColorPickerUtils.NormalizeHex(hex);
                if (normalizedHex == null || string.IsNullOrEmpty(name))
                {
                    continue;
                }

                swatches.Add(new ColorPickerSwatchViewModel { Name = name, Hex = normalizedHex });
            }
        }

        protected virtual bool ParseBoolean(string value) => ParseBoolean(value, false);

        // Unrecognized spellings fall back to defaultValue, so a typo never flips a flag.
        protected virtual bool ParseBoolean(string value, bool defaultValue)
        {
            if (bool.TryParse(value, out var flag))
            {
                return flag;
            }

            if (string.Equals(value, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(value, "0", StringComparison.Ordinal))
            {
                return false;
            }

            return defaultValue;
        }

        protected virtual bool TryParseColorEntry(string entry, out string name, out string hex)
        {
            name = null;
            hex = null;

            if (entry[0] == '[')
            {
                var closeIndex = entry.IndexOf(']');
                if (closeIndex < 0)
                {
                    return false;
                }

                name = entry.Substring(1, closeIndex - 1).Trim();
                var rest = entry.Substring(closeIndex + 1).Trim();
                if (rest.Length == 0 || rest[0] != ':')
                {
                    return false;
                }

                hex = rest.Substring(1).Trim();
                return true;
            }

            // An unbracketed name can't contain ':', so anything but exactly one ':' is invalid.
            var parts = entry.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            name = parts[0].Trim();
            hex = parts[1].Trim();
            return true;
        }
    }

    public class ColorPickerSourceResult
    {
        public List<ColorPickerSwatchViewModel> Swatches { get; } = new List<ColorPickerSwatchViewModel>();

        public string ViewPath { get; set; }

        public bool AllowTransparency { get; set; }

        public bool AllowCustomColor { get; set; } = true;
    }
}
