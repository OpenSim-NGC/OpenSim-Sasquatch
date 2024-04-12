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

using OpenSim.Services.Interfaces;
using OpenSim.Data;
using OpenSim.Framework;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.AuthenticationService
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need
    // verifiable identification.
    //
    public class AuthenticationServiceBase
    {
        protected readonly IConfiguration m_configuration;
        protected readonly ILogger m_logger;
        protected readonly IAuthenticationData m_Database;
        protected readonly IUserAccountService m_UserAccountService;

        public AuthenticationServiceBase(
            IConfiguration config, 
            ILogger logger,
            IAuthenticationData authenticationData,
            IUserAccountService acct)
        {
            m_configuration = config;
            m_logger = logger;
            m_Database = authenticationData;
            m_UserAccountService = acct;

            string? connString = String.Empty;
            string? realm = "auth";

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
                realm = dbConfig.GetValue("Realm", string.Empty);
            }
            
            //
            // Try reading the [AuthenticationService] section first, if it exists
            // Overides the default DB configuration above if it exists
            //
            var authConfig = config.GetSection("AuthenticationService");
            if (authConfig.Exists())
            {
                connString = authConfig.GetValue("ConnectionString", connString);
                realm = authConfig.GetValue("Realm", realm);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (string.IsNullOrEmpty(connString) || string.IsNullOrEmpty(realm))
                throw new Exception("No StorageProvider configured");

            m_Database.Initialize(connString, realm);
        }

        public bool Verify(UUID principalID, string token, int lifetime)
        {
            return m_Database.CheckToken(principalID, token, lifetime);
        }

        public virtual bool Release(UUID principalID, string token)
        {
            return m_Database.CheckToken(principalID, token, 0);
        }

        public virtual bool SetPassword(UUID principalID, string password)
        {
            string passwordSalt = Util.Md5Hash(UUID.Random().ToString());
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + passwordSalt);

            AuthenticationData auth = m_Database.Get(principalID);
            if (auth == null)
            {
                auth = new AuthenticationData();
                auth.PrincipalID = principalID;
                auth.Data = new System.Collections.Generic.Dictionary<string, object>();
                auth.Data["accountType"] = "UserAccount";
                auth.Data["webLoginKey"] = UUID.Zero.ToString();
            }
            auth.Data["passwordHash"] = md5PasswdHash;
            auth.Data["passwordSalt"] = passwordSalt;

            if (!m_Database.Store(auth))
            {
                m_logger.LogDebug($"[AUTHENTICATION DB]: Failed to store authentication data");
                return false;
            }

            m_logger.LogInformation($"[AUTHENTICATION DB]: Set password for principalID {principalID}");

            return true;
        }

        public virtual AuthInfo? GetAuthInfo(UUID principalID)
        {
            AuthenticationData? data = m_Database.Get(principalID);

            if (data == null)
            {
                return null;
            }
            else
            {
                AuthInfo info
                    = new AuthInfo()
                        {
                            PrincipalID = data.PrincipalID,
                            AccountType = data.Data["accountType"] as string,
                            PasswordHash = data.Data["passwordHash"] as string,
                            PasswordSalt = data.Data["passwordSalt"] as string,
                            WebLoginKey = data.Data["webLoginKey"] as string
                        };

                return info;
            }
        }

        public virtual bool SetAuthInfo(AuthInfo info)
        {
            AuthenticationData auth = new AuthenticationData();
            auth.PrincipalID = info.PrincipalID;
            auth.Data = new System.Collections.Generic.Dictionary<string, object>();
            auth.Data["accountType"] = info.AccountType;
            auth.Data["webLoginKey"] = info.WebLoginKey;
            auth.Data["passwordHash"] = info.PasswordHash;
            auth.Data["passwordSalt"] = info.PasswordSalt;

            if (!m_Database.Store(auth))
            {
                m_logger.LogError($"[AUTHENTICATION DB]: Failed to store authentication info.");
                return false;
            }

            m_logger.LogDebug($"[AUTHENTICATION DB]: Set authentication info for principalID {info.PrincipalID}");
            
            return true;
        }

        protected string GetToken(UUID principalID, int lifetime)
        {
            UUID token = UUID.Random();

            if (m_Database.SetToken(principalID, token.ToString(), lifetime))
                return token.ToString();

            return String.Empty;
        }

    }
}
