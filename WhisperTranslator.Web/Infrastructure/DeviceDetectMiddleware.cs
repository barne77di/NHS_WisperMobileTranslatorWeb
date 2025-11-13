using Microsoft.AspNetCore.Http;

namespace WhisperTranslator.Web.Infrastructure
{
    // Simple UA-based device tagger: sets HttpContext.Items["Device"] = "Mobile"|"Tablet"|"Desktop"
    public sealed class DeviceDetectMiddleware
    {
        private readonly RequestDelegate _next;

        // ✅ REQUIRED: UseMiddleware<T>() will resolve this ctor
        public DeviceDetectMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ua = (context.Request.Headers.UserAgent.ToString() ?? string.Empty).ToLowerInvariant();
            var device = "Desktop";

            if (IsTablet(ua)) device = "Tablet";
            else if (IsMobile(ua)) device = "Mobile";

            context.Items["Device"] = device;
            await _next(context);
        }

        private static bool IsMobile(string ua)
        {
            // phone indicators
            if (ua.Contains("iphone") || ua.Contains("ipod")) return true;
            if (ua.Contains("android") && ua.Contains("mobile")) return true;
            if (ua.Contains("windows phone")) return true;
            if (ua.Contains("blackberry") || ua.Contains("bb10")) return true;
            return false;
        }

        private static bool IsTablet(string ua)
        {
            // iPad / Android tablet (android without "mobile")
            if (ua.Contains("ipad")) return true;
            if (ua.Contains("android") && !ua.Contains("mobile")) return true;
            if (ua.Contains("tablet")) return true;
            return false;
        }
    }
}
