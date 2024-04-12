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

using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Autofac;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.GridServer
{
    public partial class GridServer
    {
        private bool m_NoVerifyCertChain = false;
        private bool m_NoVerifyCertHostname = false;
        private OpenSimServer m_baseServer;

        protected List<IServiceConnector> m_ServiceConnectors = new();

        protected Dictionary<string,ServiceEntry> serviceList = new();

        private readonly IComponentContext m_context;
        private readonly ILogger<GridServer> m_logger;
        private readonly IConfiguration m_configuration;

        public GridServer(
            IComponentContext componentContext,
            IConfiguration configuration, 
            ILogger<GridServer> logger,
            OpenSimServer openSimServer
            )
        {
            m_context = componentContext;
            m_configuration = configuration;
            m_logger = logger;
            m_baseServer = openSimServer;
        }
        

        private bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
            )
        {
            if (m_NoVerifyCertChain)
                sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateChainErrors;

            if (m_NoVerifyCertHostname)
                sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;

            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            return false;
        }

        /// <summary>
        /// Opens a file and uses it as input to the console command parser.
        /// </summary>
        /// <param name="fileName">name of file to use as input to the console</param>
        private void PrintFileToConsole(string fileName)
        {
            if (File.Exists(fileName))
            {
                using (StreamReader readFile = File.OpenText(fileName))
                {
                    string? currentLine;
                    while ((currentLine = readFile.ReadLine()) is not null)
                    {
                        m_logger.LogInformation("[!]" + currentLine);
                    }
                }
            }
        }

        public int Startup()
        {
            Culture.SetCurrentCulture();
            Culture.SetDefaultCurrentCulture();

            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 30000;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;

            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;

            WebUtil.SetupHTTPClients(m_NoVerifyCertChain, m_NoVerifyCertHostname, null, 32);

            m_baseServer.Startup();

            if (LoadServices() <= 0)
            {
                throw new Exception("GridServer.Startup - No Services Defined");
            }

            foreach (var kvp in serviceList)
            {
                try
                {
                    var service = m_context.ResolveNamed<IServiceConnector>(kvp.Value.ModuleName);
                    var server = MainServer.Instance;
                    // if (kvp.Value.Port != 0)
                    //     server = MainServer.GetHttpServer()

                    service.Initialize(server);
                    m_ServiceConnectors.Add(service);
                }
                catch (Exception e)
                {
                    m_logger.LogError(e, $"Configuration for {kvp.Key} not found.");
                }                 
            }

            //     if (friendlyName == "LLLoginServiceInConnector")
            //         server.AddSimpleStreamHandler(new IndexPHPHandler(server));

            //     m_logger.LogInformation("[SERVER]: Loading {0} on port {1}", friendlyName, server.Port);

            //     IServiceConnector connector = null;

            //     object[] modargs = new object[] { m_configuration, server, configName };
            //     connector = ServerUtils.LoadPlugin<IServiceConnector>(conn, modargs);

            //     if (connector == null)
            //     {
            //         modargs = new object[] { m_configuration, server };
            //         connector = ServerUtils.LoadPlugin<IServiceConnector>(conn, modargs);
            //     }

            //     if (connector != null)
            //     {
            //         m_ServiceConnectors.Add(connector);
            //         m_logger.LogInformation("[SERVER]: {0} loaded successfully", friendlyName);
            //     }
            //     else
            //     {
            //         m_logger.LogError($"[SERVER]: Failed to load {conn}");
            //     }                    

            m_logger.LogInformation("Grid Services Connectors Initialized");

            return 0;
        }

        private int LoadServices()
        {
            var servicesConfig = m_configuration.GetSection("ServiceList");
            if (servicesConfig.Exists() is false)
            {
                throw new Exception("LoadConfiguration: No ServiceList found");
            }
            
            var services = servicesConfig.AsEnumerable();
            foreach (var kvp in services)
            {
                if ((kvp.Key == "ServiceList") && (kvp.Value == null))
                    continue;
                    
                string serviceName = kvp.Key.Split(new char[] { ':' })[1];
                ServiceEntry entry = new ServiceEntry(kvp.Value);
                serviceList.Add(serviceName, entry);
            }

            return serviceList.Count;
        }

        public void Work()
        {
            MainConsole.Instance.DefaultPrompt = "Grid$ ";

            while (true)
            {
                MainConsole.Instance.Prompt();
            }
        }

        public void Shutdown(int res)
        {
            //m_Server?.Shutdown();
            Util.StopThreadPool();
            Environment.Exit(res);
        }
    }
}