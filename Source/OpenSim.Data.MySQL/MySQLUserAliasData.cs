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

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the User Server - User Aliases
    /// </summary>
    public class MySQLUserAliasData : IUserAliasData
    {
        protected MySQLGenericTableHandler<UserAliasData> tableHandler = null;

        public void Initialize(string connectionString, string realm)
        {
            tableHandler = new();
            tableHandler.Initialize(connectionString, realm, "UserAlias");
        }

        public UserAliasData Get(int Id)
        {
            UserAliasData[] ret = tableHandler.Get("Id", Id.ToString());

            if (ret.Length == 0)
                return null;

            return ret[0];
        }

        public UserAliasData GetUserForAlias(UUID aliasID)
        {
            UserAliasData[] ret = tableHandler.Get("AliasID", aliasID.ToString());

            if (ret.Length == 0)
                return null;

            return ret[0];
        }

        public List<UserAliasData> GetUserAliases(UUID userID)
        {
            var aliases = tableHandler.Get("UserID", userID.ToString());

            if (aliases.Length == 0)
                return null;

            return new List<UserAliasData>(aliases);
        }

        public bool Store(UserAliasData data)
        {
            return tableHandler.Store(data);
        }

        public bool Delete(string field, string val)
        {
            return tableHandler.Delete(field, val);
        }
    }
}