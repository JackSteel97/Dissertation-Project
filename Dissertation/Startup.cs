using Dissertation.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace Dissertation
{
    public class Startup
    {
        private readonly AppSettingsService AppSettings = new AppSettingsService();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            Configuration.GetSection("Application").Bind(AppSettings);

            // Uncomment when publishing to azure.
            //AppSettings.DatabaseConnection = configuration.GetConnectionString("DatabaseConnection");
            //AppSettings.Accommodations = JsonConvert.DeserializeObject<string[]>(configuration["Accommodations"]);
            //AppSettings.EdgeCaseWeights = JsonConvert.DeserializeObject<EdgeCaseWeightsConfiguration>(configuration["EdgeCaseWeights"]);
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews().AddNewtonsoftJson(options => options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

            // Add the 'AppSettingsService' singleton for DI.
            services.AddSingleton(AppSettings);

            // Add database context.
            services.AddDbContext<DissDatabaseContext>(options => options.UseSqlServer(AppSettings.DatabaseConnection, providerOptions => providerOptions.EnableRetryOnFailure()));
            services.AddApplicationInsightsTelemetry();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();

            // Use static files.
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    const int durationInSeconds = 3600;
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + durationInSeconds;
                }
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}