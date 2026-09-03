namespace SoftConsulting.ColorPicker.Admin
{
    using System;
    using System.Web.UI;

    /// <summary>
    /// Admin page at /sitecore/admin/ColorPicker/Uninstall.aspx - a static wizard shell driven
    /// entirely by AJAX calls into UninstallApi.ashx. This code-behind only gates access to
    /// authenticated Sitecore administrators (<see cref="EnsureAdministrator"/>) and hands the
    /// page a one-time anti-forgery token (<see cref="TokenSessionKey"/>) for its JS to send back
    /// on every mutating API call. It depends on no Sitecore item, so it can never end up "half
    /// removed" and deliberately never tries to delete itself - see
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
