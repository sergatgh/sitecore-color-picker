namespace SoftConsulting.ColorPicker.Uninstall
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Sitecore.Data;
    using Sitecore.Data.Items;

    public interface IColorPickerValidatorLocator
    {
        IEnumerable<Item> GetModuleValidators(Database database);

        bool IsModuleValidator(Item validatorItem);
    }

    /// <summary>
    /// Finds validator items whose Type would stop loading without this module's assembly: types
    /// declared in it, and consumer types deriving from one of them.
    /// </summary>
    public class ColorPickerValidatorLocator : IColorPickerValidatorLocator
    {
        private const string TypeFieldName = "Type";

        // /sitecore/templates/System/Validation/Validation Rule
        protected static readonly ID ValidationRuleTemplateId = new ID("{8F8E2549-C77F-4860-B83B-54FA31E2BEFA}");

        protected virtual Assembly ModuleAssembly => typeof(ColorPickerValidatorLocator).Assembly;

        public virtual IEnumerable<Item> GetModuleValidators(Database database)
        {
            var items = database?.SelectItems("fast://*[@@templateid='" + ValidationRuleTemplateId + "']") ?? new Item[0];
            return items.Where(IsModuleValidator);
        }

        public virtual bool IsModuleValidator(Item validatorItem)
        {
            var typeName = validatorItem?[TypeFieldName]?.Trim();
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            if (string.Equals(GetAssemblyName(typeName), ModuleAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var type = ResolveType(typeName);
            return type != null && InheritsFromModule(type);
        }

        // "Namespace.Type, Assembly[, Version=..., ...]" -> "Assembly".
        protected virtual string GetAssemblyName(string typeName)
        {
            var parts = typeName.Split(',');
            return parts.Length < 2 ? null : parts[1].Trim();
        }

        protected virtual Type ResolveType(string typeName)
        {
            try
            {
                return Type.GetType(typeName, false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected virtual bool InheritsFromModule(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.Assembly == ModuleAssembly)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
