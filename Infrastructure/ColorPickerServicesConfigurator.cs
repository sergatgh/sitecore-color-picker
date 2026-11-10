namespace SoftConsulting.ColorPicker.Infrastructure
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.DependencyInjection;
    using SoftConsulting.ColorPicker.Fields;
    using SoftConsulting.ColorPicker.Uninstall;

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
