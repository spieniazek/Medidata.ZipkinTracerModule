using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using Microsoft.AspNetCore.Builder;

namespace Medidata.ZipkinTracer.Core.Middlewares
{
    public class ZipkinMiddleware
    {
        private readonly IZipkinConfig _config;

        private readonly RequestDelegate _next;

        public ZipkinMiddleware(RequestDelegate next, IZipkinConfig options)
        {
            _config = options;
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (_config.Bypass != null && _config.Bypass(context.Request))
            {
                await _next(context);
                return;
            }

            var zipkin = new ZipkinClient(_config, context);
            var span = await zipkin.StartServerTrace(new Uri(context.Request.GetEncodedUrl()), context.Request.Method);
            await _next(context);
            zipkin.EndServerTrace(span);
        }
    }

    public static class AppBuilderExtensions
    {
        public static void UseZipkin(this IApplicationBuilder app, IZipkinConfig config)
        {
            config.Validate();
            app.UseMiddleware<ZipkinMiddleware>(config);
        }
    }
}