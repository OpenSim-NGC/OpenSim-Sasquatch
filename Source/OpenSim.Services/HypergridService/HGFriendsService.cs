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

using Autofac;

using OpenSim.Framework;
using OpenSim.Services.Connectors.Friends;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// W2W social networking
    /// </summary>
    public class HGFriendsService : IHGFriendsService
    {
        static bool m_Initialized = false;

        protected static IGridUserService? m_GridUserService;
        protected static IGridService? m_GridService;
        protected static IGatekeeperService? m_GatekeeperService;
        protected static IFriendsService? m_FriendsService;
        protected static IPresenceService? m_PresenceService;
        protected static IUserAccountService? m_UserAccountService;

        private readonly IFriendsSimConnector m_FriendsLocalSimConnector; // standalone, points to HGFriendsModule
        private readonly FriendsSimConnector m_FriendsSimConnector; // grid
        private readonly IConfiguration m_configuration;
        private readonly ILogger<HGFriendsService> m_logger;
        private readonly IComponentContext m_context;

        private const string m_ConfigName = "HGFriendsService";

        
        public HGFriendsService(
            IConfiguration config, 
            ILogger<HGFriendsService> logger,
            IComponentContext componentContext
            )
        {
            m_configuration = config;
            m_logger = logger;
            m_context = componentContext;
            
            m_FriendsSimConnector = new FriendsSimConnector();
                
            // m_FriendsLocalSimConnector = localSimConn;       
            // if (m_FriendsLocalSimConnector == null)
            //     m_FriendsLocalSimConnector = localSimConn;

            if (!m_Initialized)
            {
                m_Initialized = true;

                Object[] args = new Object[] { config };

                var serverConfig = config.GetSection(m_ConfigName);
                if (serverConfig.Exists() is false)
                    throw new Exception($"No section {m_ConfigName} in config file");

                string? theService = serverConfig.GetValue("FriendsService", string.Empty);
                if (string.IsNullOrEmpty(theService))
                    throw new Exception("No FriendsService in config file " + m_ConfigName);
                m_FriendsService = m_context.ResolveNamed<IFriendsService>(theService);

                theService = serverConfig.GetValue("UserAccountService", string.Empty);
                if (string.IsNullOrEmpty(theService))
                    throw new Exception("No UserAccountService in " + m_ConfigName);
                m_UserAccountService = m_context.ResolveNamed<IUserAccountService>(theService);

                theService = serverConfig.GetValue("GridService", string.Empty);
                if (string.IsNullOrEmpty(theService))
                    throw new Exception("No GridService in " + m_ConfigName);
                m_GridService = m_context.ResolveNamed<IGridService>(theService);

                theService = serverConfig.GetValue("PresenceService", string.Empty);
                if (string.IsNullOrEmpty(theService))
                    throw new Exception("No PresenceService in " + m_ConfigName);
                m_PresenceService = m_context.ResolveNamed<IPresenceService>(theService);

                m_FriendsSimConnector = new FriendsSimConnector();

                m_logger.LogDebug("[HGFRIENDS SERVICE]: Starting...");

            }
        }

        #region IHGFriendsService

        public int GetFriendPerms(UUID userID, UUID friendID)
        {
            FriendInfo[] friendsInfo = m_FriendsService.GetFriends(userID);
            foreach (FriendInfo finfo in friendsInfo)
            {
                if (finfo.Friend.StartsWith(friendID.ToString()))
                    return finfo.TheirFlags;
            }
            return -1;
        }

        public bool NewFriendship(FriendInfo friend, bool verified)
        {
            UUID friendID;
            string tmp = string.Empty, url = String.Empty, first = String.Empty, last = String.Empty;
            if (!Util.ParseUniversalUserIdentifier(friend.Friend, out friendID, out url, out first, out last, out tmp))
                return false;

            m_logger.LogDebug($"[HGFRIENDS SERVICE]: New friendship {friend.PrincipalID} {friend.Friend} ({verified})");

            // Does the friendship already exist?
            FriendInfo[] finfos = m_FriendsService.GetFriends(friend.PrincipalID);
            foreach (FriendInfo finfo in finfos)
            {
                if (finfo.Friend.StartsWith(friendID.ToString()))
                    return false;
            }

            // Verified user session. But the user needs to confirm friendship when he gets home
            if (verified)
            {
                return m_FriendsService.StoreFriend(friend.PrincipalID.ToString(), friend.Friend, 0);
            }

            // Does the reverted friendship exist? meaning that this user initiated the request
            finfos = m_FriendsService.GetFriends(friendID);
            bool userInitiatedOffer = false;

            foreach (FriendInfo finfo in finfos)
            {
                if (friend.Friend.StartsWith(finfo.PrincipalID.ToString()) && finfo.Friend.StartsWith(friend.PrincipalID.ToString()) && finfo.TheirFlags == -1)
                {
                    userInitiatedOffer = true;
                    // Let's delete the existing friendship relations that was stored
                    m_FriendsService.Delete(friendID, finfo.Friend);
                    break;
                }
            }

            if (userInitiatedOffer)
            {
                m_FriendsService.StoreFriend(friend.PrincipalID.ToString(), friend.Friend, 1);
                m_FriendsService.StoreFriend(friend.Friend, friend.PrincipalID.ToString(), 1);
                // notify the user
                ForwardToSim("ApproveFriendshipRequest", friendID, Util.UniversalName(first, last, url), "", friend.PrincipalID, "");
                return true;
            }
            return false;
        }

        public bool DeleteFriendship(FriendInfo friend, string secret)
        {
            FriendInfo[] finfos = m_FriendsService.GetFriends(friend.PrincipalID);
            foreach (FriendInfo finfo in finfos)
            {
                // We check the secret here. Or if the friendship request was initiated here, and was declined
                if (finfo.Friend.StartsWith(friend.Friend) && finfo.Friend.EndsWith(secret))
                {
                    m_logger.LogDebug($"[HGFRIENDS SERVICE]: Delete friendship {friend.PrincipalID} {friend.Friend}");

                    m_FriendsService.Delete(friend.PrincipalID, finfo.Friend);
                    m_FriendsService.Delete(finfo.Friend, friend.PrincipalID.ToString());

                    return true;
                }
            }

            return false;
        }

        public bool FriendshipOffered(UUID fromID, string fromName, UUID toID, string message)
        {
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, toID);
            if (account == null)
                return false;

            // OK, we have that user here.
            // So let's send back the call, but start a thread to continue
            // with the verification and the actual action.

            Util.FireAndForget(
                o => ProcessFriendshipOffered(fromID, fromName, toID, message), null, "HGFriendsService.ProcessFriendshipOffered");

            return true;
        }

        public bool ValidateFriendshipOffered(UUID fromID, UUID toID)
        {
            FriendInfo[] finfos = m_FriendsService.GetFriends(toID.ToString());
            foreach (FriendInfo fi in finfos)
            {
                if (fi.Friend.StartsWith(fromID.ToString()) && fi.TheirFlags == -1)
                    return true;
            }
            return false;
        }

        public List<UUID> StatusNotification(List<string> friends, UUID foreignUserID, bool online)
        {
            if (m_FriendsService == null || m_PresenceService == null)
            {
                m_logger.LogWarning($"[HGFRIENDS SERVICE]: Unable to perform status notifications because friends or presence services are missing");

                return new List<UUID>();
            }

            // Let's unblock the caller right now, and take it from here async

            List<UUID> localFriendsOnline = new List<UUID>();

            m_logger.LogDebug(
                $"[HGFRIENDS SERVICE]: Status notification: foreign user {foreignUserID} "+ 
                $"wants to notify {friends.Count} local friends of {(online ? "online" : "offline")} status");

            // First, let's double check that the reported friends are, indeed, friends of that user
            // And let's check that the secret matches
            List<string> usersToBeNotified = new List<string>();
            string foreignUserIDToString = foreignUserID.ToString();
            foreach (string uui in friends)
            {
                if (Util.ParseUniversalUserIdentifier(uui, out UUID localUserID, out _, out _, out _, out string secret))
                {
                    FriendInfo[] friendInfos = m_FriendsService.GetFriends(localUserID);
                    foreach (FriendInfo finfo in friendInfos)
                    {
                        if (finfo.Friend.StartsWith(foreignUserIDToString) && finfo.Friend.EndsWith(secret))
                        {
                            // great!
                            usersToBeNotified.Add(localUserID.ToString());
                        }
                    }
                }
            }

            // Now, let's send the notifications
            //m_log.DebugFormat("[HGFRIENDS SERVICE]: Status notification: user has {0} local friends", usersToBeNotified.Count);

            // First, let's send notifications to local users who are online in the home grid
            PresenceInfo[] friendSessions = m_PresenceService.GetAgents(usersToBeNotified.ToArray());
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo? friendSession = null;
                foreach (PresenceInfo pinfo in friendSessions)
                    if (!pinfo.RegionID.IsZero()) // let's guard against traveling agents
                    {
                        friendSession = pinfo;
                        break;
                    }

                if (friendSession != null)
                {
                    ForwardStatusNotificationToSim(friendSession.RegionID, foreignUserID, friendSession.UserID, online);
                    usersToBeNotified.Remove(friendSession.UserID.ToString());
                    UUID id;
                    if (UUID.TryParse(friendSession.UserID, out id))
                        localFriendsOnline.Add(id);

                }
            }

