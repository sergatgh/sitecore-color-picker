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

        /// <summary>
        /// The module's files, left for manual cleanup by deliberate choice rather than because
        /// deletion is impossible for all of them - see the class remarks. Computed at runtime
        /// so the list never drifts from what is actually deployed.
        /// </summary>
        IReadOnlyList<string> GetManualCleanupFiles();
    }

    /// <summary>
    /// Acts on <see cref="IColorPickerUsageScanner"/>'s findings: converts Color Picker-typed
    /// fields, detaches swatch-validator references, and deletes custom Palette items - the
    /// three things that would make it unsafe to stop deploying this module's own Sitecore items.
    /// The module's own items (field type, validator, Palette template, sample palettes) and its
    /// deployed files are deliberately left for manual cleanup rather than deleted here - see
    /// <see cref="GetManualCleanupFiles"/> and the README's "Uninstalling" section for why. Every
    /// "all" method re-scans via <see cref="IColorPickerUsageScanner"/> before acting rather than
    /// trusting a caller to have scanned first, so it stays safe to call directly (e.g. from
    /// Tools/Remove-ColorPickerModule.ps1) without going through the admin page's wizard in order.
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

        // Mirrors the "SwatchValidator" include in serialization/ColorPicker/ColorPicker.module.json
        // (content database) - needed here only to look up the validator item's id so
        // RemoveValidatorReference knows which token to strip from a field's Validators list.
        private const string ValidatorItemPath =
            "/sitecore/system/Settings/Validation Rules/Field Rules/Color Picker/Color Picker Swatch";

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => this.contentDatabaseResolver;

        protected virtual IColorPickerUsageScanner UsageScanner => this.usageScanner;

        // ---- Step 1: convert Color Picker-typed fields ----------------------------------------

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

        // ---- Step 2: detach swatch-validator references ---------------------------------------

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

                // A Validation Rules List Builder field stores its assigned validators as a
                // "|"-delimited list of item IDs - remove only the one matching the swatch
                // validator, leaving any other assigned validators on this field untouched.
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

        // ---- Step 3: remove custom Palette items -----------------------------------------------

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

        // ---- Step 4: files to remove by hand ----------------------------------------------------

        public virtual IReadOnlyList<string> GetManualCleanupFiles()
        {
            var files = new List<string>();

            // IIS shadow-copies the loaded assembly into the ASP.NET Temporary Files folder, so
            // Assembly.GetExecutingAssembly().Location points there rather than at the deployed
            // DLL - map by the assembly's own name instead, so this lists the real file under the
            // site's ~/bin (the one a deploy actually writes and a manual delete should target).
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            AddMappedPathIfExists(files, "~/bin/" + assemblyName + ".dll");
            AddMappedPathIfExists(files, "~/bin/" + assemblyName + ".pdb");

            // Views/Web.config is shared MVC plumbing, not exclusive to this module, so it is
            // deliberately left off this list.
            AddMappedPathIfExists(files, "~/App_Config/Modules/ColorPicker");
            AddMappedPathIfExists(files, "~/Views/ColorPicker");
            AddMappedPathIfExists(files, "~/sitecore/admin/ColorPicker");
            AddMappedPathIfExists(files, "~/sitecore modules/Shell/ColorPicker");

            // Unlike everything above, deleting these two is what actually removes the module's
            // own Sitecore items (the field type, the Palette template, the swatch validator, the
            // sample palettes) - Sitecore's "Items as Resources" feature auto-loads any .dat file
            // under sitecore modules/items/<database>/ on startup, so deleting the file and recycling the
            // pool is the removal, the same as everything else on this list; nothing here calls
            // the Sitecore Data API to delete an item directly. Any item an author has since
            // edited and saved was copied into the real content database as an override at that
            // point, so it survives this - see the README's "Uninstalling" section for
            // `itemres cleanup`, which also removes those overrides.
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

        // System.Web.Hosting.HostingEnvironment.MapPath (rather than
        // HttpContext.Current.Server.MapPath) is a static, AppDomain-scoped API that resolves
        // identically whether this is called from the admin page/API's live HttpContext or from
        // an SPE ISE session with no HttpContext of its own - so this one method serves every
        // entry point with nothing duplicated between them.
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
