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

using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Server.Handlers.Land
{
    public class LandServiceInConnector : IServiceConnector
    {
        private ILandService m_LandService;
        private IScene m_Scene;
        // TODO : private IAuthenticationService m_AuthenticationService;

        public LandServiceInConnector(
            IConfiguration configuration,
            ILogger<LandServiceInConnector> logger,
            ILandService service, 
            IScene scene)
        {
            Config = configuration;
            Logger = logger;
            m_LandService = service;
            m_Scene = scene;
        }

        public string ConfigName { get; private set; } = "LamdService";

        public IConfiguration Config { get; private set; }

        public ILogger Logger { get; private set; }

        public IHttpServer HttpServer { get; private set; }

        public void Initialize(IHttpServer httpServer)
        {
            HttpServer = httpServer;

            if (m_LandService == null)
            {
                Logger.LogError("Land service was not provided");
                return;
            }

            //bool authentication = neighbourConfig.GetBoolean("RequireAuthentication", false);
            //if (authentication)
            //    m_AuthenticationService = m_scene.RequestModuleInterface<IAuthenticationService>();

            LandHandlers landHandlers = new LandHandlers(Logger, m_LandService);
            HttpServer.AddXmlRPCHandler("land_data", landHandlers.GetLandData, false);
        }
    }
}