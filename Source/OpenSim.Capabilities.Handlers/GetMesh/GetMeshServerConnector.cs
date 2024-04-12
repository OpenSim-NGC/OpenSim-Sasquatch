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

using OpenMetaverse;

using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;

using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Capabilities.Handlers
{
    public class GetMeshServerConnector : IServiceConnector
    {
        private IAssetService m_AssetService;
        private const string _ConfigName = "CapsService";

        protected readonly IConfiguration m_configuration;
        protected readonly ILogger<GetMeshServerConnector> m_logger;
        protected readonly IComponentContext m_context;

        public GetMeshServerConnector(
            IConfiguration config,
            ILogger<GetMeshServerConnector> logger,
            IComponentContext componentContext
            )
        {
            m_configuration = config;
            m_logger = logger;
            m_context = componentContext;
        }

        public string ConfigName { get; } = _ConfigName;

        public IHttpServer HttpServer { get; private set; }

        public void Initialize(IHttpServer httpServer)
        {
            var serverConfig = m_configuration.GetSection(ConfigName);
            if (serverConfig.Exists() is false)
                throw new Exception($"No section '{ConfigName}' in config file");

            string assetService = serverConfig.GetValue("AssetService", string.Empty);
            if (string.IsNullOrEmpty(assetService))
                throw new Exception("No AssetService in config file");

            m_AssetService = m_context.ResolveNamed<IAssetService>(assetService);
            if (m_AssetService == null)
                throw new Exception($"Failed to load AssetService from {assetService}; config is {ConfigName}");

            string rurl = serverConfig.GetValue("GetMeshRedirectURL", string.Empty);

            GetMeshHandler gmeshHandler = new GetMeshHandler(m_AssetService);
            IRequestHandler reqHandler
                = new RestHTTPHandler(
                    "GET",
                    "/" + UUID.Random(),
                    httpMethod => gmeshHandler.ProcessGetMesh(httpMethod, UUID.Zero, null),
                    "GetMesh",
                    null);

            HttpServer.AddStreamHandler(reqHandler);
        }
    }
}