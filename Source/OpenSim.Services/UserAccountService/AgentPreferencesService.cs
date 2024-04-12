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
using OpenSim.Data;
using OpenSim.Services.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Autofac;
using OpenSim.Server.Base;

namespace OpenSim.Services.UserAccountService
{
    public class AgentPreferencesService : IAgentPreferencesService
    {
        private readonly IComponentContext m_context;
        private readonly IConfiguration m_configuration;
        private readonly ILogger<AgentPreferencesService> m_logger;
        private readonly IAgentPreferencesData m_Database;

        public AgentPreferencesService(
            IComponentContext componentContext,
            IConfiguration config,
            ILogger<AgentPreferencesService> logger,
            IAgentPreferencesData agentPreferencesData)
        {
            m_context = componentContext;
            m_configuration = config;
            m_logger = logger;
            m_Database = agentPreferencesData;

            string realm = "AgentPrefs";
            string connString = String.Empty;

            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
                connString = dbConfig.GetValue("ConnectionString", String.Empty);

            var userConfig = config.GetSection("AgentPreferencesService");
            if (userConfig.Exists() is false)
                throw new Exception("No AgentPreferencesService configuration");

            connString = userConfig.GetValue("ConnectionString", connString);
            realm = userConfig.GetValue("Realm", realm);
            
            m_Database.Initialize(connectionString: connString, realm: realm);

            m_logger.LogDebug("[AGENT PREFERENCES SERVICE]: Starting agent preferences service");
        }

        public AgentPrefs GetAgentPreferences(UUID principalID)
        {
            AgentPreferencesData d = m_Database.GetPrefs(principalID);
            AgentPrefs prefs = (d == null) ? null : new AgentPrefs(d.Data);
            return prefs;
        }

        public bool StoreAgentPreferences(AgentPrefs data)
        {
            AgentPreferencesData d = new AgentPreferencesData();

            d.Data = new Dictionary<string, string>();
            d.Data["PrincipalID"] = data.PrincipalID.ToString();
            d.Data["AccessPrefs"] = data.AccessPrefs;
            d.Data["HoverHeight"] = data.HoverHeight.ToString();
            d.Data["Language"] = data.Language;
            d.Data["LanguageIsPublic"] = (data.LanguageIsPublic ? "1" : "0");
            d.Data["PermEveryone"] = data.PermEveryone.ToString();
            d.Data["PermGroup"] = data.PermGroup.ToString();
            d.Data["PermNextOwner"] = data.PermNextOwner.ToString();
            
            return m_Database.Store(d);
        }

        public string GetLang(UUID principalID)
        {
            AgentPrefs data = GetAgentPreferences(principalID);
            if (data != null)
            {
                if (data.LanguageIsPublic)
                    return data.Language;
            }

            return "en-us";
        }
    }
}
