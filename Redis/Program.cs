using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;

namespace Redis
{
    public class Program
    {
        private static IServiceProvider _service = null;
        public static IServiceProvider Service { get { return _service; } }
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            _service = host.Services;
            host.Run();
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    Type typeStartup = typeof(Startup);
                    webBuilder.UseStartup(typeStartup);
                })
                .ConfigureServices(services =>
                {
                    //services.AddHostedService<RedisService>();
                });
            return host;
        }
    }
}
