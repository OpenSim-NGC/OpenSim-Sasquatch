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
 *     * Neither the name of the OpenSimulator Project nor the
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

using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Monitoring;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Runtime.CompilerServices;

namespace OpenSim.Server.Base
{
    public class HttpServerBase : ServicesServerBase
    {
        private uint m_consolePort;

        private IServiceProvider m_provider;
        private IConfiguration m_configuration;
        private ILogger<HttpServerBase> m_logger;

        public IServiceProvider Provider { get => m_provider; set => m_provider = value; }

        public HttpServerBase(
            IServiceProvider provider,
            IConfiguration configuration,
            ILogger<HttpServerBase> logger,
            ServerStatsCollector statsCollector) 
            : base(configuration, logger, statsCollector)
        {
            Provider = provider;
            m_configuration = configuration;
            m_logger = logger;
        }

        protected void ReadConfig()
        {
            var networkConfig = m_configuration.GetSection("Network");
            if (networkConfig.Exists() is false)
            {
                System.Console.WriteLine("ERROR: Section [Network] not found, server can't start");
                Environment.Exit(1);
            }

            uint port = networkConfig.GetValue<uint>("port", 0);
            if (port == 0)
            {
                System.Console.WriteLine("ERROR: No 'port' entry found in [Network].  Server can't start");
                Environment.Exit(1);
            }

            bool ssl_main = networkConfig.GetValue<bool>("https_main",false);
            bool ssl_listener = networkConfig.GetValue<bool>("https_listener",false);
            bool ssl_external = networkConfig.GetValue<bool>("https_external",false);

            m_consolePort = networkConfig.GetValue<uint>("ConsolePort", 0);

            IHttpServer httpServer = null;

            //
            // This is where to make the servers:
            //
            //
            // Make the base server according to the port, etc.
            // ADD: Possibility to make main server ssl
            // Then, check for https settings and ADD a server to
            // m_Servers
            //
            if (!ssl_main)
            {
                httpServer = MainServer.GetHttpServer(null, port);  ///XXX MCD
            }
            else
            {
                string cert_path = networkConfig.GetValue("cert_path", string.Empty);
                if (string.IsNullOrEmpty(cert_path))
                {
                    System.Console.WriteLine("ERROR: Path to X509 certificate is missing, server can't start.");
                    Environment.Exit(1);
                }

                string cert_pass = networkConfig.GetValue("cert_pass", string.Empty);
                if (string.IsNullOrEmpty(cert_pass))
                {
                    System.Console.WriteLine("ERROR: Password for X509 certificate is missing, server can't start.");
                    Environment.Exit(1);
                }

                httpServer = MainServer.GetHttpServer(null, port, ipaddr: null, ssl_main, cert_path, cert_pass); //XXX MCD
            }

            // If https_listener = true, then add an ssl listener on the https_port...
            if (ssl_listener == true)
            {
                uint https_port = networkConfig.GetValue<uint>("https_port", 0);

                m_logger.LogWarning($"External flag is {ssl_external}");
                if (!ssl_external)
                {
                    string cert_path = networkConfig.GetValue("cert_path", string.Empty);
                    if (string.IsNullOrEmpty(cert_path))
                    {
                        System.Console.WriteLine("Path to X509 certificate is missing, server can't start.");
                        //Thread.CurrentThread.Abort();
                    }
                    string cert_pass = networkConfig.GetValue("cert_pass", string.Empty);
                    if (string.IsNullOrEmpty(cert_pass))
                    {
                        System.Console.WriteLine("Password for X509 certificate is missing, server can't start.");
                        //Thread.CurrentThread.Abort();
                    }

                    MainServer.GetHttpServer(null, https_port, ipaddr: null, ssl_listener, cert_path, cert_pass); //XXX MCD
                }
                else
                {
                    m_logger.LogWarning($"SSL port is active but no SSL is used because external SSL was requested.");
                    MainServer.GetHttpServer(null, https_port); // XXX MCD
                }
            }
        }

        protected override void Initialise()
        {
            foreach (BaseHttpServer s in MainServer.Servers.Values)
                s.Start();

            MainServer.RegisterHttpConsoleCommands(MainConsole.Instance);

            MethodInfo mi = m_console.GetType().GetMethod("SetServer", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(BaseHttpServer) }, null);

            if (mi != null)
            {
                if (m_consolePort == 0)
                    mi.Invoke(MainConsole.Instance, new object[] { MainServer.Instance });
                else
                    mi.Invoke(MainConsole.Instance, new object[] { MainServer.GetHttpServer(null, m_consolePort) }); // XXX MCD
            }
        }
    }
}
