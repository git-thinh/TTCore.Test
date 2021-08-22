using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Redis
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
            var _redisSetting = new RedisSetting();
            _configuration.GetSection("Redis").Bind(_redisSetting);
            services.AddSingleton<RedisSetting>(_redisSetting);

            services.AddSingleton<RedisService>();
            services.AddSingleton<IHostedService>(p => p.GetService<RedisService>());

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
                endpoints.MapHub<RedisHub>("/hubs/redis");

                endpoints.MapControllers();
                endpoints.MapGet("/", async context =>
                {
                    context.Response.Redirect("/swagger");
                    await System.Threading.Tasks.Task.CompletedTask;
                });
            });
        }
    }
}
