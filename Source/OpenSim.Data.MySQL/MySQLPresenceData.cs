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

using System;
using MySqlConnector;
using OpenMetaverse;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Grid Server
    /// </summary>
    public class MySQLPresenceData : IPresenceData
    {
        protected MySQLGenericTableHandler<PresenceData> tableHandler = null;

        public void Initialize(string connectionString, string realm)
        {
            tableHandler = new();
            tableHandler.Initialize(connectionString, realm, "Presence");
        }

        public PresenceData Get(UUID sessionID)
        {
            PresenceData[] ret = tableHandler.Get("SessionID", sessionID.ToString());

            if (ret.Length == 0)
                return null;

            return ret[0];
        }

        public void LogoutRegionAgents(UUID regionID)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = String.Format("delete from {0} where `RegionID`=?RegionID", tableHandler.Realm);

                cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                tableHandler.ExecuteNonQuery(cmd);
            }
        }

        public bool ReportAgent(UUID sessionID, UUID regionID)
        {
            PresenceData[] pd = tableHandler.Get("SessionID", sessionID.ToString());
            if (pd.Length == 0)
                return false;

            if (regionID.IsZero())
                return false;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = String.Format("update {0} set RegionID=?RegionID, LastSeen=NOW() where `SessionID`=?SessionID", tableHandler.Realm);

                cmd.Parameters.AddWithValue("?SessionID", sessionID.ToString());
                cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                if (tableHandler.ExecuteNonQuery(cmd) == 0)
                    return false;
            }

            return true;
        }

        public bool VerifyAgent(UUID agentId, UUID secureSessionID)
        {
            PresenceData[] ret = tableHandler.Get("SecureSessionID", secureSessionID.ToString());

            if (ret.Length == 0)
                return false;

            if(ret[0].UserID != agentId.ToString())
                return false;

            return true;
        }

        public bool Store(PresenceData data)
        {
            return tableHandler.Store(data);
        }

        public PresenceData[] Get(string field, string data)
        {
            return tableHandler.Get(field, data);
        }

        public bool Delete(string field, string val)
        {
            return tableHandler.Delete(field, val);
        }
    }
}