//            // Lastly, let's notify the rest who may be online somewhere else
//            foreach (string user in usersToBeNotified)
//            {
//                UUID id = new UUID(user);
//                //m_UserAgentService.LocateUser(id);
//                //etc...
//                //if (m_TravelingAgents.ContainsKey(id) && m_TravelingAgents[id].GridExternalName != m_GridName)
//                //{
//                //    string url = m_TravelingAgents[id].GridExternalName;
//                //    // forward
//                //}
//                //m_log.WarnFormat("[HGFRIENDS SERVICE]: User {0} is visiting another grid. HG Status notifications still not implemented.", user);
//            }

            // and finally, let's send the online friends
            if (online)
            {
                return localFriendsOnline;
            }
            else
                return new List<UUID>();
        }

        #endregion IHGFriendsService

        #region Aux

        private void ProcessFriendshipOffered(UUID fromID, String fromName, UUID toID, String message)
        {
            // Great, it's a genuine request. Let's proceed.
            // But now we need to confirm that the requester is who he says he is
            // before we act on the friendship request.

            if (!fromName.Contains("@"))
                return;

            string[] parts = fromName.Split(new char[] {'@'});
            if (parts.Length != 2)
                return;

            string uriStr = "http://" + parts[1];
            try
            {
                new Uri(uriStr);
            }
            catch (UriFormatException)
            {
                return;
            }

            throw new Exception("FixME.  This code sucks");

            // UserAgentServiceConnector uasConn = m_context.Resolve<UserAgentServiceConnecto>(uriStr);
            // Dictionary<string, object> servers = uasConn.GetServerURLs(fromID);
            // if (!servers.ContainsKey("FriendsServerURI"))
            //     return;

            // HGFriendsServicesConnector friendsConn = new HGFriendsServicesConnector(servers["FriendsServerURI"].ToString());
            // if (!friendsConn.ValidateFriendshipOffered(fromID, toID))
            // {
            //     m_logger.LogWarning($"[HGFRIENDS SERVICE]: Friendship request from {fromID} to {toID} is invalid. Impersonations?");
            //     return;
            // }

            // string fromUUI = Util.UniversalIdentifier(fromID, parts[0], "@" + parts[1], uriStr);

            // // OK, we're good!
            // ForwardToSim("FriendshipOffered", fromID, fromName, fromUUI, toID, message);
        }

        private bool ForwardToSim(string op, UUID fromID, string name, String fromUUI, UUID toID, string message)
        {
            PresenceInfo? session = null;
            GridRegion? region = null;

            PresenceInfo[] sessions = m_PresenceService.GetAgents(new string[] { toID.ToString() });
            if (sessions != null && sessions.Length > 0)
                session = sessions[0];
            if (session != null)
                region = m_GridService.GetRegionByUUID(UUID.Zero, session.RegionID);

            switch (op)
            {
                case "FriendshipOffered":
                    // Let's store backwards
                    string secret = UUID.Random().ToString().Substring(0, 8);
                    m_FriendsService.StoreFriend(toID.ToString(), fromUUI + ";" + secret, 0);
                    if (m_FriendsLocalSimConnector != null) // standalone
                    {
                        GridInstantMessage im = new GridInstantMessage(null, fromID, name, toID,
                            (byte)InstantMessageDialog.FriendshipOffered, message, false, Vector3.Zero);
                        // !! HACK
                        im.imSessionID = im.fromAgentID;
                        return m_FriendsLocalSimConnector.LocalFriendshipOffered(toID, im);
                    }
                    else if (region != null) // grid
                        return m_FriendsSimConnector.FriendshipOffered(region, fromID, toID, message, name);
                    break;
                case "ApproveFriendshipRequest":
                    if (m_FriendsLocalSimConnector != null) // standalone
                        return m_FriendsLocalSimConnector.LocalFriendshipApproved(fromID, name, toID);
                    else if (region != null) //grid
                        return m_FriendsSimConnector.FriendshipApproved(region, fromID, name, toID);
                    break;
            }

            return false;
        }

        protected void ForwardStatusNotificationToSim(UUID regionID, UUID foreignUserID, string user, bool online)
        {
            UUID userID;
            if (UUID.TryParse(user, out userID))
            {
                if (m_FriendsLocalSimConnector != null)
                {
                    m_logger.LogDebug($"[HGFRIENDS SERVICE]: Local Notify, user {foreignUserID} is {(online ? "online" : "offline")}");
                    m_FriendsLocalSimConnector.StatusNotify(foreignUserID, userID, online);
                }
                else
                {
                    GridRegion region = m_GridService.GetRegionByUUID(UUID.Zero /* !!! */, regionID);
                    if (region != null)
                    {
                        m_logger.LogDebug(
                            $"[HGFRIENDS SERVICE]: Remote Notify to region {region.RegionName}, " +
                            $"user {foreignUserID} is {(online ? "online" : "offline")}");

                        m_FriendsSimConnector.StatusNotify(region, foreignUserID, userID.ToString(), online);
                    }
                }
            }
        }

        #endregion Aux
    }
}
