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

using OpenSim.Data;
using OpenSim.Services.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.AuthorizationService
{
    public class AuthorizationService : IAuthorizationService
    {
        protected readonly IConfiguration m_configuration;
        protected readonly ILogger<AuthorizationService> m_logger;
        protected readonly IAssetDataPlugin m_Database;
        
        protected static string _ConfigName = "AuthorizationService";
        public AuthorizationService(
            IConfiguration config,
            ILogger<AuthorizationService> logger,
            IAssetDataPlugin assetDataPlugin
            )
        {
            m_configuration = config;
            m_logger = logger;
            m_Database = assetDataPlugin;

            string? connString = String.Empty;

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            var dbConfig = m_configuration.GetSection("DatabaseService");
            if (dbConfig.Exists())
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
            }

            //
            // Try reading the [AuthorizationService] section.  If it exists
            // it will overide the global database definition
            //
            var assetConfig = m_configuration.GetSection(_ConfigName);
            if (assetConfig.Exists())
            {
                connString = assetConfig.GetValue("ConnectionString", connString);
            }

            m_Database.Initialise(connString);

            m_logger.LogInformation("[AUTHORIZATION CONNECTOR]: Local Authorization service enabled");
        }

        public bool IsAuthorizedForRegion(
            string userID, string firstName, string lastName, string regionID, out string message)
        {
            message = "Authorized";
            return true;
        }
    }
}