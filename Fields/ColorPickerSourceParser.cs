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
    /// Parses a Color Picker field's Source value into a merged swatch palette plus an optional
    /// custom view override. Source is pipe-separated "Key=Value" segments: repeatable "Palette"
    /// entries (an item path/ID, or an inline "Name:RRGGBB;..." list) merge into one swatch
    /// list, "View" overrides the rendering view, and "Transparency=true" opts into alpha
    /// editing; a bare Source with neither '|' nor '=' is treated as a single Palette value.
    /// Malformed or unresolvable pieces (a missing Palette item, an unparsable color, an
    /// ambiguous name) are skipped individually rather than failing the whole Source.
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

        // Item paths ("/sitecore/...") and IDs ("{GUID}") are the only two shapes a Palette
        // value can resolve to an item as; anything else is treated as an inline color list.
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

        // The "Colors" field on a Palette item is a "Name Value List" field, stored as a
        // URL-encoded "Name1=Value1&Name2=Value2" query string - not the semicolon/colon syntax
        // AppendCustomColorList understands. NameValueListFieldReader parses it properly (the
        // same way Sitecore's own field editor does), and this converts the resulting
        // name/color pairs into that semicolon/colon syntax so they can be fed straight into
        // AppendCustomColorList and get the same hex validation/normalization as any other
        // custom color list.
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

        // Accepts the same boolean spellings Sitecore Source strings commonly use: "true"/"false"
        // (any case, via bool.TryParse) as well as the bare "1" checkbox-field convention. Any
        // other spelling (including an empty/unrecognized value) is treated as "false", keeping
        // transparency opt-in by default.
        protected virtual bool ParseBoolean(string value)
        {
            if (bool.TryParse(value, out var flag))
            {
                return flag;
            }

            return string.Equals(value, "1", StringComparison.Ordinal);
        }

        // Splits one "Name:RRGGBB" (or "[Name]:RRGGBB") entry. Returns false for anything
        // malformed or ambiguous, which the caller treats as "skip this entry".
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

            // Unbracketed: exactly one ':' is required - zero means there is no color half, and
            // more than one means the name itself contains ':', which is only unambiguous when
            // bracketed (see class docs).
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

    /// <summary>
    /// Result of parsing a Color Picker field's Source value: the merged swatch palette and an
    /// optional custom view path. See <see cref="ColorPickerSourceParser"/> for the grammar.
    /// </summary>
    public class ColorPickerSourceResult
    {
        public List<ColorPickerSwatchViewModel> Swatches { get; } = new List<ColorPickerSwatchViewModel>();

        public string ViewPath { get; set; }

        /// <summary>
        /// Whether the field's Source opted into transparency editing via "Transparency=true".
        /// Defaults to false - a plain opaque color picker, no alpha slider or 8-digit hex entry.
        /// </summary>
        public bool AllowTransparency { get; set; }
    }
}
