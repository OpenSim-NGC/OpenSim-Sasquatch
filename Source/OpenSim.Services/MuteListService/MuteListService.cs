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

using System.Text;
using OpenMetaverse;

using OpenSim.Services.Interfaces;
using OpenSim.Data;
using OpenSim.Framework;

using Microsoft.Extensions.Configuration;

namespace OpenSim.Services.EstateService
{
    public class MuteListService : IMuteListService
    {
        protected IMuteListData m_database;

        private const string _ConfigName = "MuteListService";

        public MuteListService(
            IConfiguration config,
            IMuteListData muteListData
            )
        {
            m_database = muteListData;

            string? connString = String.Empty;

            // Try reading the [DatabaseService] section, if it exists
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
                connString = dbConfig.GetValue("MuteConnectionString", connString);
            }

            // Try reading the [MuteListStore] section, if it exists
            var muteConfig = config.GetSection("MuteListStore");
            if (muteConfig.Exists())
            {
                connString = muteConfig.GetValue("ConnectionString", connString);
            }

            m_database.Initialize(connString);
        }

        public Byte[]? MuteListRequest(UUID agentID, uint crc)
        {
            if(m_database == null)
                return null;

            MuteData[] data = m_database.Get(agentID);
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            StringBuilder sb = new StringBuilder(16384);
            foreach (MuteData d in data)
                sb.AppendFormat("{0} {1} {2}|{3}\n",
                        d.MuteType,
                        d.MuteID.ToString(),
                        d.MuteName,
                        d.MuteFlags);

            Byte[] filedata = Util.UTF8.GetBytes(sb.ToString());

            uint dataCrc = Crc32.Compute(filedata);

            if (dataCrc == crc)
            {
                if(crc == 0)
                     return Array.Empty<byte>();

                Byte[] ret = new Byte[1] {1};
                return ret;
            }

            return filedata;
        }

        public bool UpdateMute(MuteData mute)
        {
            if(m_database == null)
                return false;
            return m_database.Store(mute);
        }

        public bool RemoveMute(UUID agentID, UUID muteID, string muteName)
        {
            if(m_database == null)
                return false;
            return m_database.Delete(agentID, muteID, muteName);
        }
    }
}