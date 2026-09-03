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
        /// Every content database this module should scan for usage - at minimum
        /// <see cref="GetContentDatabase"/>'s result.
        /// </summary>
        IEnumerable<Database> GetContentDatabases();
    }

    /// <summary>
    /// Resolves the database content editors author in (typically "master"), via the "shell"
    /// site's contentDatabase setting rather than a hardcoded name, so this keeps working on
    /// instances that rename or repoint their authoring database.
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
        /// "web") by name alone, and scanning one would double-report usage findings. Instances
        /// with more than one real content database should override
        /// <see cref="GetAdditionalContentDatabaseNames"/> via their own subclass.
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
