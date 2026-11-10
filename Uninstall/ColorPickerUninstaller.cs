namespace SoftConsulting.ColorPicker.Uninstall
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Web.Hosting;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.SecurityModel;
    using SoftConsulting.ColorPicker.Infrastructure;

    public interface IColorPickerUninstaller
    {
        ColorPickerActionResult ConvertFieldToSingleLineText(string databaseName, string itemId);

        ColorPickerBulkActionResult ConvertAllFieldsToSingleLineText();

        ColorPickerActionResult RemoveValidatorReference(string databaseName, string itemId, string fieldId);

        ColorPickerBulkActionResult RemoveAllValidatorReferences();

        ColorPickerActionResult RemovePaletteItem(string databaseName, string itemId);

        ColorPickerBulkActionResult RemoveAllPaletteItems();

        IReadOnlyList<string> GetManualCleanupFiles();
    }

    /// <summary>
    /// Clears what would break once the module's items are gone (Color Picker fields, validator
    /// references, custom Palette items). The module's own items and files are left for manual
    /// cleanup. "All" methods re-scan first, so they're safe to call outside the wizard.
    /// </summary>
    public class ColorPickerUninstaller : IColorPickerUninstaller
    {
        private readonly IContentDatabaseResolver contentDatabaseResolver;
        private readonly IColorPickerUsageScanner usageScanner;

        public ColorPickerUninstaller(IContentDatabaseResolver contentDatabaseResolver, IColorPickerUsageScanner usageScanner)
        {
            this.contentDatabaseResolver = contentDatabaseResolver ?? new ContentDatabaseResolver();
            this.usageScanner = usageScanner ?? new ColorPickerUsageScanner(this.contentDatabaseResolver);
        }

        private const string SingleLineTextFieldType = "Single-Line Text";

        private const string ValidatorItemPath =
            "/sitecore/system/Settings/Validation Rules/Field Rules/Color Picker/Color Picker Swatch";

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => this.contentDatabaseResolver;

        protected virtual IColorPickerUsageScanner UsageScanner => this.usageScanner;

        public virtual ColorPickerActionResult ConvertFieldToSingleLineText(string databaseName, string itemId)
        {
            if (!ID.TryParse(itemId, out var parsedItemId))
            {
                return ColorPickerActionResult.FailedResult("Invalid item ID.");
            }

            using (new SecurityDisabler())
            {
                var database = Factory.GetDatabase(databaseName, false);
                var item = database?.GetItem(parsedItemId);
                if (item == null)
                {
                    return ColorPickerActionResult.SuccessResult("Field no longer exists - nothing to convert.");
                }

                UpdateFieldType(item, SingleLineTextFieldType);
                return ColorPickerActionResult.SuccessResult("Converted to Single-Line Text.");
            }
        }

        public virtual ColorPickerBulkActionResult ConvertAllFieldsToSingleLineText()
        {
            var failures = new List<string>();
            var count = 0;

            foreach (var field in UsageScanner.Scan().ColorPickerTypedFields)
            {
                var result = ConvertFieldToSingleLineText(field.DatabaseName, field.ItemId.ToString());
                if (result.Success)
                {
                    count++;
                }
                else
                {
                    failures.Add(field.ItemPath + ": " + result.Message);
                }
            }

            return new ColorPickerBulkActionResult { Success = failures.Count == 0, Count = count, Failures = failures };
        }

        public virtual ColorPickerActionResult RemoveValidatorReference(string databaseName, string itemId, string fieldId)
        {
            if (!ID.TryParse(itemId, out var parsedItemId) || !ID.TryParse(fieldId, out var parsedFieldId))
            {
                return ColorPickerActionResult.FailedResult("Invalid item or field ID.");
            }

            using (new SecurityDisabler())
            {
                var database = Factory.GetDatabase(databaseName, false);
                var item = database?.GetItem(parsedItemId);
                if (item == null)
                {
                    return ColorPickerActionResult.SuccessResult("Item no longer exists - nothing to detach.");
                }

                var field = item.Fields[parsedFieldId];
                if (field == null)
                {
                    return ColorPickerActionResult.SuccessResult("Field no longer exists - nothing to detach.");
                }

                var validatorId = database.GetItem(ValidatorItemPath)?.ID;
                if (validatorId is null)
                {
                    return ColorPickerActionResult.SuccessResult("Validator item no longer exists - nothing to detach.");
                }

                // A "|"-delimited list of validator IDs - keep every other validator.
                var remaining = new List<string>();
                var removed = false;
                foreach (var rawToken in (field.Value ?? string.Empty).Split('|'))
                {
                    var token = rawToken.Trim();
                    if (token.Length == 0)
                    {
                        continue;
                    }

                    if (ID.TryParse(token, out var tokenId) && tokenId.Equals(validatorId))
                    {
                        removed = true;
                        continue;
                    }

                    remaining.Add(token);
                }

                if (!removed)
                {
                    return ColorPickerActionResult.FailedResult(
                        "Could not find the validator reference in this field's current value - it may already have been removed.");
                }

                using (new EditContext(item))
                {
                    field.Value = string.Join("|", remaining);
                }

                return ColorPickerActionResult.SuccessResult("Reference removed.");
            }
        }

        public virtual ColorPickerBulkActionResult RemoveAllValidatorReferences()
        {
            var failures = new List<string>();
            var count = 0;

            foreach (var field in UsageScanner.Scan().SwatchValidatorFields)
            {
                var result = RemoveValidatorReference(field.DatabaseName, field.ItemId.ToString(), field.ValidatorFieldId?.ToString());
                if (result.Success)
                {
                    count++;
                }
                else
                {
                    failures.Add(field.ItemPath + ": " + result.Message);
                }
            }

            return new ColorPickerBulkActionResult { Success = failures.Count == 0, Count = count, Failures = failures };
        }

        public virtual ColorPickerActionResult RemovePaletteItem(string databaseName, string itemId)
        {
            if (!ID.TryParse(itemId, out var parsedItemId))
            {
                return ColorPickerActionResult.FailedResult("Invalid item ID.");
            }

            using (new SecurityDisabler())
            {
                var database = Factory.GetDatabase(databaseName, false);
                var item = database?.GetItem(parsedItemId);
                if (item == null)
                {
                    return ColorPickerActionResult.SuccessResult("Item no longer exists - nothing to remove.");
                }

                item.Delete();
                return ColorPickerActionResult.SuccessResult("Removed.");
            }
        }

        public virtual ColorPickerBulkActionResult RemoveAllPaletteItems()
        {
            var failures = new List<string>();
            var count = 0;

            foreach (var palette in UsageScanner.Scan().CustomPalettes)
            {
                var result = RemovePaletteItem(palette.DatabaseName, palette.ItemId.ToString());
                if (result.Success)
                {
                    count++;
                }
                else
                {
                    failures.Add(palette.ItemPath + ": " + result.Message);
                }
            }

            return new ColorPickerBulkActionResult { Success = failures.Count == 0, Count = count, Failures = failures };
        }

        public virtual IReadOnlyList<string> GetManualCleanupFiles()
        {
            var files = new List<string>();

            // Assembly.Location points at the shadow copy, so map ~/bin by assembly name instead.
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            AddMappedPathIfExists(files, "~/bin/" + assemblyName + ".dll");
            AddMappedPathIfExists(files, "~/bin/" + assemblyName + ".pdb");

            // Views/Web.config is shared with other modules, so it isn't listed.
            AddMappedPathIfExists(files, "~/App_Config/Modules/ColorPicker");
            AddMappedPathIfExists(files, "~/Views/ColorPicker");
            AddMappedPathIfExists(files, "~/sitecore/admin/ColorPicker");
            AddMappedPathIfExists(files, "~/sitecore modules/Shell/ColorPicker");

            // Deleting these removes the module's items (Items as Resources). Items an author has
            // edited were copied into the database and survive - see `itemres cleanup` in the README.
            AddMappedPathIfExists(files, "~/sitecore modules/items/core/items.core.ColorPicker.dat");
            AddMappedPathIfExists(files, "~/sitecore modules/items/master/items.master.ColorPicker.dat");

            return files;
        }

        protected virtual void UpdateFieldType(Item templateFieldItem, string newTypeValue)
        {
            if (templateFieldItem["Type"] == newTypeValue)
            {
                return;
            }

            using (new EditContext(templateFieldItem))
            {
                templateFieldItem["Type"] = newTypeValue;
            }
        }

        // HostingEnvironment.MapPath also works without an HttpContext (e.g. from an SPE session).
        protected virtual void AddMappedPathIfExists(List<string> files, string virtualPath)
        {
            string physicalPath;
            try
            {
                physicalPath = HostingEnvironment.MapPath(virtualPath);
            }
            catch (Exception)
            {
                return;
            }

            if (string.IsNullOrEmpty(physicalPath))
            {
                return;
            }

            if (Directory.Exists(physicalPath) || File.Exists(physicalPath))
            {
                files.Add(physicalPath);
            }
        }
    }

    public class ColorPickerActionResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; }

        public static ColorPickerActionResult SuccessResult(string message) => new ColorPickerActionResult { Success = true, Message = message };

        public static ColorPickerActionResult FailedResult(string message) => new ColorPickerActionResult { Success = false, Message = message };
    }

    public class ColorPickerBulkActionResult
    {
        public bool Success { get; set; }

        public int Count { get; set; }

        public IReadOnlyList<string> Failures { get; set; } = new List<string>();
    }
}
