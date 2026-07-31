namespace SoftConsulting.ColorPicker.Infrastructure
{
    using System.IO;
    using System.Web;
    using System.Web.Mvc;
    using System.Web.Routing;

    /// <summary>
    /// Renders a Razor (.cshtml) view to a string outside of a real MVC request/routing
    /// pipeline. Used by field controls that run inside the Sitecore Content Editor
    /// (Sheer/WebForms), where there is no controller action to return a ViewResult from -
    /// only a real ASP.NET HttpContext (HttpContext.Current), which is what makes faking a
    /// minimal ControllerContext viable here.
    ///
    /// Generic and Sitecore-agnostic - reusable by any other Razor-rendered control.
    /// </summary>
    public static class RazorViewRenderer
    {
        /// <summary>
        /// Renders the view at <paramref name="virtualPath"/> (e.g.
        /// "~/Views/ColorPicker/Field.cshtml") with the given model and returns the resulting
        /// HTML. Any failure (view not found, Razor compile error, runtime error in the view)
        /// propagates to the caller - this method does not swallow exceptions, so callers
        /// remain in full control of fallback behavior.
        /// </summary>
        public static string RenderViewToString(string virtualPath, object model)
        {
            var httpContextWrapper = new HttpContextWrapper(HttpContext.Current);
            var controllerContext = new ControllerContext(httpContextWrapper, new RouteData(), new FakeController());

            // RazorViewEngine.CreateView is protected, so it can't be called directly from here -
            // construct the System.Web.Mvc.RazorView it would have returned ourselves instead
            // (its constructor is public). This is safe because the virtual path is already
            // known exactly; there's no view-location probing to delegate to the engine for.
            IView view = new RazorView(controllerContext, virtualPath, layoutPath: null, runViewStartPages: false, viewStartFileExtensions: null);

            using (var writer = new StringWriter())
            {
                var viewData = new ViewDataDictionary { Model = model };
                var viewContext = new ViewContext(controllerContext, view, viewData, new TempDataDictionary(), writer);
                view.Render(viewContext, writer);
                return writer.ToString();
            }
        }

        /// <summary>
        /// Exists only to satisfy ControllerContext's constructor, which requires a
        /// ControllerBase. Never executed as an actual controller.
        /// </summary>
        private sealed class FakeController : Controller
        {
        }
    }
}
