using System.CommandLine;

using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenSim.Data.Model;
using OpenSim.Data.Model.Core;
using OpenSim.Data.Model.Economy;
using OpenSim.Data.Model.Identity;
using OpenSim.Data.Model.Region;
using OpenSim.Data.Model.Search;

namespace OpenSim.Server.HyperGrid;

public class Program
{
    public static void SetCommandLineArgs(string console, List<string> inifiles, string prompt)
    {

    }

    public static void Main(string[] args)
    {
        // var rootCommand = new RootCommand("Grid Server");

        // var consoleOption = new Option<string>
        //     (name: "--console", description: "console type, one of basic, local or rest.", getDefaultValue: () => "local")
        //     .FromAmong("basic", "local", "rest");
        // var inifileOption = new Option<List<string>>
        //     (name: "--inifile", description: "Specify the location of zero or more .ini file(s) to read.");
        // var promptOption = new Option<string>
        //     (name: "--prompt", description: "Overide the server prompt",
        //     getDefaultValue: () => "GRID> ");

        // rootCommand.Add(consoleOption);
        // rootCommand.Add(inifileOption);
        // rootCommand.Add(promptOption);
        
        // rootCommand.SetHandler(
        //     (consoleOptionValue, inifileOptionValue, promptOptionValue) =>
        //     {
        //         SetCommandLineArgs(consoleOptionValue, inifileOptionValue, promptOptionValue);
        //     },
        //     consoleOption, inifileOption, promptOption);

        // await rootCommand.InvokeAsync(args);

        // Create Builder and run program
        var builder = WebApplication.CreateBuilder(args);

        // builder.Configuration.AddCommandLine(args, switchMappings);

        // builder.Configuration.EnableSubstitutions("$(", ")");
        builder.Configuration.AddIniFile("GridServer.ini", optional: true, reloadOnChange: false);

        // foreach (var item in inifile)
        // {
        //     builder.Configuration.AddIniFile(item, optional: true, reloadOnChange: true);
        // }

        // Initialize Database
        var connectionString = builder.Configuration.GetConnectionString("IdentityConnection");
        builder.Services.AddDbContext<IdentityContext>(
            options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        var coreConnectionString = builder.Configuration.GetConnectionString("OpenSimCoreConnection");
        builder.Services.AddDbContext<OpenSimCoreContext>(
            options => options.UseMySql(coreConnectionString, ServerVersion.AutoDetect(coreConnectionString)));

        var regionConnectionString = builder.Configuration.GetConnectionString("OpenSimRegionConnection");
        builder.Services.AddDbContext<OpenSimRegionContext>(
            options => options.UseMySql(regionConnectionString, ServerVersion.AutoDetect(regionConnectionString)));

        var economyConnectionString = builder.Configuration.GetConnectionString("OpenSimEconomyConnection");
        builder.Services.AddDbContext<OpenSimEconomyContext>(
            options => options.UseMySql(economyConnectionString, ServerVersion.AutoDetect(economyConnectionString)));

        var searchConnectionString = builder.Configuration.GetConnectionString("OpenSimSearchConnection");
        builder.Services.AddDbContext<OpenSimSearchContext>(
            options => options.UseMySql(searchConnectionString, ServerVersion.AutoDetect(searchConnectionString)));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddControllers()
            .AddXmlDataContractSerializerFormatters();
        
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}