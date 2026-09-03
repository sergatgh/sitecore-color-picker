namespace SoftConsulting.ColorPicker.Infrastructure
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.DependencyInjection;
    using SoftConsulting.ColorPicker.Fields;
    using SoftConsulting.ColorPicker.Uninstall;

    /// <summary>
    /// Registers this module's services with Sitecore's dependency injection container, wired up
    /// via the "services/configurator" section in SoftConsulting.ColorPicker.Services.config.
    /// Everything here is registered so classes Sitecore creates outside the container - the
    /// "ColorPicker" Sheer field control, <see cref="SoftConsulting.ColorPicker.Validators.SwatchValidator"/>,
    /// the admin uninstall page/API, and the PowerShell uninstall script - can still pull these
    /// services from <see cref="ServiceLocator"/> at the point of use, since none of them can be
    /// constructor-injected.
    /// </summary>
    public class ColorPickerServicesConfigurator : IServicesConfigurator
    {
        public virtual void Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IColorPickerSourceParser, ColorPickerSourceParser>();
            serviceCollection.AddSingleton<IContentDatabaseResolver, ContentDatabaseResolver>();
            serviceCollection.AddSingleton<IColorPickerUsageScanner, ColorPickerUsageScanner>();
            serviceCollection.AddSingleton<IColorPickerUninstaller, ColorPickerUninstaller>();
        }
    }
}
