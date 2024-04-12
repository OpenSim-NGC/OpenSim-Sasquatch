using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenSim.ApplicationPlugins.LoadRegions;
using OpenSim.ApplicationPlugins.RegionModulesController;
using OpenSim.ApplicationPlugins.RemoteController;
using OpenSim.Region.Framework;
using OpenSim.Region.OptionalModules;

namespace OpenSim.Server.RegionServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            // In ASP.NET Core 3.x, using `Host.CreateDefaultBuilder` (as in the preceding Program.cs snippet) will
            // set up some configuration for you based on your appsettings.json and environment variables. See "Remarks" at
            // https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.host.createdefaultbuilder for details.
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; private set; }

        public ILifetimeScope AutofacContainer { get; private set; }

        // ConfigureServices is where you register dependencies. This gets
        // called by the runtime before the ConfigureContainer method, below.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add services to the collection. Don't build or return
            // any IServiceProvider or the ConfigureContainer method
            // won't get called. Don't create a ContainerBuilder
            // for Autofac here, and don't call builder.Populate() - that
            // happens in the AutofacServiceProviderFactory for you.
            services.AddOptions();
            services.AddHostedService<RegionService>();
        }

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you by the factory.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            // Register your own things directly with Autofac here. Don't
            // call builder.Populate(), that happens in AutofacServiceProviderFactory
            // for you.
            // Startup Application Plugins
            builder.RegisterType<LoadRegionsPlugin>().As<IApplicationPlugin>().SingleInstance();
            builder.RegisterType<RegionModulesControllerPlugin>().As<IApplicationPlugin>().SingleInstance();
            builder.RegisterType<RemoteAdminPlugin>().As<IApplicationPlugin>().SingleInstance();

            // Register Modules
            builder.RegisterModule(new OptionalModulesModule());
//            Container = builder.Build();
//            return container.Resolve<IServiceProvider>();
        }

        //// Configure is where you add middleware. This is called after
        //// ConfigureContainer. You can use IApplicationBuilder.ApplicationServices
        //// here if you need to resolve things from the container.
        //public void Configure(
        //  IApplicationBuilder app,
        //  ILoggerFactory loggerFactory)
        //{
        //    // If, for some reason, you need a reference to the built container, you
        //    // can use the convenience extension method GetAutofacRoot.
        //    this.AutofacContainer = app.ApplicationServices.GetAutofacRoot();

        //    loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
        //    loggerFactory.AddDebug();
        //    app.UseMvc();
        //}
    }
}
