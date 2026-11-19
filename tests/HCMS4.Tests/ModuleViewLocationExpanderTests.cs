using System.Reflection;
using HCMS4.Shared.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;

namespace HCMS4.Modules.Sample.Controllers
{
    public class SampleController : Controller { }
}

namespace HCMS4.Tests
{
    public class ModuleViewLocationExpanderTests
    {
        private static ViewLocationExpanderContext ContextFor(Type controllerType)
        {
            var descriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                ControllerName = "Sample",
                ActionName = "Index"
            };
            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
            return new ViewLocationExpanderContext(actionContext, "Index", "Sample", null, null, isMainPage: true)
            {
                // The view engine supplies this dictionary at runtime.
                Values = new Dictionary<string, string?>()
            };
        }

        [Fact]
        public void ModuleController_LooksInItsModuleFolderFirst()
        {
            var expander = new ModuleViewLocationExpander();
            var context = ContextFor(typeof(HCMS4.Modules.Sample.Controllers.SampleController));

            expander.PopulateValues(context);
            var locations = expander.ExpandViewLocations(context, new[] { "/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml" }).ToList();

            Assert.Equal("/Modules/Sample/Views/{1}/{0}.cshtml", locations[0]);
            Assert.Contains("/Views/Shared/{0}.cshtml", locations);
        }

        [Fact]
        public void NonModuleController_KeepsDefaultLocations()
        {
            var expander = new ModuleViewLocationExpander();
            var context = ContextFor(typeof(HCMS4.Shared.Identity.Controllers.AccountController));

            expander.PopulateValues(context);
            var locations = expander.ExpandViewLocations(context, new[] { "/Views/{1}/{0}.cshtml" }).ToList();

            Assert.Equal(new[] { "/Views/{1}/{0}.cshtml" }, locations);
        }
    }
}
