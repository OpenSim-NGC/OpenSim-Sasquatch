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
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.UserAccountService
{
    public class UserAliasService : IUserAliasService
    {
        private readonly IConfiguration m_configuration;
        private readonly ILogger<UserAliasService> m_logger;
        private readonly IUserAliasData m_Database = null;

        public UserAliasService(
            IConfiguration config,
            ILogger<UserAliasService> logger,
            IUserAliasData userAliasData
            )
        {
            m_configuration = config;
            m_logger = logger;
            m_Database = userAliasData;

            string connString = String.Empty;
            string realm = "UserAlias";

            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
                connString = dbConfig.GetValue("ConnectionString", String.Empty);

            var userConfig = config.GetSection("UserAliasService");
            if (userConfig.Exists() is false)
            {
                throw new Exception("No UserAliasService configuration");
            }

            connString = userConfig.GetValue("ConnectionString", connString);
            realm = userConfig.GetValue("Realm", realm);

            m_Database.Initialize(connectionString: connString, realm: realm);

            // Console commands

            // In case there are several instances of this class in the same process,
            // the console commands are only registered for the root instance
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand("Aliases", false,
                    "create alias",
                    "create alias [<userId> [<aliasid> [<description>]]]",
                    "Create a new user alias", HandleCreateAlias);

                MainConsole.Instance.Commands.AddCommand("Aliases", false,
                    "show alias",
                    "show alias <userId>",
                    "Show Aliases user ids defined for the specified user account", HandleShowAliases);

                MainConsole.Instance.Commands.AddCommand("Aliases", false,
                        "delete alias",
                        "delete alias [<aliasid>]",
                        "delete an existing user alias by aliasId", HandleDeleteAlias);
            }
        }

        #region Console commands

        /// <summary>
        /// Handle the create user command from the console.
        /// </summary>
        /// <param name="cmdparams">string array with parameters: userid, aliasid, description </param>
        protected void HandleCreateAlias(string module, string[] cmdparams)
        {
            string rawUserId = (cmdparams.Length < 3 ? MainConsole.Instance.Prompt("UserID", "") : cmdparams[2]);
            string rawAliasId = (cmdparams.Length < 4 ? MainConsole.Instance.Prompt("AliasID", "") : cmdparams[3]);
            string description = (cmdparams.Length < 5 ? MainConsole.Instance.Prompt("Description", "") : cmdparams[4]);

            if (UUID.TryParse(rawUserId, out UUID UserID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawUserId));

            if (UUID.TryParse(rawAliasId, out UUID AliasID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawAliasId));

            var alias = CreateAlias(AliasID, UserID, description);
            if (alias != null)
            { 
                MainConsole.Instance.Output(
                    "Alias Created - UserID: {0}, AliasID: {1}, Description: {2}",
                    alias.UserID, alias.AliasID, alias.Description);
            }
        }

        protected void HandleShowAliases(string module, string[] cmdparams)
        {
            string rawUserId = (cmdparams.Length < 3 ? MainConsole.Instance.Prompt("UserID", "") : cmdparams[2]);

            if (UUID.TryParse(rawUserId, out UUID UserID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawUserId));

            var aliases = GetUserAliases(UserID);

            if (aliases == null)
            {
                MainConsole.Instance.Output("No aliases for user {0}", rawUserId);
                return;
            }

            foreach (var alias in aliases)
            {
                MainConsole.Instance.Output(
                    "UserID: {0}, AliasID: {1}, Description: {2}", 
                    alias.UserID, alias.AliasID, alias.Description);
            }
        }

        private void HandleDeleteAlias(string module, string[] cmdparams)
        {
            string rawAliasId = (cmdparams.Length < 3 ? MainConsole.Instance.Prompt("AliasID", "") : cmdparams[2]);

            if (UUID.TryParse(rawAliasId, out UUID AliasID) == false)
                throw new Exception(string.Format("ID {0} is not a valid UUID", rawAliasId));

            if (DeleteAlias(AliasID) == true)
            {
                MainConsole.Instance.Output("Alias for ID {0} deleted", rawAliasId);
            }
            else
            {
                MainConsole.Instance.Output("No alias with ID {0} found", rawAliasId);
            }
        }

        #endregion

        /// <summary>
        /// Given a userID, return a list of any aliases (UUIDs) defined for this user
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>List<UserAlias>() - A list of aliases or null if none are defined</UUID></returns>
        public List<UserAlias> GetUserAliases(UUID userID)
        {
            m_logger.LogDebug($"[USER ALIAS SERVICE] Retrieving aliases for user by userid {userID}");

            var aliases = m_Database.GetUserAliases(userID);

            if ((aliases == null) || (aliases.Count == 0))
                return null;
            
            var userAliases = new List<UserAlias>();
            foreach (var alias in aliases)
            {
                var userAlias = new UserAlias
                {
                    AliasID = alias.AliasID,
                    UserID = alias.UserID,
                    Description = alias.Description
                };

                userAliases.Add(userAlias);
            }

            return userAliases;
        }

        public UserAlias GetUserForAlias(UUID aliasID)
        {
            m_logger.LogDebug($"[USER ALIAS SERVICE]: Retrieving userID for alias by aliasId {aliasID}");

            var alias = m_Database.GetUserForAlias(aliasID);

            if (alias == null)
            {
                return null;
            }
            else
            {
                var userAlias = new UserAlias
                {
                    AliasID = alias.AliasID,
                    UserID = alias.UserID,
                    Description = alias.Description
                };

                return userAlias;
            }
        }

        public UserAlias CreateAlias(UUID AliasID, UUID UserID, string Description)
        {
            var aliasData = new UserAliasData
            {
                AliasID = AliasID,
                UserID = UserID,
                Description = Description
            };

            if (m_Database.Store(aliasData) == true)
            {
                return new UserAlias(AliasID, UserID, Description); 
            }

            return null;
        }

        public bool DeleteAlias(UUID aliasID)
        {
            return m_Database.Delete("AliasID", aliasID.ToString());
        }
    }
}
