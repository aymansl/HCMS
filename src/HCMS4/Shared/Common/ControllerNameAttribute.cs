using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace HCMS4.Shared.Common
{
    /// <summary>
    /// Gives a controller class a different MVC controller name. Several module controllers can share one name
    /// (for example "Admin"), so the old /Admin/... URLs, links and view folders keep working after the split.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ControllerNameAttribute : Attribute, IControllerModelConvention
    {
        public ControllerNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void Apply(ControllerModel controller)
        {
            controller.ControllerName = Name;
        }
    }
}
