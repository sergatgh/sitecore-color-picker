namespace SoftConsulting.ColorPicker.Infrastructure
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.DependencyInjection;
    using SoftConsulting.ColorPicker.Fields;

    /// <summary>
    /// Registers this module's services with Sitecore's dependency injection container. Wired up
    /// via the "services/configurator" section in SoftConsulting.ColorPicker.Initialize.config:
    ///
    ///   &lt;services&gt;
    ///     &lt;configurator type="SoftConsulting.ColorPicker.Infrastructure.ColorPickerServicesConfigurator, SoftConsulting.ColorPicker" /&gt;
    ///   &lt;/services&gt;
    ///
    /// <see cref="IColorPickerSourceParser"/> is registered here so classes Sitecore creates
    /// outside the container - the "ColorPicker" Sheer field control (resolved through the
    /// Content Editor's XML control factory) and
    /// <see cref="SoftConsulting.ColorPicker.Validators.SwatchValidator"/> (resolved through .NET
    /// serialization when a validator item is loaded) - can still pull it from
    /// <see cref="ServiceLocator"/> at the point of use, since neither can be constructor-injected.
    ///
    /// <see cref="IContentDatabaseResolver"/> is registered too, for the same reason and for
    /// consistency, even though the current consumers that need it -
    /// <see cref="SoftConsulting.ColorPicker.Processors.InitializeSwatchValidator"/> and
    /// <see cref="SoftConsulting.ColorPicker.Processors.InitializeColorPickerPalette"/> - are not
    /// built through this container: they take it as a constructor parameter, and
    /// SoftConsulting.ColorPicker.Initialize.config supplies a <see cref="ContentDatabaseResolver"/>
    /// instance for that parameter directly via a nested, typed &lt;param&gt; element rather than
    /// resolving it here.
    /// </summary>
    public class ColorPickerServicesConfigurator : IServicesConfigurator
    {
        public virtual void Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IColorPickerSourceParser, ColorPickerSourceParser>();
            serviceCollection.AddSingleton<IContentDatabaseResolver, ContentDatabaseResolver>();
        }
    }
}
