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
    /// Read-only scan for what would break if the module's items were removed. Only content
    /// databases are scanned; nothing relevant lives in "core".
    /// </summary>
    public class ColorPickerUsageScanner : IColorPickerUsageScanner
    {
        private readonly IContentDatabaseResolver contentDatabaseResolver;
        private readonly IColorPickerValidatorLocator validatorLocator;

        public ColorPickerUsageScanner(IContentDatabaseResolver contentDatabaseResolver)
            : this(contentDatabaseResolver, null)
        {
        }

        public ColorPickerUsageScanner(IContentDatabaseResolver contentDatabaseResolver, IColorPickerValidatorLocator validatorLocator)
        {
            this.contentDatabaseResolver = contentDatabaseResolver ?? new ContentDatabaseResolver();
            this.validatorLocator = validatorLocator ?? new ColorPickerValidatorLocator();
        }

        private const string ColorPickerFieldTypeName = "Color Picker";

        private const string PaletteTemplatePath = "/sitecore/templates/Foundation/Color Picker/Palette";
        private const string PaletteModuleFolderPath = "/sitecore/system/Modules/Color Picker/Palettes";
        private static readonly string[] SeedPaletteNames = { "Material Design", "Flat UI Colors" };

        // sc_bw=1 opens the editor outside the shell frame; "fo" makes the tree select the item
        // itself rather than its parent.
        private const string ContentEditorUrlFormat = "/sitecore/shell/Applications/Content Editor.aspx?sc_bw=1&id={0}&fo={0}&db={1}";

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => this.contentDatabaseResolver;

        protected virtual IColorPickerValidatorLocator ValidatorLocator => this.validatorLocator;

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

            ScanValidatorReferences(database, report);

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

        protected virtual IEnumerable<Item> GetTemplateFieldItems(Database database)
            => database.SelectItems("fast://*[@@templateid='" + Sitecore.TemplateIDs.TemplateField + "']")
               ?? new Item[0];

        protected virtual bool IsColorPickerFieldType(Item templateFieldItem)
            => string.Equals(templateFieldItem["Type"], ColorPickerFieldTypeName, StringComparison.OrdinalIgnoreCase);

        // One entry per referencing validation field, even when it lists several module validators.
        protected virtual void ScanValidatorReferences(Database database, ColorPickerUsageReport report)
        {
            var usages = new Dictionary<string, ColorPickerFieldUsage>(StringComparer.OrdinalIgnoreCase);

            foreach (var validatorItem in ValidatorLocator.GetModuleValidators(database))
            {
                foreach (var link in GetValidatorReferrers(validatorItem))
                {
                    var sourceItem = link.GetSourceItem();
                    if (sourceItem == null)
                    {
                        continue;
                    }

                    var key = sourceItem.ID + "|" + link.SourceFieldID;
                    if (!usages.TryGetValue(key, out var usage))
                    {
                        usage = DescribeField(sourceItem, link.SourceFieldID);
                        usages.Add(key, usage);
                        report.ValidatorFields.Add(usage);
                    }

                    usage.ValidatorNames.Add(validatorItem.Name);
                }
            }
        }

        // Only template fields can reference a validator item, so every referrer is one.
        // Results can be stale until the Link Database is rebuilt.
        protected virtual IEnumerable<ItemLink> GetValidatorReferrers(Item validatorItem)
            => Sitecore.Globals.LinkDatabase.GetReferrers(validatorItem) ?? new ItemLink[0];

        protected virtual IEnumerable<Item> GetPaletteItems(Database database, ID paletteTemplateId)
            => database.SelectItems("fast://*[@@templateid='" + paletteTemplateId + "']") ?? new Item[0];

        protected virtual bool IsSeedPalette(Item paletteItem)
        {
            var parentPath = paletteItem.Parent?.Paths.FullPath;
            return string.Equals(parentPath, PaletteModuleFolderPath, StringComparison.OrdinalIgnoreCase)
                && SeedPaletteNames.Contains(paletteItem.Name, StringComparer.OrdinalIgnoreCase);
        }

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

    public class ColorPickerUsageReport
    {
        public IList<ColorPickerFieldUsage> ColorPickerTypedFields { get; } = new List<ColorPickerFieldUsage>();

        /// <summary>Fields referencing any validator item whose Type comes from this module.</summary>
        public IList<ColorPickerFieldUsage> ValidatorFields { get; } = new List<ColorPickerFieldUsage>();

        /// <summary>Palette items other than the seeded samples.</summary>
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

        /// <summary>Set only for <see cref="ColorPickerUsageReport.ValidatorFields"/> entries.</summary>
        public ID ValidatorFieldId { get; set; }

        public string ValidatorFieldName { get; set; }

        /// <summary>Names of the module validator items the field references; empty for other entries.</summary>
        public IList<string> ValidatorNames { get; } = new List<string>();
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
