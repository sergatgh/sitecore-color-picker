namespace SoftConsulting.ColorPicker.Admin
{
    using System;
    using System.IO;
    using System.Web;
    using System.Web.SessionState;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Sitecore.DependencyInjection;
    using SoftConsulting.ColorPicker.Infrastructure;
    using SoftConsulting.ColorPicker.Uninstall;

    /// <summary>
    /// JSON API backing sitecore/admin/ColorPicker/Uninstall.js's wizard. Every action here maps
    /// straight onto one <see cref="IColorPickerUsageScanner"/>/<see cref="IColorPickerUninstaller"/>
    /// member - this class only routes, authenticates, and serializes; no business logic of its
    /// own.
    ///
    /// Gated by <see cref="EnsureAdministrator"/>, the same check
    /// SoftConsulting.ColorPicker.Admin.UninstallPage uses - either entry point alone
    /// is fully gated. Every state-changing (POST) action additionally requires the per-page-load
    /// anti-forgery token <see cref="UninstallPage"/> stores in <c>Session</c> and
    /// hands to the page's JS - see <see cref="ValidateToken"/>. Implements
    /// <see cref="IRequiresSessionState"/> (a marker interface with no members) because plain
    /// <see cref="IHttpHandler"/>s don't get <see cref="HttpContext.Session"/> populated by ASP.NET
    /// otherwise - without it, <see cref="ValidateToken"/> would always see a null session and
    /// reject every POST.
    /// </summary>
    public class UninstallApiHandler : IHttpHandler, IRequiresSessionState
    {
        public virtual bool IsReusable => false;

        protected virtual IColorPickerUsageScanner UsageScanner
            => ServiceLocator.ServiceProvider.GetService<IColorPickerUsageScanner>()
               ?? new ColorPickerUsageScanner(new ContentDatabaseResolver());

        protected virtual IColorPickerUninstaller Uninstaller
            => ServiceLocator.ServiceProvider.GetService<IColorPickerUninstaller>()
               ?? new ColorPickerUninstaller(new ContentDatabaseResolver(), UsageScanner);

        public virtual void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.Cache.SetNoStore();

            if (!EnsureAdministrator(context))
            {
                return;
            }

            var isPost = string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase);
            if (isPost && !ValidateToken(context))
            {
                WriteError(context, 403, "Invalid or missing anti-forgery token - reload the page and try again.");
                return;
            }

            try
            {
                Dispatch(context, context.Request["action"]);
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error("ColorPicker uninstall API error: " + ex.Message, ex, this);
                WriteError(context, 500, "Unexpected error: " + ex.Message);
            }
        }

        protected virtual void Dispatch(HttpContext context, string action)
        {
            switch (action)
            {
                case "scan":
                    WriteJson(context, UsageScanner.Scan());
                    return;
                case "manifest":
                    WriteJson(context, Uninstaller.GetRemovalManifest());
                    return;
                case "convertField":
                    {
                        var body = ReadJsonBody(context);
                        WriteJson(context, Uninstaller.ConvertFieldToSingleLineText(
                            body.Value<string>("databaseName"), body.Value<string>("itemId")));
                        return;
                    }
                case "convertAllFields":
                    WriteJson(context, Uninstaller.ConvertAllFieldsToSingleLineText());
                    return;
                case "removeValidatorReference":
                    {
                        var body = ReadJsonBody(context);
                        WriteJson(context, Uninstaller.RemoveValidatorReference(
                            body.Value<string>("databaseName"), body.Value<string>("itemId"), body.Value<string>("fieldId")));
                        return;
                    }
                case "removeAllValidatorReferences":
                    WriteJson(context, Uninstaller.RemoveAllValidatorReferences());
                    return;
                case "removePaletteItem":
                    {
                        var body = ReadJsonBody(context);
                        WriteJson(context, Uninstaller.RemovePaletteItem(
                            body.Value<string>("databaseName"), body.Value<string>("itemId")));
                        return;
                    }
                case "removeAllPaletteItems":
                    WriteJson(context, Uninstaller.RemoveAllPaletteItems());
                    return;
                case "removeModule":
                    {
                        var result = Uninstaller.RemoveModuleItems();
                        WriteJson(context, new
                        {
                            result.Success,
                            result.Skipped,
                            result.Message,
                            result.DeletedPaths,
                            ManualCleanupFiles = Uninstaller.GetManualCleanupFiles(),
                        });
                        return;
                    }
                default:
                    WriteError(context, 400, "Unknown action.");
                    return;
            }
        }

        /// <summary>
        /// No item/config dependency - just the authenticated Sitecore user's own administrator
        /// flag, the same gate Sitecore's own admin pages (and ColorPickerUninstallPage) use.
        /// </summary>
        protected virtual bool EnsureAdministrator(HttpContext context)
        {
            var user = Sitecore.Context.User;
            if (user != null && user.IsAuthenticated && user.IsAdministrator)
            {
                return true;
            }

            WriteError(context, 403, "Forbidden: this endpoint requires an authenticated Sitecore administrator.");
            return false;
        }

        protected virtual bool ValidateToken(HttpContext context)
        {
            var expected = context.Session?[ColorPickerUninstallPage.TokenSessionKey] as string;
            var provided = context.Request.Headers["X-ColorPicker-Token"];
            return !string.IsNullOrEmpty(expected) && string.Equals(expected, provided, StringComparison.Ordinal);
        }

        protected virtual JObject ReadJsonBody(HttpContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                var text = reader.ReadToEnd();
                return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
            }
        }

        protected virtual void WriteJson(HttpContext context, object value)
            => context.Response.Write(JsonConvert.SerializeObject(value));

        protected virtual void WriteError(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Write(JsonConvert.SerializeObject(new { success = false, message }));
        }
    }
}
