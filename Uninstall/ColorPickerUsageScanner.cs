namespace SoftConsulting.ColorPicker.Uninstall
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Links;
    using Sitecore.SecurityModel;
    using SoftConsulting.ColorPicker.Infrastructure;

    public interface IColorPickerUsageScanner
    {
        ColorPickerUsageReport Scan();
    }

    /// <summary>
    /// Read-only scan for anything that would make it unsafe to blindly remove this module's
    /// items - see <see cref="ColorPickerUsageReport"/> for the three things it checks. Never
    /// mutates anything; <see cref="IColorPickerUninstaller"/> is the counterpart that acts on
    /// these findings. Only the content database(s) -
    /// <see cref="IContentDatabaseResolver.GetContentDatabases"/> - are scanned, not "core",
    /// since that's where template fields, Palette items, and the validator all live.
    /// </summary>
    public class ColorPickerUsageScanner : IColorPickerUsageScanner
    {
        private readonly IContentDatabaseResolver contentDatabaseResolver;

        public ColorPickerUsageScanner(IContentDatabaseResolver contentDatabaseResolver)
        {
            this.contentDatabaseResolver = contentDatabaseResolver ?? new ContentDatabaseResolver();
        }

        // Mirrors the field type item's own Sitecore name - the leaf item at
        // /sitecore/system/Field types/Color Picker/Color (see the "FieldType" include in
        // serialization/ColorPicker/ColorPicker.module.json), not the "Color Picker" category
        // folder it lives under. This is what a template field's "Type" holds once an author
        // picks it from the Type dropdown.
        private const string ColorPickerFieldTypeName = "Color Picker";

        // Mirrors the "SwatchValidator" include in serialization/ColorPicker/ColorPicker.module.json.
        private const string ValidatorItemPath =
            "/sitecore/system/Settings/Validation Rules/Field Rules/Color Picker/Color Picker Swatch";

        // Mirrors the "PaletteTemplate"/"PalettesFolder" includes in
        // serialization/ColorPicker/ColorPicker.module.json.
        private const string PaletteTemplatePath = "/sitecore/templates/Foundation/Color Picker/Palette";
        private const string PaletteModuleFolderPath = "/sitecore/system/Modules/Color Picker/Palettes";
        private static readonly string[] SeedPaletteNames = { "Material Design", "Flat UI Colors" };

        // The classic Content Editor's deep-link format - stable across Sitecore 8.x through
        // 10.x. sc_bw=1 opens the item outside the Content Editor's own frame, which is what's
        // needed when linking in from an external admin page rather than from inside the shell.
        // "fo" (force open) is what actually makes the Content Tree expand to and select "id"
        // itself - without it the tree stops one level up and selects the parent instead.
        // Not executable against a live instance from here - verify against your own Sitecore
        // install; if wrong, only BuildContentEditorUrl needs fixing.
        private const string ContentEditorUrlFormat = "/sitecore/shell/Applications/Content Editor.aspx?sc_bw=1&id={0}&fo={0}&db={1}";

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => this.contentDatabaseResolver;

        public virtual ColorPickerUsageReport Scan()
        {
            var report = new ColorPickerUsageReport();

            using (new SecurityDisabler())
            {
                foreach (var database in ContentDatabaseResolver.GetContentDatabases())
                {
                    if (database != null)
                    {
                        ScanDatabase(database, report);
                    }
                }
            }

            return report;
        }

        protected virtual void ScanDatabase(Database database, ColorPickerUsageReport report)
        {
            foreach (var templateField in GetTemplateFieldItems(database))
            {
                if (IsColorPickerFieldType(templateField))
                {
                    report.ColorPickerTypedFields.Add(DescribeField(templateField));
                }
            }

            var validatorItem = database.GetItem(ValidatorItemPath);
            if (validatorItem != null)
            {
                foreach (var link in GetSwatchValidatorReferrers(validatorItem))
                {
                    var sourceItem = link.GetSourceItem();
                    if (sourceItem != null)
                    {
                        report.SwatchValidatorFields.Add(DescribeField(sourceItem, link.SourceFieldID));
                    }
                }
            }

            var paletteTemplateItem = database.GetItem(PaletteTemplatePath);
            if (paletteTemplateItem == null)
            {
                return;
            }

            foreach (var palette in GetPaletteItems(database, paletteTemplateItem.ID))
            {
                if (!IsSeedPalette(palette))
                {
                    report.CustomPalettes.Add(DescribePalette(palette));
                }
            }
        }

        // Every field defined on every template in "database" - i.e. every item based on the
        // built-in "Template field" template, regardless of which content template it belongs
        // to. A "fast:" query is a single index-backed lookup rather than one database round
        // trip per template.
        protected virtual IEnumerable<Item> GetTemplateFieldItems(Database database)
            => database.SelectItems("fast://*[@@templateid='" + Sitecore.TemplateIDs.TemplateField + "']")
               ?? new Item[0];

        protected virtual bool IsColorPickerFieldType(Item templateFieldItem)
            => string.Equals(templateFieldItem["Type"], ColorPickerFieldTypeName, StringComparison.OrdinalIgnoreCase);

        // Sitecore's Link Database already tracks every reference from a template field's
        // Validators list to the validator item it points at - the same index Sitecore's own UI
        // uses for "this item is referenced elsewhere" before a delete. Nothing else in Sitecore
        // can reference a Validation Rule item this way, so every referrer here is, by
        // construction, a template field. This can be stale if the Link Database was never
        // rebuilt after a manual/import-based validator assignment - see the README's
        // troubleshooting note (Control Panel -> Indexing -> Rebuild Link Database).
        protected virtual IEnumerable<ItemLink> GetSwatchValidatorReferrers(Item validatorItem)
            => Sitecore.Globals.LinkDatabase.GetReferrers(validatorItem) ?? new ItemLink[0];

        protected virtual IEnumerable<Item> GetPaletteItems(Database database, ID paletteTemplateId)
            => database.SelectItems("fast://*[@@templateid='" + paletteTemplateId + "']") ?? new Item[0];

        protected virtual bool IsSeedPalette(Item paletteItem)
        {
            var parentPath = paletteItem.Parent?.Paths.FullPath;
            return string.Equals(parentPath, PaletteModuleFolderPath, StringComparison.OrdinalIgnoreCase)
                && SeedPaletteNames.Contains(paletteItem.Name, StringComparer.OrdinalIgnoreCase);
        }

        // validatorFieldId is only meaningful for a swatch-validator-reference entry (the field
        // on "item" whose Validators list holds the reference) - null for a plain
        // Color-Picker-typed-field entry.
        protected virtual ColorPickerFieldUsage DescribeField(Item item, ID validatorFieldId = null)
            => new ColorPickerFieldUsage
            {
                DatabaseName = item.Database.Name,
                ItemId = item.ID,
                ItemPath = item.Paths.FullPath,
                TemplateName = item.Parent?.Parent?.Name,
                FieldName = item.Name,
                ContentEditorUrl = BuildContentEditorUrl(item.Database.Name, item.ID),
                ValidatorFieldId = validatorFieldId,
                ValidatorFieldName = DescribeValidatorFieldName(item, validatorFieldId),
            };

        // Resolves validatorFieldId (a field on the "Template field" item itself - e.g. its
        // "Validation" field) to the name shown in the UI, so an admin can see exactly where on
        // that template field the swatch validator reference lives, not just which template
        // field it belongs to. Null when there is no validator field (plain
        // Color-Picker-typed-field entries) or it can no longer be resolved on the item.
        protected virtual string DescribeValidatorFieldName(Item item, ID validatorFieldId)
        {
            if (validatorFieldId is null || ID.Null.Equals(validatorFieldId))
            {
                return null;
            }

            var field = item.Fields[validatorFieldId];
            return field == null ? validatorFieldId.ToString() : (field.DisplayName ?? field.Name);
        }

        protected virtual ColorPickerPaletteUsage DescribePalette(Item paletteItem)
            => new ColorPickerPaletteUsage
            {
                DatabaseName = paletteItem.Database.Name,
                ItemId = paletteItem.ID,
                ItemPath = paletteItem.Paths.FullPath,
                Name = paletteItem.Name,
                ContentEditorUrl = BuildContentEditorUrl(paletteItem.Database.Name, paletteItem.ID),
            };

        protected virtual string BuildContentEditorUrl(string databaseName, ID itemId)
            => string.Format(ContentEditorUrlFormat, Uri.EscapeDataString(itemId.ToString()), Uri.EscapeDataString(databaseName));
    }

    /// <summary>
    /// Result of <see cref="IColorPickerUsageScanner.Scan"/> - everything that should be
    /// reviewed, and possibly acted on, before this module stops being deployed.
    /// </summary>
    public class ColorPickerUsageReport
    {
        /// <summary>
        /// Template fields whose Type is "Color". Removing the field type item while any of
        /// these exist would leave those fields unable to resolve their editor; that's what
        /// <see cref="IColorPickerUninstaller.ConvertFieldToSingleLineText"/> exists to clear first.
        /// </summary>
        public IList<ColorPickerFieldUsage> ColorPickerTypedFields { get; } = new List<ColorPickerFieldUsage>();

        /// <summary>
        /// Template fields whose Validators list references the "Color Picker Swatch" validator.
        /// Not blocking, but <see cref="IColorPickerUninstaller.RemoveValidatorReference"/> lets
        /// an admin detach these first so no field is left with a dangling reference.
        /// </summary>
        public IList<ColorPickerFieldUsage> SwatchValidatorFields { get; } = new List<ColorPickerFieldUsage>();

        /// <summary>
        /// Items based on the "Palette" template that are not one of the two seeded samples.
        /// These would be orphaned if the Palette template were deleted - see
        /// <see cref="IColorPickerUninstaller.RemovePaletteItem"/>.
        /// </summary>
        public IList<ColorPickerPaletteUsage> CustomPalettes { get; } = new List<ColorPickerPaletteUsage>();

        public bool HasColorPickerTypedFields => ColorPickerTypedFields.Count > 0;

        public bool HasCustomPalettes => CustomPalettes.Count > 0;
    }

    public class ColorPickerFieldUsage
    {
        public string DatabaseName { get; set; }

        public ID ItemId { get; set; }

        public string ItemPath { get; set; }

        public string TemplateName { get; set; }

        public string FieldName { get; set; }

        public string ContentEditorUrl { get; set; }

        /// <summary>
        /// The specific field on this item whose Validators list holds the swatch-validator
        /// reference - only populated on a <see cref="ColorPickerUsageReport.SwatchValidatorFields"/>
        /// entry; null otherwise.
        /// </summary>
        public ID ValidatorFieldId { get; set; }

        /// <summary>
        /// Display name of the field identified by <see cref="ValidatorFieldId"/> (typically
        /// "Validation"). Only populated alongside <see cref="ValidatorFieldId"/>.
        /// </summary>
        public string ValidatorFieldName { get; set; }
    }

    public class ColorPickerPaletteUsage
    {
        public string DatabaseName { get; set; }

        public ID ItemId { get; set; }

        public string ItemPath { get; set; }

        public string Name { get; set; }

        public string ContentEditorUrl { get; set; }
    }
}
