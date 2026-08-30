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
    ///
    /// Every item this bootstrap creates gets a fixed id (see the *ItemId fields below),
    /// generated once and passed explicitly to Item.Add, rather than whatever random id Item.Add
    /// would otherwise assign - see the equivalent note on
    /// <see cref="InitializeColorPickerFieldType"/> for why. "Foundation" is the one folder here
    /// that other, unrelated modules may also create: EnsureItem only ever assigns the fixed id
    /// on first creation, so if another module gets there first, this bootstrap simply adopts the
    /// existing item by path as before - it never overwrites or moves an id.
    /// </summary>
    public class InitializeColorPickerPalette
    {
        private readonly IContentDatabaseResolver contentDatabaseResolver;

        /// <summary>
        /// <paramref name="contentDatabaseResolver"/> is supplied directly from
        /// SoftConsulting.ColorPicker.Initialize.config, via a nested, typed &lt;param&gt;
        /// element on this processor's &lt;processor&gt; node - not resolved through Sitecore's
        /// DI container. The fallback below only matters if this class is ever constructed some
        /// other way (e.g. directly in a test).
        /// </summary>
        public InitializeColorPickerPalette(IContentDatabaseResolver contentDatabaseResolver)
        {
            this.contentDatabaseResolver = contentDatabaseResolver ?? new ContentDatabaseResolver();
        }

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

        private static readonly ID FoundationFolderItemId = new ID("{9E5C192D-D12E-4E5B-A5EF-323313EC8F88}");
        private static readonly ID TemplatesModuleFolderItemId = new ID("{FC69C389-BCA6-4252-A742-436D6413A59B}");
        private static readonly ID PaletteTemplateItemId = new ID("{692EC644-3522-42DA-9BE1-3BD0E7D55AF1}");
        private static readonly ID DataSectionItemId = new ID("{0B641964-80E9-42A8-B7FD-1030BAE1D93D}");
        private static readonly ID ColorsFieldItemId = new ID("{C481680C-7BCB-420C-9DC1-87F7220E3AF8}");
        private static readonly ID ColorPickerModuleFolderItemId = new ID("{6F97E656-CFC0-43A9-BCB0-883316C71F13}");
        private static readonly ID PalettesModuleFolderItemId = new ID("{D7C6FDCE-60EE-47E6-9054-9A1A0102B6DF}");
        private static readonly ID MaterialDesignPaletteItemId = new ID("{57DAA0F7-77CC-473E-93EC-0E71E7DEFF9D}");
        private static readonly ID FlatUiColorsPaletteItemId = new ID("{5DA7B7C5-0CAF-4E66-9CC7-8DB43C063B71}");

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

        protected virtual IContentDatabaseResolver ContentDatabaseResolver => this.contentDatabaseResolver;

        protected virtual TemplateItem EnsurePaletteTemplate(Database database)
        {
            var templateFolderTemplate = database.Templates[TemplateFolderTemplateId];
            if (templateFolderTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateFolderTemplateId + " not found, skipping palette template.", this);
                return null;
            }

            if (EnsureItem(database, TemplatesFoundationFolderPath, "Foundation", templateFolderTemplate, FoundationFolderItemId) == null)
            {
                return null;
            }

            if (EnsureItem(database, TemplatesModuleFolderPath, "Color Picker", templateFolderTemplate, TemplatesModuleFolderItemId) == null)
            {
                return null;
            }

            var templateTemplate = database.Templates[TemplateTemplateId];
            if (templateTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateTemplateId + " not found, skipping palette template.", this);
                return null;
            }

            var paletteTemplateItem = EnsureItem(database, PaletteTemplatePath, PaletteTemplateName, templateTemplate, PaletteTemplateItemId);
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

            if (EnsureItem(database, DataSectionPath, DataSectionName, sectionTemplate, DataSectionItemId) == null)
            {
                return false;
            }

            var fieldTemplate = database.Templates[TemplateFieldTemplateId];
            if (fieldTemplate == null)
            {
                Log.Error("ColorPicker bootstrap: template " + TemplateFieldTemplateId + " not found, skipping palette template.", this);
                return false;
            }

            var colorsField = EnsureItem(database, ColorsFieldPath, ColorsFieldName, fieldTemplate, ColorsFieldItemId);
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

            var colorPickerFolder = EnsureItem(database, ColorPickerModuleFolderPath, "Color Picker", folderTemplate, ColorPickerModuleFolderItemId);
            if (colorPickerFolder == null)
            {
                return;
            }

            var palettesFolder = EnsureItem(database, PaletteModuleFolderPath, "Palettes", folderTemplate, PalettesModuleFolderItemId);
            if (palettesFolder == null)
            {
                return;
            }

            EnsureSamplePalette(database, palettesFolder, paletteTemplate, "Material Design", MaterialDesignPaletteItemId, MaterialDesignPalette());
            EnsureSamplePalette(database, palettesFolder, paletteTemplate, "Flat UI Colors", FlatUiColorsPaletteItemId, FlatUiColorsPalette());
        }

        protected virtual void EnsureSamplePalette(
            Database database,
            Item parent,
            TemplateItem template,
            string name,
            ID itemId,
            NameValueCollection colors)
        {
            var path = parent.Paths.FullPath + "/" + name;
            if (database.GetItem(path) != null)
            {
                return;
            }

            var item = parent.Add(name, template, itemId);
            using (new EditContext(item))
            {
                new NameValueListField(item.Fields[ColorsFieldName]).NameValues = colors;
            }

            Log.Info("ColorPicker bootstrap: created '" + item.Paths.FullPath + "'.", this);
        }

        // Creates the item at "path" from "template" (with the fixed "itemId") if it doesn't
        // already exist. The item's parent (everything in "path" up to the last segment) must
        // already exist - callers are responsible for ensuring ancestors first, same as the rest
        // of this bootstrap. When the item already exists (e.g. "Foundation" created by another
        // module first), "itemId" is simply ignored - its existing id is left untouched.
        protected virtual Item EnsureItem(Database database, string path, string name, TemplateItem template, ID itemId)
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

            item = parent.Add(name, template, itemId);
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
