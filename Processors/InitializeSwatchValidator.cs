namespace SoftConsulting.ColorPicker.Processors
{
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines;
    using Sitecore.SecurityModel;
    using SoftConsulting.ColorPicker.Infrastructure;

    /// <summary>
    /// Idempotent bootstrap that provisions the "Color Picker Swatch" validator item in the
    /// content database (the database content editors actually author in, e.g. "master") at:
    ///   /sitecore/system/Settings/Validation Rules/Field Rules/Color Picker/Color Picker Swatch
    ///
    /// After provisioning, content editors can attach this validator to any Color Picker
    /// template field to restrict accepted values to the field's configured swatch palette.
    ///
    /// The validator lives in its own "Color Picker" folder (rather than directly under
    /// "Field Rules") so future validators for this module have a home without cluttering the
    /// shared Field Rules folder.
    ///
    /// The content database is resolved via the "shell" site's contentDatabase setting rather
    /// than a hardcoded database name, so this still works correctly on instances that rename
    /// or repoint their authoring database.
    ///
    /// The templates for folder and validator items are looked up by path because their IDs are
    /// not surfaced in Sitecore.Kernel public constants — if a path ever changes, update the
    /// constant below (or confirm the actual template ID from Technical Information and switch
    /// to an ID lookup matching the pattern used in <see cref="InitializeColorPickerFieldType"/>).
    /// </summary>
    public class InitializeSwatchValidator
    {
        private readonly IContentDatabaseResolver contentDatabaseResolver;

        /// <summary>
        /// <paramref name="contentDatabaseResolver"/> is supplied directly from
        /// SoftConsulting.ColorPicker.Initialize.config, via a nested, typed &lt;param&gt;
        /// element on this processor's &lt;processor&gt; node - not resolved through Sitecore's
        /// DI container. The fallback below only matters if this class is ever constructed some
        /// other way (e.g. directly in a test).
        /// </summary>
        public InitializeSwatchValidator(IContentDatabaseResolver contentDatabaseResolver)
        {
            this.contentDatabaseResolver = contentDatabaseResolver ?? new ContentDatabaseResolver();
        }

        // The "Field Rules" folder already exists in every stock Sitecore install.
        private const string FieldRulesFolderPath = "/sitecore/system/Settings/Validation Rules/Field Rules";
        private const string ValidatorFolderName = "Color Picker";
        private const string ValidatorFolderPath = FieldRulesFolderPath + "/" + ValidatorFolderName;
        private const string ValidatorItemName = "Color Picker Swatch";
        private const string ValidatorItemPath = ValidatorFolderPath + "/" + ValidatorItemName;

        // Looked up by path because the Validation Rule template ID is not in any Sitecore.Kernel
        // public constant — this path is stable across all Sitecore 9+ / 10.x versions.
        private const string ValidationRuleTemplatePath = "/sitecore/templates/System/Validation/Validation Rule";

        // Standard "Folder" template, used to hold the "Color Picker" validators together.
        private const string FolderTemplatePath = "/sitecore/templates/Common/Folder";

        private const string TypeValue =
            "SoftConsulting.ColorPicker.Validators.SwatchValidator, SoftConsulting.ColorPicker";

        public virtual void Process(PipelineArgs args)
        {
            using (new SecurityDisabler())
            {
                var contentDatabase = GetContentDatabase();
                if (contentDatabase == null)
                {
                    Log.Warn("SwatchValidator bootstrap: content database not found, skipping.", this);
                    return;
                }

                var item = contentDatabase.GetItem(ValidatorItemPath);
                if (item != null)
                {
                    EnsureFieldValues(item);
                    return;
                }

                var templateItem = contentDatabase.GetItem(ValidationRuleTemplatePath);
                if (templateItem == null)
                {
                    Log.Error(
                        "SwatchValidator bootstrap: template at '" + ValidationRuleTemplatePath + "' not found, skipping.",
                        this);
                    return;
                }

                var validationRuleTemplate = new TemplateItem(templateItem);

                var folder = GetOrCreateValidatorFolder(contentDatabase);
                if (folder == null)
                {
                    return;
                }

                item = folder.Add(ValidatorItemName, validationRuleTemplate);
                EnsureFieldValues(item);

                Log.Info("SwatchValidator bootstrap: created '" + item.Paths.FullPath + "'.", this);
            }
        }

        protected virtual Item GetOrCreateValidatorFolder(Database contentDatabase)
        {
            var folder = contentDatabase.GetItem(ValidatorFolderPath);
            if (folder != null)
            {
                return folder;
            }

            var fieldRulesFolder = contentDatabase.GetItem(FieldRulesFolderPath);
            if (fieldRulesFolder == null)
            {
                Log.Error(
                    "SwatchValidator bootstrap: folder '" + FieldRulesFolderPath + "' not found, skipping.",
                    this);
                return null;
            }

            var folderTemplateItem = contentDatabase.GetItem(FolderTemplatePath);
            if (folderTemplateItem == null)
            {
                Log.Error(
                    "SwatchValidator bootstrap: template at '" + FolderTemplatePath + "' not found, skipping.",
                    this);
                return null;
            }

            folder = fieldRulesFolder.Add(ValidatorFolderName, new TemplateItem(folderTemplateItem));
            Log.Info("SwatchValidator bootstrap: created '" + folder.Paths.FullPath + "'.", this);

            return folder;
        }

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => this.contentDatabaseResolver;

        /// <summary>
        /// Resolves the database content editors author in, via the "shell" site's
        /// contentDatabase setting, falling back to "master" if that cannot be determined.
        /// </summary>
        protected virtual Database GetContentDatabase() => ContentDatabaseResolver.GetContentDatabase();

        protected virtual void EnsureFieldValues(Item item)
        {
            if (item["Type"] == TypeValue)
            {
                return;
            }

            using (new EditContext(item))
            {
                item["Type"] = TypeValue;
            }

            Log.Info("SwatchValidator bootstrap: updated '" + item.Paths.FullPath + "'.", item);
        }
    }
}
