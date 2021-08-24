using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using PDF2Image.Hubs;
using PDF2Image.Services;
using System.IO;

namespace PDF2Image
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
            services.AddSingleton<OcrService>();
            services.AddSingleton<IHostedService>(p => p.GetService<OcrService>());

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Swagger.Api", Version = "v1" });
            });
            services.AddControllers().AddApplicationPart(this.GetType().Assembly).AddControllersAsServices();

            services.AddSignalR().AddMessagePackProtocol(options =>
            {
                //options.SerializerOptions = MessagePackSerializerOptions.Standard
                //    //.WithResolver(new CustomResolver())
                //    .WithSecurity(MessagePackSecurity.UntrustedData);
                //StaticCompositeResolver.Instance.Register(DynamicGenericResolver.Instance, StandardResolver.Instance);
                //options.SerializerOptions = MessagePackSerializerOptions.Standard
                //    .WithResolver(StaticCompositeResolver.Instance)
                //    .WithSecurity(MessagePackSecurity.UntrustedData);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseStaticFiles();
            foreach (var dir in Directory.GetDirectories(env.WebRootPath))
            {
                string folder = Path.GetFileName(dir);
                string dirTest = Path.Combine(env.WebRootPath, folder);
                app.UseDirectoryBrowser(new DirectoryBrowserOptions
                {
                    FileProvider = new PhysicalFileProvider(dirTest),
                    RequestPath = "/" + folder
                });
            }

            //--------------------------------------------------------

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
                endpoints.MapHub<ImageHub>("/hubs/image");

                endpoints.MapControllers();
                endpoints.MapGet("/", async context =>
                {
                    context.Response.Redirect("/test");
                    //context.Response.Redirect("/swagger");
                    await System.Threading.Tasks.Task.CompletedTask;
                });
            });
        }
    }
}
