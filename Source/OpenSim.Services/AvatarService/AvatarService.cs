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

using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace OpenSim.Services.AvatarService
{
    public class AvatarService : IAvatarService
    {
        protected readonly IConfiguration m_configuration;
        protected readonly ILogger<AvatarService> m_logger;
        protected readonly IAvatarData m_Database;

        protected static string _ConfigName = "AvatarService";

        public AvatarService(
            IConfiguration config,
            ILogger<AvatarService> logger,
            IAvatarData avatarData            
        )
        {
            m_configuration = config;            
            m_logger = logger;
            m_Database = avatarData;

            string? connString = String.Empty;
            string? realm = "Avatars";

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
                realm = dbConfig.GetValue("Realm", realm);
            }
            
            //
            // [AvatarService] section overrides [DatabaseService], if it exists
            //
            var presenceConfig = config.GetSection(_ConfigName);
            if (presenceConfig.Exists())
            {
                connString = presenceConfig.GetValue("ConnectionString", connString);
                realm = presenceConfig.GetValue("Realm", realm);
            }

            m_Database.Initialize(connString, realm);
 
            m_logger.LogDebug("[AVATAR SERVICE]: Starting avatar service");
        }

        public AvatarAppearance GetAppearance(UUID principalID)
        {
            AvatarData avatar = GetAvatar(principalID);
            return avatar.ToAvatarAppearance();
        }

        public bool SetAppearance(UUID principalID, AvatarAppearance appearance)
        {
            AvatarData avatar = new AvatarData(appearance);
            return SetAvatar(principalID,avatar);
        }

        public AvatarData GetAvatar(UUID principalID)
        {
            AvatarBaseData[] av = m_Database.Get("PrincipalID", principalID.ToString());
            AvatarData ret = new AvatarData();
            ret.Data = new Dictionary<string,string>();

            if (av.Length == 0)
            {
                ret.AvatarType = 1; // SL avatar
                return ret;
            }

            foreach (AvatarBaseData b in av)
            {
                if (b.Data["Name"] == "AvatarType")
                    ret.AvatarType = Convert.ToInt32(b.Data["Value"]);
                else
                    ret.Data[b.Data["Name"]] = b.Data["Value"];
            }

            return ret;
        }

        public bool SetAvatar(UUID principalID, AvatarData avatar)
        {
            int count = 0;
            foreach (KeyValuePair<string, string> kvp in avatar.Data)
                if (kvp.Key.StartsWith("_"))
                    count++;

//            m_log.DebugFormat("[AVATAR SERVICE]: SetAvatar for {0}, attachs={1}", principalID, count);
            m_Database.Delete("PrincipalID", principalID.ToString());

            AvatarBaseData av = new AvatarBaseData();
            av.Data = new Dictionary<string,string>();

            av.PrincipalID = principalID;
            av.Data["Name"] = "AvatarType";
            av.Data["Value"] = avatar.AvatarType.ToString();

            if (!m_Database.Store(av))
                return false;

            foreach (KeyValuePair<string,string> kvp in avatar.Data)
            {
                av.Data["Name"] = kvp.Key;
                av.Data["Value"] = kvp.Value;

                if (!m_Database.Store(av))
                {
                    m_Database.Delete("PrincipalID", principalID.ToString());
                    return false;
                }
            }

            return true;
        }

        public bool ResetAvatar(UUID principalID)
        {
            return m_Database.Delete("PrincipalID", principalID.ToString());
        }

        public bool SetItems(UUID principalID, string[] names, string[] values)
        {
            AvatarBaseData av = new AvatarBaseData();
            av.Data = new Dictionary<string,string>();
            av.PrincipalID = principalID;

            if (names.Length != values.Length)
                return false;

            for (int i = 0 ; i < names.Length ; i++)
            {
                av.Data["Name"] = names[i];
                av.Data["Value"] = values[i];

                if (!m_Database.Store(av))
                    return false;
            }

            return true;
        }

        public bool RemoveItems(UUID principalID, string[] names)
        {
            foreach (string name in names)
            {
                m_Database.Delete(principalID, name);
            }
            return true;
        }
    }
}
