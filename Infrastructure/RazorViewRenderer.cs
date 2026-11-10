namespace SoftConsulting.ColorPicker.Infrastructure
{
    using System.IO;
    using System.Web;
    using System.Web.Mvc;
    using System.Web.Routing;

    /// <summary>
    /// Renders a Razor view to a string outside the MVC pipeline (e.g. from Sheer/WebForms
    /// controls) by faking a minimal ControllerContext. Exceptions propagate to the caller.
    /// </summary>
    public static class RazorViewRenderer
    {
        public static string RenderViewToString(string virtualPath, object model)
        {
            var httpContextWrapper = new HttpContextWrapper(HttpContext.Current);
            var controllerContext = new ControllerContext(httpContextWrapper, new RouteData(), new FakeController());

            // RazorViewEngine.CreateView is protected; the path is exact, so no view probing is needed.
            IView view = new RazorView(controllerContext, virtualPath, layoutPath: null, runViewStartPages: false, viewStartFileExtensions: null);

            using (var writer = new StringWriter())
            {
                var viewData = new ViewDataDictionary { Model = model };
                var viewContext = new ViewContext(controllerContext, view, viewData, new TempDataDictionary(), writer);
                view.Render(viewContext, writer);
                return writer.ToString();
            }
        }

        // Only satisfies ControllerContext's constructor; never executed.
        private sealed class FakeController : Controller
        {
        }
    }
}
