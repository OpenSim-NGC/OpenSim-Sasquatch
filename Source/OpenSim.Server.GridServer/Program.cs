/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using Autofac;
using Autofac.Extensions.DependencyInjection;

using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ConfigurationSubstitution;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers.HttpServer;

using OpenSim.Server.Base;
using OpenSim.Server.Common;
using OpenSim.Server.Handlers;
using OpenSim.Data.MySQL;
using OpenSim.Services;

namespace OpenSim.Server.GridServer
{
    class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand();

            var consoleOption = new Option<string>
                (name: "--console", description: "console type, one of basic, local or rest.", getDefaultValue: () => "local")
                .FromAmong("basic", "local", "rest");
            var inifileOption = new Option<List<string>>
                (name: "--inifile", description: "Specify the location of zero or more .ini file(s) to read.");
            var promptOption = new Option<string>
                (name: "--prompt", description: "Overide the server prompt",
                getDefaultValue: () => "GRID> ");

            rootCommand.AddGlobalOption(consoleOption);
            rootCommand.AddGlobalOption(inifileOption);
            rootCommand.AddGlobalOption(promptOption);

            rootCommand.SetHandler((console, inifile, prompt) =>
            {
                StartGrid(console, inifile, prompt);
            },
            consoleOption, inifileOption, promptOption);

            return await rootCommand.InvokeAsync(args);
        }

        private static void StartGrid(string console, List<string> inifile, string prompt)
        {
            IHostBuilder builder = Host.CreateDefaultBuilder()
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureAppConfiguration(configuration =>
                {
                    //configuration.AddCommandLine(args, switchMappings);
                    configuration.AddIniFile("GridServer.ini", optional: true, reloadOnChange: false);
                    foreach (var item in inifile)
                    {
                        configuration.AddIniFile(item, optional: true, reloadOnChange: true);
                    }
                    configuration.EnableSubstitutions("$(", ")");
                })
                .ConfigureContainer<ContainerBuilder>(builder =>
                {
                    builder.RegisterType<OpenSimServer>().SingleInstance();
                    builder.RegisterType<GridServer>().SingleInstance();

                    builder.RegisterType<BaseHttpServer>().As<IHttpServer>();

                    if (console == "basic")
                        builder.RegisterType<MainConsole>().As<ICommandConsole>().SingleInstance();
                    else if (console == "rest")
                        builder.RegisterType<RemoteConsole>().As<ICommandConsole>().SingleInstance();
                    else if (console == "mock")
                        builder.RegisterType<MockConsole>().As<ICommandConsole>().SingleInstance();
                    else
                        builder.RegisterType<LocalConsole>().As<ICommandConsole>().SingleInstance();
                        
                    // Register Grid Modules
                    builder.RegisterModule(new MySQLDataModule());
                    builder.RegisterModule(new OpenSimServicesModule());
                    builder.RegisterModule(new OpenSimServerHandlersModule());
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<GridService>();
                    services.AddHostedService<PidFileService>();
                });

            IHost host = builder.Build();

            host.Run();
        }
    }
}
