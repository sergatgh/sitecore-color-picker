namespace SoftConsulting.ColorPicker.Infrastructure
{
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Diagnostics;

    public interface IContentDatabaseResolver
    {
        Database GetContentDatabase();
    }

    /// <summary>
    /// Resolves the database content editors author in (typically "master"), via the "shell"
    /// site's contentDatabase setting rather than a hardcoded database name, so bootstrap
    /// processors keep working on instances that rename or repoint their authoring database.
    /// Shared by every initialize-pipeline processor in this module that needs to provision
    /// items in the content database (as opposed to "core").
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
    }
}
