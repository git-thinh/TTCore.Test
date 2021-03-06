using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Hangfire.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hangfire
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IJobTestService, JobTestService>();

            services.AddHangfire(config =>
            {
                config.UseMemoryStorage();
            });
            services.AddHangfireServer();

            services.AddMvc();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Swagger.Api", Version = "v1" });
            });
            services.AddControllers().AddApplicationPart(this.GetType().Assembly).AddControllersAsServices();
        }

        public void Configure(IApplicationBuilder app, IBackgroundJobClient backgroundJobs, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIT.UI.Api v1"));

            backgroundJobs.Enqueue(() => Console.WriteLine("[1] - Hello world from Hangfire!"));
            BackgroundJob.Enqueue(() => Console.WriteLine("[2] - " + Guid.NewGuid().ToString()));

            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });

                endpoints.MapControllers();

                endpoints.MapHangfireDashboard("/task", new DashboardOptions
                {
                    // Change `Back to site` link URL
                    AppPath = "/",
                    DashboardTitle = "Task",
                    IsReadOnlyFunc = (DashboardContext context) => true
                });
            });
        }
    }
}
