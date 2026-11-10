namespace SoftConsulting.ColorPicker.Infrastructure
{
    using System.Collections.Generic;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Diagnostics;

    public interface IContentDatabaseResolver
    {
        Database GetContentDatabase();

        IEnumerable<Database> GetContentDatabases();
    }

    /// <summary>
    /// Reads the "shell" site's contentDatabase rather than hardcoding "master", so a renamed
    /// authoring database still works.
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
        /// Doesn't scan "all databases but core": publishing targets like "web" can't be told apart
        /// by name and would double-report usage. Override
        /// <see cref="GetAdditionalContentDatabaseNames"/> to add more.
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

        protected virtual IEnumerable<string> GetAdditionalContentDatabaseNames() => new string[0];
    }
}
