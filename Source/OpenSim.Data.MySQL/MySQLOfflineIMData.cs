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

using MySqlConnector;

namespace OpenSim.Data.MySQL
{
    public class MySQLOfflineIMData : IOfflineIMData
    {
        protected MySQLGenericTableHandler<OfflineIMData> tableHandler = null;

        public void Initialize(string connectionString, string realm)
        {
            tableHandler = new();
            tableHandler.Initialize(connectionString, realm, "IM_Store");
        }

        public void DeleteOld()
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = String.Format("delete from {0} where TMStamp < NOW() - INTERVAL 2 WEEK", tableHandler.Realm);

                tableHandler.ExecuteNonQuery(cmd);
            }
        }

        public OfflineIMData[] Get(string field, string val)
        {
            return tableHandler.Get(field, val);
        }

        public long GetCount(string field, string key)
        {
            return tableHandler.GetCount(field,key);
        }

        public bool Store(OfflineIMData data)
        {
            return tableHandler.Store(data);
        }

        public bool Delete(string field, string val)
        {
            return tableHandler.Delete(field, val);
        }
    }
}