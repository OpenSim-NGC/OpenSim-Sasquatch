﻿/*
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
using OpenSim.Services.Interfaces;

using OpenSim.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.AuthenticationService
{
    public class WebkeyOrPasswordAuthenticationService : AuthenticationServiceBase, IAuthenticationService
    {
        private Dictionary<string, IAuthenticationService> m_svcChecks = new();

        public WebkeyOrPasswordAuthenticationService(
            IConfiguration config, 
            ILogger<AuthenticationServiceBase> logger,
            IAuthenticationData authenticationData,
            IUserAccountService acct)
            : base(config, logger, authenticationData, acct)
        {
            m_svcChecks["web_login_key"] = new WebkeyAuthenticationService(config, logger, authenticationData, acct);
            m_svcChecks["password"]      = new PasswordAuthenticationService(config, logger, authenticationData, acct);
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            UUID realID;

            return Authenticate(principalID, password, lifetime, out realID);
        }

        public string Authenticate(UUID principalID, string password, int lifetime, out UUID realID)
        {
            AuthenticationData data = m_Database.Get(principalID);
            string result = String.Empty;
            realID = UUID.Zero;
            if (data != null && data.Data != null)
            {
                if (data.Data.ContainsKey("webLoginKey"))
                {
                    m_logger.LogDebug($"[AUTH SERVICE]: Attempting web key authentication for PrincipalID {principalID}");
                    result = m_svcChecks["web_login_key"].Authenticate(principalID, password, lifetime, out realID);

                    if (result.Length == 0)
                    {
                        m_logger.LogDebug($"[AUTH SERVICE]: Web Login failed for PrincipalID {principalID}");
                    }
                }

                if (result.Length == 0 && data.Data.ContainsKey("passwordHash") && data.Data.ContainsKey("passwordSalt"))
                {
                    m_logger.LogDebug($"[AUTH SERVICE]: Attempting password authentication for PrincipalID {principalID}");
                    result = m_svcChecks["password"].Authenticate(principalID, password, lifetime, out realID);
                    if (result.Length == 0)
                    {
                        m_logger.LogDebug($"[AUTH SERVICE]: Password login failed for PrincipalID {principalID}");
                    }
                }

                if (result.Length == 0)
                {
                    m_logger.LogDebug($"[AUTH SERVICE]: Both password and webLoginKey-based authentication failed for PrincipalID {principalID}");
                }
            }
            else
            {
                m_logger.LogDebug($"[AUTH SERVICE]: PrincipalID {principalID} or its data not found");
            }

            return result;
        }
    }
}
