namespace SoftConsulting.ColorPicker.Processors
{
    using System.Collections.Specialized;
    using Sitecore.Data;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines;
    using Sitecore.SecurityModel;
    using SoftConsulting.ColorPicker.Infrastructure;

    /// <summary>
    /// Idempotent, self-healing bootstrap that provisions, in the content database:
    ///
    ///   - The "Palette" template at /sitecore/templates/Foundation/Color Picker/Palette, with a
    ///     single "Colors" field of type "Name Value List" - each name/value pair in that field
    ///     is one swatch (name -&gt; "RRGGBB"). This is the template a Color Picker field's Source
    ///     points a "Palette=&lt;path&gt;" segment at - see
    ///     <see cref="SoftConsulting.ColorPicker.Fields.ColorPickerSourceParser"/>.
    ///   - A "Color Picker" folder at /sitecore/system/Modules/Color Picker, with a "Palettes"
    ///     subfolder (leaving room for other kinds of items under "Color Picker" later)
    ///     containing a couple of ready-made Palette items (popular, well-known color systems) so
    ///     a new instance has something usable out of the box.
    ///
    /// Runs on every application start; re-checks (and repairs) the template's field values and
    /// only creates the sample palette items the first time - any later edits an author makes to
    /// those items are left alone.
    /// </summary>
    public class InitializeColorPickerPalette
    {
        private const string TemplatesFoundationFolderPath = "/sitecore/templates/Foundation";
        private const string TemplatesModuleFolderPath = TemplatesFoundationFolderPath + "/Color Picker";
        private const string PaletteTemplateName = "Palette";
        private const string PaletteTemplatePath = TemplatesModuleFolderPath + "/" + PaletteTemplateName;
        private const string DataSectionName = "Data";
        private const string DataSectionPath = PaletteTemplatePath + "/" + DataSectionName;
        private const string ColorsFieldName = "Colors";
        private const string ColorsFieldPath = DataSectionPath + "/" + ColorsFieldName;
        private const string ColorsFieldType = "Name Value List";

        private const string ModulesFolderPath = "/sitecore/system/Modules";
        private const string ColorPickerModuleFolderPath = ModulesFolderPath + "/Color Picker";
        private const string PaletteModuleFolderPath = ColorPickerModuleFolderPath + "/Palettes";

        private static readonly ID TemplateTemplateId = Sitecore.TemplateIDs.Template;
        private static readonly ID TemplateSectionTemplateId = Sitecore.TemplateIDs.TemplateSection;
        private static readonly ID TemplateFieldTemplateId = Sitecore.TemplateIDs.TemplateField;
        private static readonly ID TemplateFolderTemplateId = Sitecore.TemplateIDs.TemplateFolder;
        private static readonly ID FolderTemplateId = Sitecore.TemplateIDs.Folder;

        public virtual void Process(PipelineArgs args)
        {
            using (new SecurityDisabler())
            {
                var database = ContentDatabaseResolver.GetContentDatabase();
                if (database == null)
                {
                    Log.Warn("ColorPicker bootstrap: content database not found, skipping palette provisioning.", this);
                    return;
                }

                var paletteTemplate = EnsurePaletteTemplate(database);
                if (paletteTemplate == null)
                {
                    return;
                }

                EnsureSamplePalettes(database, paletteTemplate);
            }
        }

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => new ContentDatabaseResolver();

        protected virtual TemplateItem EnsurePaletteTemplate(Database database)
        {
            var templateFolderTemplate = database.Templates[TemplateFolderTemplateId];
            if (templateFolderTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateFolderTemplateId + " not found, skipping palette template.", this);
                return null;
            }

            if (EnsureItem(database, TemplatesFoundationFolderPath, "Foundation", templateFolderTemplate) == null)
            {
                return null;
            }

            if (EnsureItem(database, TemplatesModuleFolderPath, "Color Picker", templateFolderTemplate) == null)
            {
                return null;
            }

            var templateTemplate = database.Templates[TemplateTemplateId];
            if (templateTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateTemplateId + " not found, skipping palette template.", this);
                return null;
            }

            var paletteTemplateItem = EnsureItem(database, PaletteTemplatePath, PaletteTemplateName, templateTemplate);
            if (paletteTemplateItem == null)
            {
                return null;
            }

            if (!EnsureColorsField(database))
            {
                return null;
            }

            // The template engine caches field definitions per template; without resetting it,
            // items added against this template later in this same pipeline run would not see
            // the "Colors" field we may have just created.
            database.Engines.TemplateEngine.Reset();

