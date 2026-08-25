namespace SoftConsulting.ColorPicker.Admin
{
    using System;
    using System.Web.UI;

    /// <summary>
    /// Admin page at /sitecore/admin/ColorPicker/Uninstall.aspx - a static wizard shell (see the
    /// .aspx markup and its Uninstall.css/.js siblings) driven entirely by AJAX calls
    /// into UninstallApi.ashx. This code-behind does only two things: gate access to
    /// authenticated Sitecore administrators (<see cref="EnsureAdministrator"/> - the API handler
    /// repeats this same check independently, so either entry point alone is fully gated) and
    /// hand the page a one-time anti-forgery token (<see cref="TokenSessionKey"/>) for its JS to
    /// send back on every mutating API call.
    ///
    /// Depends on no Sitecore item - only this compiled page and the two services it and the API
    /// handler resolve via <see cref="Sitecore.DependencyInjection.ServiceLocator"/> - so, unlike
    /// a Control Panel button/wizard, it can never end up "half removed": whatever state the
    /// module's own items are in, this page still loads and still reports on them accurately. It
    /// deliberately never tries to delete itself - not because it couldn't (unlike the module's
    /// DLL, this page's own files aren't locked by the running process), but for the reasons in
    /// <see cref="Uninstall.IColorPickerUninstaller.GetManualCleanupFiles"/>'s remarks.
    /// </summary>
    public class ColorPickerUninstallPage : Page
    {
        public const string TokenSessionKey = "ColorPickerUninstallToken";

        protected virtual string Token { get; private set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (!EnsureAdministrator())
            {
                return;
            }

            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            Token = Guid.NewGuid().ToString("N");
            Session[TokenSessionKey] = Token;
        }

        /// <summary>
        /// No item/config dependency - just the authenticated Sitecore user's own administrator
        /// flag, the same gate Sitecore's own admin pages use.
        /// </summary>
        protected virtual bool EnsureAdministrator()
        {
            var user = Sitecore.Context.User;
            if (user != null && user.IsAuthenticated && user.IsAdministrator)
            {
                return true;
            }

            Response.StatusCode = 403;
            Response.Write("Forbidden: this page requires an authenticated Sitecore administrator.");
            Response.End();
            return false;
        }
    }
}
