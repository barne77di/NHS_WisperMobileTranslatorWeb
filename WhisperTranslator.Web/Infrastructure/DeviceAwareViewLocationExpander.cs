using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Razor;

namespace WhisperTranslator.Web.Infrastructure
{
    public sealed class DeviceAwareViewLocationExpander : IViewLocationExpander
    {
        public void PopulateValues(ViewLocationExpanderContext context)
        {
            var device = context.ActionContext?.HttpContext?.Items?["Device"] as string ?? "Desktop";
            context.Values["device"] = device;
        }

        public IEnumerable<string> ExpandViewLocations(
            ViewLocationExpanderContext context,
            IEnumerable<string> viewLocations)
        {
            var device = context.Values.TryGetValue("device", out var d) ? d : "Desktop";

            // Try device-specific folders first, then the defaults provided by MVC
            var deviceLocations = new[]
            {
                $"/Views/{device}/{{1}}/{{0}}.cshtml",
                $"/Views/{device}/Shared/{{0}}.cshtml",
            };

            return deviceLocations.Concat(viewLocations);
        }
    }
}
