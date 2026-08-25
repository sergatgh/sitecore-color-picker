namespace SoftConsulting.ColorPicker.Infrastructure
{
    using System.Collections.Generic;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Diagnostics;

    public interface IContentDatabaseResolver
    {
        Database GetContentDatabase();

        /// <summary>
        /// Every content database this module should provision into / scan for usage - at
        /// minimum <see cref="GetContentDatabase"/>'s result. See
        /// <see cref="ContentDatabaseResolver.GetAdditionalContentDatabaseNames"/> for how to
        /// add more on installs with multiple authoring databases.
        /// </summary>
        IEnumerable<Database> GetContentDatabases();
    }

    /// <summary>
    /// Resolves the database content editors author in (typically "master"), via the "shell"
    /// site's contentDatabase setting rather than a hardcoded database name, so bootstrap
    /// processors keep working on instances that rename or repoint their authoring database.
    /// Shared by every initialize-pipeline processor in this module that needs to provision
    /// items in the content database (as opposed to "core"), and by
    /// <see cref="SoftConsulting.ColorPicker.Uninstall.IColorPickerUsageScanner"/> to know which
    /// databases to scan for module usage before an uninstall.
    /// </summary>
    public class ContentDatabaseResolver : IContentDatabaseResolver
    {
        private const string ShellSiteName = "shell";
        private const string DefaultContentDatabaseName = "master";

        public virtual Database GetContentDatabase()
        {
            var shellSiteInfo = Factory.GetSiteInfo(ShellSiteName);
            var databaseName = shellSiteInfo?.MasterDatabase;

            if (string.IsNullOrEmpty(databaseName))
            {
                Log.Warn(
                    "ColorPicker bootstrap: could not resolve contentDatabase from the '" + ShellSiteName +
                    "' site definition, falling back to '" + DefaultContentDatabaseName + "'.",
                    this);
                databaseName = DefaultContentDatabaseName;
            }

            return Factory.GetDatabase(databaseName, false);
        }

        /// <summary>
        /// Deliberately does not default to "every database Sitecore knows about minus core" -
        /// there is no reliable way to tell an authoring database from a publishing target (e.g.
        /// "web") by name alone, and scanning a publishing target would double-report every
        /// usage finding as if it came from a second, independent authoring database. Instances
        /// with more than one real content database (e.g. a second tenant's authoring database)
        /// should override <see cref="GetAdditionalContentDatabaseNames"/> - via a patch config
        /// swapping in a subclass, the same extensibility pattern this class itself already
        /// supports (see SoftConsulting.ColorPicker.Initialize.config's header comment).
        /// </summary>
        public virtual IEnumerable<Database> GetContentDatabases()
        {
            var primary = GetContentDatabase();
            if (primary != null)
            {
                yield return primary;
            }

            foreach (var name in GetAdditionalContentDatabaseNames())
            {
                if (string.IsNullOrEmpty(name) || (primary != null && name == primary.Name))
                {
                    continue;
                }

                var database = Factory.GetDatabase(name, false);
                if (database != null)
                {
                    yield return database;
                }
            }
        }

        /// <summary>
        /// No additional content databases by default - override to name more (e.g. a second
        /// tenant's authoring database) on instances where <see cref="GetContentDatabase"/>
        /// alone does not cover every place this module's items and fields might live.
        /// </summary>
        protected virtual IEnumerable<string> GetAdditionalContentDatabaseNames() => new string[0];
    }
}
