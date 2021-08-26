using Driver.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Redis;
using System.IO;

namespace Driver
{
    public class Startup
    {
        IConfiguration _configuration { get; }
        IWebHostEnvironment _environment { get; }
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _environment = env;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DriverService>();
            services.AddSingleton<IHostedService>(p => p.GetService<DriverService>());

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Swagger.Api", Version = "v1" });
            });
            services.AddControllers().AddApplicationPart(this.GetType().Assembly).AddControllersAsServices();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIT.UI.Api v1"));

            //--------------------------------------------------------

            app.UseRouting();
            app.UseCors(x => x
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapHub<ImageHub>("/hubs/image");

                endpoints.MapControllers();
                endpoints.MapGet("/", async context =>
                {
                    //context.Response.Redirect("/test");
                    context.Response.Redirect("/swagger");
                    await System.Threading.Tasks.Task.CompletedTask;
                });
            });
        }
    }
}
