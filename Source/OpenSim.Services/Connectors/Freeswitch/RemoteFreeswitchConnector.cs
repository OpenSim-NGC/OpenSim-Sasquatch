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

using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenSim.Framework;

using OpenSim.Services.Interfaces;

namespace OpenSim.Services.Connectors
{
    public class RemoteFreeswitchConnector : IFreeswitchService
    {
        private string m_ServerURI = String.Empty;

        private readonly IConfiguration m_configuration;
        private readonly ILogger<RemoteFreeswitchConnector> m_logger;

        public RemoteFreeswitchConnector(
            IConfiguration configuration, 
            ILogger<RemoteFreeswitchConnector> logger,
            string serverURI)
            : this(configuration, logger)
        {
            m_ServerURI = serverURI.TrimEnd('/') + "/region-config";
        }

        public RemoteFreeswitchConnector(
            IConfiguration configuration, 
            ILogger<RemoteFreeswitchConnector> logger)
        {
            m_configuration = configuration;
            m_logger = logger;
            
            Initialise();
        }

        public virtual void Initialise()
        {
            var freeswitchConfig = m_configuration.GetSection("FreeSwitchVoice");
            if (freeswitchConfig.Exists() is false)
            {
                m_logger.LogError("FreeSwitchVoice missing from OpenSim.ini");
                throw new Exception("Freeswitch connector init error");
            }

            string serviceURI = freeswitchConfig.GetValue("FreeswitchServiceURL", String.Empty);
            if (string.IsNullOrEmpty(serviceURI))
            {
                m_logger.LogError("No FreeswitchServiceURL named in section FreeSwitchVoice");
                throw new Exception("Freeswitch connector init error");
            }

            m_ServerURI = serviceURI.TrimEnd('/') + "/region-config";
        }

        public Hashtable HandleDirectoryRequest(Hashtable requestBody)
        {
            // not used here
            return new Hashtable();
        }

        public Hashtable HandleDialplanRequest(Hashtable requestBody)
        {
            // not used here
            return new Hashtable();
        }

        public string GetJsonConfig()
        {
            m_logger.LogDebug($"Requesting config from {m_ServerURI}");
            return SynchronousRestFormsRequester.MakeRequest("GET", m_ServerURI, String.Empty);
        }
    }
}
