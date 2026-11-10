namespace SoftConsulting.ColorPicker.Admin
{
    using System;
    using System.Web.UI;

    /// <summary>
    /// Wizard shell driven by UninstallApi.ashx. Depends on no Sitecore item, so it can't end up
    /// half-removed, and never deletes itself.
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
