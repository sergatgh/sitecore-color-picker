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
    /// custom view override.
    ///
    /// Grammar - zero or more pipe-separated "Key=Value" segments (keys are case-insensitive):
    ///
    ///   Palette=&lt;item path or {ID}&gt; | Palette=&lt;custom color list&gt; | View=&lt;virtual path&gt;
    ///
    /// - "Palette" (repeatable): either the path/ID of an item whose "Colors" field (a "Name
    ///   Value List" field - see the "Palette" template at
    ///   /sitecore/templates/Foundation/Color Picker/Palette) holds name/color pairs, or an
    ///   inline custom color list. Every "Palette" segment's swatches are appended in order, so
    ///   multiple palettes - item-based, inline, or a mix - merge into one combined list.
    /// - Custom color list syntax: semicolon-separated "Name:RRGGBB" entries, e.g.
    ///   "[Solid Green]:#11FF11;[Green]:#00FF00;Red:#FF0000". ':' divides the name (left) from
    ///   the color (right). Wrapping the name in square brackets lets it contain ':' itself;
    ///   without brackets the name must not contain ':' - such an entry is ambiguous and is
    ///   skipped, same as an entry whose color half does not parse as a hex color.
    /// - "View": a virtual path to a Razor view that overrides the field control's default
    ///   rendering view. The last "View" segment wins if more than one is given.
    /// - Any other "Key=Value" segment is treated as one legacy literal swatch
    ///   ("Name=RRGGBB"), preserving the original Source format this field started with.
    /// - When the whole Source contains neither '|' nor '=', it is treated as a single Palette
    ///   value (item path/ID or inline custom color list) - this covers the common case of a
    ///   bare Source with nothing else configured.
    ///
    /// Malformed or unresolvable pieces (a Palette item that doesn't exist, an unparsable color,
    /// an ambiguous name) are skipped individually rather than failing the whole Source, so one
    /// bad entry never blanks out the rest of the palette.
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
    /// optional custom view virtual path. See <see cref="ColorPickerSourceParser"/> for the
    /// Source grammar that produces this.
    /// </summary>
    public class ColorPickerSourceResult
    {
        public List<ColorPickerSwatchViewModel> Swatches { get; } = new List<ColorPickerSwatchViewModel>();

        public string ViewPath { get; set; }
    }
}
