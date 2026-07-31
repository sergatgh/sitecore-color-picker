namespace SoftConsulting.ColorPicker.Processors
{
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines;
    using Sitecore.SecurityModel;

    /// <summary>
    /// Idempotent, self-healing bootstrap for the "Color Picker" field type item in the core
    /// database. This is the supported, permanent way the field type is provisioned: it runs
    /// on every application start, creates the item the first time, and on every subsequent
    /// start re-syncs Control/Assembly/Class/Icon in case the code changes later - so a fresh
    /// environment (or one where someone edited/deleted the item by hand) always ends up
    /// correct without any manual setup or serialized-item push step.
    ///
    /// All ids below were read directly from the live Sitecore instance (Technical Information
    /// dialog) - none are guessed:
    ///   Parent          /sitecore/system/Field types                                  {76E6D8C7-1F93-4712-872B-DA3C96B808F2}
    ///   Field type tmpl /sitecore/templates/System/Templates/Template field type      {F8A17D6A-118E-4CD7-B5F5-88FF37A4F237}
    ///   Folder tmpl     /sitecore/templates/Common/Folder                             {A87A00B1-E6DB-45AB-8B54-636FEC3B5523}
    ///
    /// Creates a "Custom Field Types" category folder (using the standard Folder template, same
    /// as the built-in categories like "Simple Types") and "Color Picker" underneath it - the
    /// field-type dropdown only treats grandchildren of "/sitecore/system/Field types" as
    /// selectable, so the new field type cannot be a direct child of the root.
    /// </summary>
    public class InitializeColorPickerFieldType
    {
        private static readonly ID FieldTypesRootId = new ID("{76E6D8C7-1F93-4712-872B-DA3C96B808F2}");
        private static readonly ID TemplateFieldTypeId = new ID("{F8A17D6A-118E-4CD7-B5F5-88FF37A4F237}");
        private static readonly ID FolderTemplateId = new ID("{A87A00B1-E6DB-45AB-8B54-636FEC3B5523}");
        private const string CategoryItemPath = "/sitecore/system/Field types/Custom Field Types";
        private const string FieldTypeItemPath = CategoryItemPath + "/Color Picker";
        private const string IconPath = "/~/icon/softwarev2/32x32/color_wheel.png";
        private const string ControlValue = "custom:ColorPicker";
        private const string AssemblyValue = "SoftConsulting.ColorPicker";
        private const string ClassValue = "SoftConsulting.ColorPicker.Fields.ColorPicker";

        public void Process(PipelineArgs args)
        {
            // The initialize pipeline runs before any Sitecore user is authenticated, so
            // without this every read below is subject to the (anonymous) security context
            // and silently returns null instead of throwing - disable security for the whole
            // bootstrap, not just the write at the end.
            using (new SecurityDisabler())
            {
                var core = Factory.GetDatabase("core", false);
                if (core == null)
                {
                    Log.Warn("ColorPicker bootstrap: 'core' database not found, skipping.", this);
                    return;
                }

                var item = core.GetItem(FieldTypeItemPath);
                if (item != null)
                {
                    // Already created by a previous run of this processor - just keep
                    // Control/Assembly/Class/Icon in sync in case the code changed since.
                    EnsureFieldValues(item);
                    return;
                }

                var fieldTypeTemplate = core.Templates[TemplateFieldTypeId];
                if (fieldTypeTemplate == null)
                {
                    Log.Error("ColorPicker bootstrap: template " + TemplateFieldTypeId + " not found, skipping.", this);
                    return;
                }

                var category = core.GetItem(CategoryItemPath);
                if (category == null)
                {
                    var fieldTypesRoot = core.GetItem(FieldTypesRootId);
                    if (fieldTypesRoot == null)
                    {
                        Log.Error("ColorPicker bootstrap: parent item " + FieldTypesRootId + " not found, skipping.", this);
                        return;
                    }

                    var folderTemplate = core.Templates[FolderTemplateId];
                    if (folderTemplate == null)
                    {
                        Log.Error("ColorPicker bootstrap: template " + FolderTemplateId + " not found, skipping.", this);
                        return;
                    }

                    category = fieldTypesRoot.Add("Custom Field Types", folderTemplate);
                    Log.Info("ColorPicker bootstrap: created '" + category.Paths.FullPath + "'.", this);
                }

                item = category.Add("Color Picker", fieldTypeTemplate);
                EnsureFieldValues(item);

                Log.Info("ColorPicker bootstrap: created '" + item.Paths.FullPath + "'.", this);
            }
        }

        private static void EnsureFieldValues(Item item)
        {
            if (item["Control"] == ControlValue
                && item["Assembly"] == AssemblyValue
                && item["Class"] == ClassValue
                && item.Appearance.Icon == IconPath)
            {
                return;
            }

            using (new EditContext(item))
            {
                item["Control"] = ControlValue;
                item["Assembly"] = AssemblyValue;
                item["Class"] = ClassValue;
                item.Appearance.Icon = IconPath;
            }

            Log.Info("ColorPicker bootstrap: updated '" + item.Paths.FullPath + "'.", item);
        }
    }
}
