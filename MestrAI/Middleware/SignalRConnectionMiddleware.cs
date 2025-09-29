using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace RPGSessionManager.Middleware
{
    public class SignalRConnectionMiddleware
    {
        private readonly RequestDelegate _next;

        public SignalRConnectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Example: Log connection attempts or add custom headers
            // Console.WriteLine($"SignalR connection attempt: {context.Request.Path}");

            await _next(context);
        }
    }
}