            return new TemplateItem(paletteTemplateItem);
        }

        protected virtual bool EnsureColorsField(Database database)
        {
            var sectionTemplate = database.Templates[TemplateSectionTemplateId];
            if (sectionTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateSectionTemplateId + " not found, skipping palette template.", this);
                return false;
            }

            if (EnsureItem(database, DataSectionPath, DataSectionName, sectionTemplate) == null)
            {
                return false;
            }

            var fieldTemplate = database.Templates[TemplateFieldTemplateId];
            if (fieldTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateFieldTemplateId + " not found, skipping palette template.", this);
                return false;
            }

            var colorsField = EnsureItem(database, ColorsFieldPath, ColorsFieldName, fieldTemplate);
            if (colorsField == null)
            {
                return false;
            }

            if (colorsField["Type"] == ColorsFieldType)
            {
                return true;
            }

            using (new EditContext(colorsField))
            {
                colorsField["Type"] = ColorsFieldType;
            }

            Log.Info("ColorPicker bootstrap: updated '" + colorsField.Paths.FullPath + "'.", this);
            return true;
        }

        protected virtual void EnsureSamplePalettes(Database database, TemplateItem paletteTemplate)
        {
            var folderTemplate = database.Templates[FolderTemplateId];
            if (folderTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + FolderTemplateId + " not found, skipping sample palettes.", this);
                return;
            }

            var colorPickerFolder = EnsureItem(database, ColorPickerModuleFolderPath, "Color Picker", folderTemplate);
            if (colorPickerFolder == null)
            {
                return;
            }

            var palettesFolder = EnsureItem(database, PaletteModuleFolderPath, "Palettes", folderTemplate);
            if (palettesFolder == null)
            {
                return;
            }

            EnsureSamplePalette(database, palettesFolder, paletteTemplate, "Material Design", MaterialDesignPalette());
            EnsureSamplePalette(database, palettesFolder, paletteTemplate, "Flat UI Colors", FlatUiColorsPalette());
        }

        protected virtual void EnsureSamplePalette(
            Database database,
            Item parent,
            TemplateItem template,
            string name,
            NameValueCollection colors)
        {
            var path = parent.Paths.FullPath + "/" + name;
            if (database.GetItem(path) != null)
            {
                return;
            }

            var item = parent.Add(name, template);
            using (new EditContext(item))
            {
                new NameValueListField(item.Fields[ColorsFieldName]).NameValues = colors;
            }

            Log.Info("ColorPicker bootstrap: created '" + item.Paths.FullPath + "'.", this);
        }

        // Creates the item at "path" from "template" if it doesn't already exist. The item's
        // parent (everything in "path" up to the last segment) must already exist - callers are
        // responsible for ensuring ancestors first, same as the rest of this bootstrap.
        protected virtual Item EnsureItem(Database database, string path, string name, TemplateItem template)
        {
            var item = database.GetItem(path);
            if (item != null)
            {
                return item;
            }

            var parentPath = path.Substring(0, path.Length - name.Length - 1);
            var parent = database.GetItem(parentPath);
            if (parent == null)
            {
                Log.Error("ColorPicker bootstrap: parent '" + parentPath + "' not found, skipping '" + path + "'.", this);
                return null;
            }

            item = parent.Add(name, template);
            Log.Info("ColorPicker bootstrap: created '" + item.Paths.FullPath + "'.", this);
            return item;
        }

        protected virtual NameValueCollection MaterialDesignPalette()
            => new NameValueCollection
            {
                { "Red", "F44336" },
                { "Pink", "E91E63" },
                { "Purple", "9C27B0" },
                { "Deep_Purple", "673AB7" },
                { "Indigo", "3F51B5" },
                { "Blue", "2196F3" },
                { "Teal", "009688" },
                { "Green", "4CAF50" },
                { "Amber", "FFC107" },
                { "Orange", "FF9800" },
            };

        protected virtual NameValueCollection FlatUiColorsPalette()
            => new NameValueCollection
            {
                { "Turquoise", "1ABC9C" },
                { "Emerald", "2ECC71" },
                { "Peter_River", "3498DB" },
                { "Amethyst", "9B59B6" },
                { "Wet_Asphalt", "34495E" },
                { "Sun_Flower", "F1C40F" },
                { "Carrot", "E67E22" },
                { "Alizarin", "E74C3C" },
                { "Clouds", "ECF0F1" },
                { "Concrete", "95A5A6" },
            };
    }
}
