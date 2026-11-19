using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

namespace HCMS4.Shared.Common
{
    /// <summary>
    /// Lets a controller in HCMS4.Modules.{Module}.Controllers find its views in /Modules/{Module}/Views/{Controller}/,
    /// before the default /Views/{Controller}/ and /Views/Shared/ locations.
    /// </summary>
    public class ModuleViewLocationExpander : IViewLocationExpander
    {
        private const string ModuleKey = "module";
        private static readonly Regex ModuleNamespace = new(@"^HCMS4\.Modules\.(\w+)\.", RegexOptions.Compiled);

        public void PopulateValues(ViewLocationExpanderContext context)
        {
            if (context.ActionContext.ActionDescriptor is ControllerActionDescriptor descriptor)
            {
                var match = ModuleNamespace.Match(descriptor.ControllerTypeInfo.Namespace ?? string.Empty);
                if (match.Success)
                {
                    context.Values[ModuleKey] = match.Groups[1].Value;
                }
            }
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            if (context.Values.TryGetValue(ModuleKey, out var module) && !string.IsNullOrEmpty(module))
            {
                // {1} = controller name, {0} = view name.
                return new[] { $"/Modules/{module}/Views/{{1}}/{{0}}.cshtml" }.Concat(viewLocations);
            }

            return viewLocations;
        }
    }
}
