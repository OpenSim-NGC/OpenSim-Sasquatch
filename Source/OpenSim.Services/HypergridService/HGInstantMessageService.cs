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
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.InstantMessage;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenMetaverse.Packets;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// HG IM Service
    /// </summary>
    public class HGInstantMessageService : IInstantMessage
    {
        private bool m_Initialized = false;

        protected readonly ExpiringCacheOS<UUID, string> m_UserLocationMap = new ExpiringCacheOS<UUID, string>(10000);
        protected readonly ExpiringCacheOS<UUID, string> m_RegionsCache = new ExpiringCacheOS<UUID, string>(60000);

        private bool m_ForwardOfflineGroupMessages;
        private bool m_InGatekeeper;

        private string? m_messageKey;

        private const string _ConfigName = "HGInstantMessageService";

        private readonly IConfiguration m_configuration;
        private readonly ILogger<HGInstantMessageService> m_logger;

        protected readonly IGridService m_GridService;
        protected readonly IPresenceService m_PresenceService;
        protected readonly IUserAgentService m_UserAgentService;
        protected readonly IOfflineIMService m_OfflineIMService;
        protected readonly IInstantMessageSimConnector m_IMSimConnector;
        protected readonly InstantMessageServiceConnector m_instantMessageServiceConnector;

        public HGInstantMessageService(
            IConfiguration config, 
            ILogger<HGInstantMessageService> logger,
            IGridService gridService,
            IPresenceService presenceService,
            IUserAgentService userAgentService,
            IOfflineIMService offlineIMService,
            IInstantMessageSimConnector imConnector,
            InstantMessageServiceConnector instantMessageServiceConnector)
        {
            m_configuration = config;
            m_logger = logger;

            m_GridService = gridService;
            m_PresenceService = presenceService;
            m_UserAgentService = userAgentService;
            m_OfflineIMService = offlineIMService;
            m_IMSimConnector = imConnector;
            m_instantMessageServiceConnector = instantMessageServiceConnector;

            if (!m_Initialized)
            {
                m_Initialized = true;

                var serverConfig = config.GetSection(_ConfigName);
                if (serverConfig.Exists() is false)
                    throw new Exception($"No section {_ConfigName} in config file");

                // string? gridService = serverConfig.GetValue("GridService", string.Empty);
                // if (string.IsNullOrEmpty(gridService))
                //     throw new Exception("[HG IM SERVICE]: GridService not set in [HGInstantMessageService]");

                // string? presenceService = serverConfig.GetValue("PresenceService", string.Empty);
                // if (string.IsNullOrEmpty(presenceService))
                //     throw new Exception("[HG IM SERVICE]: PresenceService not set in [HGInstantMessageService]");

                // string? userAgentService = serverConfig.GetValue("UserAgentService", string.Empty);
                // if (string.IsNullOrEmpty(userAgentService))
                //     m_logger.LogWarning($"[HG IM SERVICE]: UserAgentService not set in [HGInstantMessageService]");

                // object[] args = new object[] { config };
                // try
                // {
                //     m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                // }
                // catch
                // {
                //     throw new Exception("[HG IM SERVICE]: Unable to load GridService");
                // }

                // try
                // {
                //     m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
                // }
                // catch
                // {
                //     throw new Exception("[HG IM SERVICE]: Unable to load PresenceService");
                // }

                // try
                // {
                //     m_UserAgentService = ServerUtils.LoadPlugin<IUserAgentService>(userAgentService, args);
                // }
                // catch
                // {
                //     m_logger.LogWarning("[HG IM SERVICE]: Unable to load PresenceService");
                // }

                m_InGatekeeper = serverConfig.GetValue<bool>("InGatekeeper", false);

                var cnf = config.GetSection("Messaging");
                if (cnf.Exists() is false)
                {
                    m_logger.LogDebug("[HG IM SERVICE]: Starting (without [MEssaging])");
                    return;
                }

                m_messageKey = cnf.GetValue("MessageKey", string.Empty);
                m_ForwardOfflineGroupMessages = cnf.GetValue<bool>("ForwardOfflineGroupMessages", false);

                if (m_InGatekeeper)
                {
                    m_logger.LogDebug("[HG IM SERVICE]: Starting In Robust GateKeeper");

                    // string offlineIMService = cnf.GetValue("OfflineIMService", string.Empty);
                    // if (offlineIMService != string.Empty)
                    //     m_OfflineIMService = ServerUtils.LoadPlugin<IOfflineIMService>(offlineIMService, args);
                }
                else
                {
                    m_logger.LogDebug("[HG IM SERVICE]: Starting");
                }
            }
        }

        public bool IncomingInstantMessage(GridInstantMessage im)
        {
            m_logger.LogDebug($"[HG IM SERVICE]: Received message from {im.fromAgentID} to {im.toAgentID}");
            //UUID toAgentID = new UUID(im.toAgentID);

            bool success = false;
            if (m_IMSimConnector != null)
            {
                success = m_IMSimConnector.SendInstantMessage(im);
            }
            else
            {
                success = TrySendInstantMessage(im, "", true, false);
            }

            if (!success && m_InGatekeeper) // we do this only in the Gatekeeper IM service
                UndeliveredMessage(im);

            return success;
        }

        public bool OutgoingInstantMessage(GridInstantMessage im, string url, bool foreigner)
        {
            m_logger.LogDebug($"[HG IM SERVICE]: Sending message from {im.fromAgentID} to {im.toAgentID}@{url}");
            return TrySendInstantMessage(im, url, true, foreigner);
        }

        protected bool TrySendInstantMessage(GridInstantMessage im, string foreignerkurl, bool firstTime, bool foreigner)
        {
            UUID toAgentID = new UUID(im.toAgentID);
            string url = null;

            // first try cache
            if (m_UserLocationMap.TryGetValue(toAgentID, out url))
            {
                if (ForwardIMToGrid(url, im, toAgentID))
                    return true;
            }

            // try the provided url (for a foreigner)
            if(!string.IsNullOrEmpty(foreignerkurl))
            {
                if (ForwardIMToGrid(foreignerkurl, im, toAgentID))
                    return true;
            }

            //try to find it in local grid
            PresenceInfo[] presences = m_PresenceService.GetAgents(new string[] { toAgentID.ToString() });
            if (presences != null && presences.Length > 0)
            {
                foreach (PresenceInfo p in presences)
                {
                    if (!p.RegionID.IsZero())
                    {
                        //m_logger.DebugFormat("[XXX]: Found presence in {0}", p.RegionID);
                        // stupid service does not cache region, even in region code
                        if(m_RegionsCache.TryGetValue(p.RegionID, out url))
                            break;

                        GridRegion reginfo = m_GridService.GetRegionByUUID(UUID.Zero, p.RegionID);
                        if (reginfo != null)
                        {
                            url = reginfo.ServerURI;
                            m_RegionsCache.AddOrUpdate(p.RegionID, url, 300);
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(url) && !foreigner && m_UserAgentService != null)
            {
                // Let's check with the UAS if the user is elsewhere in HG
                m_logger.LogDebug($"[HG IM SERVICE]: User is not present. Checking location with User Agent service");

                try
                {
                    url = m_UserAgentService.LocateUser(toAgentID);
                }
                catch (Exception e)
                {
                    m_logger.LogWarning(e, $"[HG IM SERVICE]: LocateUser call failed ");
                    url = string.Empty;
                }
            }

            if (string.IsNullOrEmpty(url))
            {
                m_logger.LogDebug($"[HG IM SERVICE]: Unable to locate user {toAgentID}");
                return false;
            }

            // check if we've tried this before..
            if (!string.IsNullOrEmpty(foreignerkurl) && url.Equals(foreignerkurl, StringComparison.InvariantCultureIgnoreCase))
            {
                m_logger.LogDebug($"[HG IM SERVICE]: Unable to send to user {toAgentID}, at {foreignerkurl}");
                return false;
            }

            // ok, the user is around somewhere. Let's send back the reply with "success"
            // even though the IM may still fail. Just don't keep the caller waiting for
            // the entire time we're trying to deliver the IM
            return ForwardIMToGrid(url, im, toAgentID);
        }

        bool ForwardIMToGrid(string url, GridInstantMessage im, UUID toAgentID)
        {
            if (m_instantMessageServiceConnector.SendInstantMessage(url, im, m_messageKey))
            {
                // IM delivery successful, so store the Agent's location in our local cache.
                m_UserLocationMap.AddOrUpdate(toAgentID, url, 120);
                return true;
            }
            else
                m_UserLocationMap.Remove(toAgentID);

            return false;
        }

        private bool UndeliveredMessage(GridInstantMessage im)
        {
            if (m_OfflineIMService == null)
                return false;

            if (m_ForwardOfflineGroupMessages)
            {
                switch (im.dialog)
                {
                    case (byte)InstantMessageDialog.MessageFromObject:
                    case (byte)InstantMessageDialog.MessageFromAgent:
                    case (byte)InstantMessageDialog.GroupNotice:
                    case (byte)InstantMessageDialog.GroupInvitation:
                    case (byte)InstantMessageDialog.InventoryOffered:
                        break;
                    default:
                        return false;
                }
            }
            else
            {
                switch (im.dialog)
                {
                    case (byte)InstantMessageDialog.MessageFromObject:
                    case (byte)InstantMessageDialog.MessageFromAgent:
                    case (byte)InstantMessageDialog.InventoryOffered:
                        break;
                    default:
                        return false;
                }
            }

            //m_logger.DebugFormat("[HG IM SERVICE]: Message saved");
            return m_OfflineIMService.StoreMessage(im, out string reason);
        }
    }
}
