namespace SoftConsulting.ColorPicker.Uninstall
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

        ColorPickerRemovalManifest GetRemovalManifest();

        ColorPickerRemovalResult RemoveModuleItems();

        /// <summary>
        /// The module's files, left for manual cleanup by deliberate choice rather than because
        /// deletion is impossible for all of them - see the class remarks. Computed at runtime,
        /// never hardcoded, so the list can never drift from what is actually deployed.
        /// </summary>
        IReadOnlyList<string> GetManualCleanupFiles();
    }

    /// <summary>
    /// Acts on <see cref="IColorPickerUsageScanner"/>'s findings: converts Color Picker-typed
    /// fields, detaches swatch-validator references, deletes custom Palette items, and finally
    /// removes everything SoftConsulting.ColorPicker.Processors.Initialize*.cs provisions -
    /// mirroring each processor's own path constants bottom-up (delete the leaf item, then
    /// delete its parent folder only if that folder is now empty - the inverse of the
    /// processors' top-down "create if missing").
    ///
    /// Every "all" method - <see cref="ConvertAllFieldsToSingleLineText"/>,
    /// <see cref="RemoveAllValidatorReferences"/>, <see cref="RemoveAllPaletteItems"/>,
    /// <see cref="RemoveModuleItems"/> - re-scans via <see cref="IColorPickerUsageScanner"/>
    /// itself before acting rather than trusting a caller to have scanned first, so this class
    /// stays safe to call directly (e.g. from Tools/Remove-ColorPickerModule.ps1) without going
    /// through the admin page's wizard steps in order.
    ///
    /// Only the DLL this class ships in (and its PDB) is actually locked by code running inside
    /// this same w3wp process - the CLR holds a loaded assembly open for the AppDomain's life.
    /// The module's App_Config/Views/admin-page files are not held open the same way and could
    /// technically be deleted here too, but <see cref="GetManualCleanupFiles"/> leaves all of it
    /// - DLL included - for manual cleanup by deliberate choice, not because deletion is
    /// impossible for all of them:
    /// <list type="bullet">
    /// <item><description>Self-modification mid-request risk - this code can run from the very
    /// admin page/API handler it would be deleting, or from a config file Sitecore's Include
    /// watcher reads on the next request.</description></item>
    /// <item><description>One consistent mental model for whoever runs this: Sitecore items are
    /// automated, the filesystem is manual, then recycle the pool - not a partial list where some
    /// files vanish automatically and others don't.</description></item>
    /// <item><description>Deployment/source-control drift - these files are normally checked in
    /// and pushed through a deploy pipeline; deleting them from a live server bypasses that, and
    /// the next deployment could silently resurrect them.</description></item>
    /// <item><description>Attack surface - keeping delete-by-path capability out of this class
    /// entirely avoids path-traversal/confused-deputy risk in an admin-gated handler.</description></item>
    /// </list>
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

        // Mirrors InitializeColorPickerFieldType.cs (core database).
        private const string FieldTypesCategoryPath = "/sitecore/system/Field types/Custom Field Types";
        private const string FieldTypeItemPath = FieldTypesCategoryPath + "/Color Picker";

        // Mirrors InitializeSwatchValidator.cs (content database).
        private const string FieldRulesFolderPath = "/sitecore/system/Settings/Validation Rules/Field Rules";
        private const string ValidatorFolderPath = FieldRulesFolderPath + "/Color Picker";
        private const string ValidatorItemPath = ValidatorFolderPath + "/Color Picker Swatch";

        // Mirrors InitializeColorPickerPalette.cs (content database).
        private const string TemplatesFoundationFolderPath = "/sitecore/templates/Foundation";
        private const string TemplatesModuleFolderPath = TemplatesFoundationFolderPath + "/Color Picker";
        private const string PaletteTemplatePath = TemplatesModuleFolderPath + "/Palette";
        private const string DataSectionPath = PaletteTemplatePath + "/Data";
        private const string ColorsFieldPath = DataSectionPath + "/Colors";
        private const string ModulesFolderPath = "/sitecore/system/Modules";
        private const string ColorPickerModuleFolderPath = ModulesFolderPath + "/Color Picker";
        private const string PaletteModuleFolderPath = ColorPickerModuleFolderPath + "/Palettes";
        private static readonly string[] SeedPaletteNames = { "Material Design", "Flat UI Colors" };

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

        // ---- Step 4: preview and final removal -------------------------------------------------

        public virtual ColorPickerRemovalManifest GetRemovalManifest()
        {
            var core = Factory.GetDatabase("core", false);
            var database = ContentDatabaseResolver.GetContentDatabase();

            var items = new List<string>();
            AddIfExists(core, FieldTypeItemPath, items);
            AddIfExists(database, ValidatorItemPath, items);

            foreach (var name in SeedPaletteNames)
            {
                AddIfExists(database, PaletteModuleFolderPath + "/" + name, items);
            }

            var hasCustomPalettes = UsageScanner.Scan().HasCustomPalettes;
            string reason = null;

            if (!hasCustomPalettes)
            {
                AddIfExists(database, ColorsFieldPath, items);
                AddIfExists(database, PaletteTemplatePath, items);
            }
            else
            {
                reason = "Custom Palette items still exist - remove them (step 3) first, or the Palette template is kept so they are not orphaned.";
            }

            return new ColorPickerRemovalManifest
            {
                ItemPathsToRemove = items,
                WillKeepPaletteTemplate = hasCustomPalettes,
                KeepPaletteTemplateReason = reason,
                ManualCleanupFiles = GetManualCleanupFiles(),
            };
        }

        public virtual ColorPickerRemovalResult RemoveModuleItems()
        {
            var fieldTypeResult = RemoveFieldType();
            var validatorResult = RemoveSwatchValidator();
            var palettesResult = RemovePalettes(deleteCustomPalettes: false);
            var paletteTemplateResult = RemovePaletteTemplate();

            var deleted = new List<string>();
            deleted.AddRange(fieldTypeResult.DeletedPaths);
            deleted.AddRange(validatorResult.DeletedPaths);
            deleted.AddRange(palettesResult.DeletedPaths);
            deleted.AddRange(paletteTemplateResult.DeletedPaths);

            // paletteTemplateResult may legitimately be "skipped" (custom palettes left in
            // place by choice) without that making the overall removal a failure - the other
            // three steps are what removing the module's own items actually depends on.
            var success = fieldTypeResult.Success && validatorResult.Success && palettesResult.Success;

            var message = string.Join(" ", new[] { fieldTypeResult, validatorResult, palettesResult, paletteTemplateResult }
                .Where(r => !r.Success)
                .Select(r => r.Message));

            return ColorPickerRemovalResult.CombinedResult(success, deleted, message.Length == 0 ? null : message);
        }

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
            AddMappedPathIfExists(files, "~/App_Config/Include/SoftConsulting/ColorPicker");
            AddMappedPathIfExists(files, "~/Views/ColorPicker");
            AddMappedPathIfExists(files, "~/sitecore/admin/ColorPicker");

            return files;
        }

        // ---- Bottom-up removal, mirroring the Initialize*.cs processors' path constants -------

        // Refuses to proceed while any field is still typed "Color Picker" - deleting the field
        // type out from under them would leave those fields unable to resolve their editor.
        protected virtual ColorPickerRemovalResult RemoveFieldType()
        {
            if (UsageScanner.Scan().HasColorPickerTypedFields)
            {
                return ColorPickerRemovalResult.SkippedResult(
                    "Skipped: one or more template fields are still typed \"Color Picker\" - convert them first.");
            }

            using (new SecurityDisabler())
            {
                var core = Factory.GetDatabase("core", false);
                if (core == null)
                {
                    return ColorPickerRemovalResult.SkippedResult("Skipped: 'core' database not found.");
                }

                var deleted = new List<string>();
                DeleteItemIfExists(core, FieldTypeItemPath, deleted);
                DeleteFolderIfEmpty(core, FieldTypesCategoryPath, deleted);

                return ColorPickerRemovalResult.SuccessResult(deleted);
            }
        }

        protected virtual ColorPickerRemovalResult RemoveSwatchValidator()
        {
            using (new SecurityDisabler())
            {
                var database = ContentDatabaseResolver.GetContentDatabase();
                if (database == null)
                {
                    return ColorPickerRemovalResult.SkippedResult("Skipped: content database not found.");
                }

                var deleted = new List<string>();
                DeleteItemIfExists(database, ValidatorItemPath, deleted);
                DeleteFolderIfEmpty(database, ValidatorFolderPath, deleted);

                return ColorPickerRemovalResult.SuccessResult(deleted);
            }
        }

        protected virtual ColorPickerRemovalResult RemovePalettes(bool deleteCustomPalettes)
        {
            using (new SecurityDisabler())
            {
                var database = ContentDatabaseResolver.GetContentDatabase();
                if (database == null)
                {
                    return ColorPickerRemovalResult.SkippedResult("Skipped: content database not found.");
                }

                var deleted = new List<string>();

                foreach (var name in SeedPaletteNames)
                {
                    DeleteItemIfExists(database, PaletteModuleFolderPath + "/" + name, deleted);
                }

                if (deleteCustomPalettes)
                {
                    foreach (var palette in UsageScanner.Scan().CustomPalettes)
                    {
                        var result = RemovePaletteItem(palette.DatabaseName, palette.ItemId.ToString());
                        if (result.Success)
                        {
                            deleted.Add(palette.ItemPath);
                        }
                    }
                }

                DeleteFolderIfEmpty(database, PaletteModuleFolderPath, deleted);
                DeleteFolderIfEmpty(database, ColorPickerModuleFolderPath, deleted);

                return ColorPickerRemovalResult.SuccessResult(deleted);
            }
        }

        // Skipped (not a failure) whenever custom Palette items still exist - removing the
        // template out from under them would orphan them. The admin (or the caller) decides
        // whether to clear those first via RemovePaletteItem/RemoveAllPaletteItems.
        protected virtual ColorPickerRemovalResult RemovePaletteTemplate()
        {
            if (UsageScanner.Scan().HasCustomPalettes)
            {
                return ColorPickerRemovalResult.SkippedResult(
                    "Skipped: custom Palette items still exist - removing the template now would orphan them.");
            }

            using (new SecurityDisabler())
            {
                var database = ContentDatabaseResolver.GetContentDatabase();
                if (database == null)
                {
                    return ColorPickerRemovalResult.SkippedResult("Skipped: content database not found.");
                }

                var deleted = new List<string>();
                DeleteItemIfExists(database, ColorsFieldPath, deleted);
                DeleteFolderIfEmpty(database, DataSectionPath, deleted);
                DeleteItemIfExists(database, PaletteTemplatePath, deleted);
                DeleteFolderIfEmpty(database, TemplatesModuleFolderPath, deleted);
                DeleteFolderIfEmpty(database, TemplatesFoundationFolderPath, deleted);

                return ColorPickerRemovalResult.SuccessResult(deleted);
            }
        }

        // Idempotent leaf delete: returns quietly (no error) when the item is already gone, the
        // same "already handled" branch every Initialize*.cs processor's EnsureItem takes on the
        // creation side.
        protected virtual bool DeleteItemIfExists(Database database, string path, List<string> deleted)
        {
            var item = database.GetItem(path);
            if (item == null)
            {
                return false;
            }

            item.Delete();
            deleted.Add(path);
            return true;
        }

        // "Not empty" (something else now lives under this folder - e.g. an author's own
        // template, or a sibling this module never created) is not an error - the folder is
        // simply left in place, same as InitializeColorPickerPalette.cs leaves "Foundation"
        // alone whenever other Foundation templates already exist.
        protected virtual bool DeleteFolderIfEmpty(Database database, string path, List<string> deleted)
        {
            var item = database.GetItem(path);
            if (item == null || item.HasChildren)
            {
                return false;
            }

            item.Delete();
            deleted.Add(path);
            return true;
        }

        protected virtual void AddIfExists(Database database, string path, List<string> items)
        {
            if (database?.GetItem(path) != null)
            {
                items.Add(path);
            }
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

    public class ColorPickerRemovalResult
    {
        public bool Success { get; private set; }

        public bool Skipped { get; private set; }

        public string Message { get; private set; }

        public IReadOnlyList<string> DeletedPaths { get; private set; } = new List<string>();

        public static ColorPickerRemovalResult SuccessResult(IReadOnlyList<string> deletedPaths)
            => new ColorPickerRemovalResult { Success = true, DeletedPaths = deletedPaths ?? new List<string>() };

        public static ColorPickerRemovalResult SkippedResult(string message)
            => new ColorPickerRemovalResult { Success = false, Skipped = true, Message = message };

        public static ColorPickerRemovalResult CombinedResult(bool success, IReadOnlyList<string> deletedPaths, string message)
            => new ColorPickerRemovalResult
            {
                Success = success,
                Skipped = !success,
                DeletedPaths = deletedPaths ?? new List<string>(),
                Message = message,
            };
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

    /// <summary>
    /// Preview for step 4: what will happen if <see cref="IColorPickerUninstaller.RemoveModuleItems"/>
    /// is run right now.
    /// </summary>
    public class ColorPickerRemovalManifest
    {
        /// <summary>
        /// Items that will unconditionally be deleted. Does not attempt to predict which
        /// now-empty parent folders will also be removed, to avoid a second simulation path that
        /// could drift from what <see cref="IColorPickerUninstaller.RemoveModuleItems"/> actually
        /// does - the removal result afterward shows the real, complete list.
        /// </summary>
        public IReadOnlyList<string> ItemPathsToRemove { get; set; } = new List<string>();

        public bool WillKeepPaletteTemplate { get; set; }

        public string KeepPaletteTemplateReason { get; set; }

        public IReadOnlyList<string> ManualCleanupFiles { get; set; } = new List<string>();
    }
}
