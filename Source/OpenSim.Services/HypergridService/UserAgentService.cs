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

using System.Net;
using System.Reflection;

using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Connectors.Friends;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;
using Microsoft.Extensions.Configuration;
using log4net.Core;
using Microsoft.Extensions.Logging;
using Autofac;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// This service is for HG1.5 only, to make up for the fact that clients don't
    /// keep any private information in themselves, and that their 'home service'
    /// needs to do it for them.
    /// Once we have better clients, this shouldn't be needed.
    /// </summary>
    public class UserAgentService : IUserAgentService
    {
        // This will need to go into a DB table
        //static Dictionary<UUID, TravelingAgentInfo> m_Database = new Dictionary<UUID, TravelingAgentInfo>();

        protected readonly IGridUserService m_GridUserService;
        protected readonly IGridService m_GridService;
        protected readonly GatekeeperServiceConnector m_GatekeeperConnector;
        protected readonly IGatekeeperService m_GatekeeperService;
        protected readonly IFriendsService m_FriendsService;
        protected readonly IPresenceService m_PresenceService;
        protected readonly IUserAccountService m_UserAccountService;
        protected readonly IFriendsSimConnector m_FriendsLocalSimConnector; // standalone, points to HGFriendsModule

        protected readonly string? m_GridName;
        protected readonly string? m_MyExternalIP = "";

        protected readonly int m_LevelOutsideContacts;
        protected readonly bool m_ShowDetails;

        protected readonly bool m_BypassClientVerification;

        private readonly Dictionary<int, bool> m_ForeignTripsAllowed = new();
        private readonly Dictionary<int, List<string>> m_TripsAllowedExceptions = new();
        private readonly Dictionary<int, List<string>> m_TripsDisallowedExceptions = new();

        protected readonly IComponentContext m_context;
        protected readonly IConfiguration m_config;
        protected readonly ILogger<UserAgentService> m_logger;
        protected readonly IHGTravelingData m_Database;
        protected readonly IFriendsSimConnector m_FriendsSimConnector; // grid

        public UserAgentService(
            IComponentContext componentContext,
            IConfiguration config, 
            ILogger<UserAgentService> logger, 
            IHGTravelingData hGTravelingData,
            IFriendsSimConnector friendsConnector)
        {
            m_context = componentContext;
            m_config = config;
            m_logger = logger;
            m_Database = hGTravelingData;
            m_FriendsSimConnector = friendsConnector;

            string? connString = String.Empty;
            string? realm = "hg_traveling_data";

            // Try reading the [DatabaseService] section, if it exists
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists() is true)
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
            }

            // [UserAgentService] section overrides [DatabaseService], if it exists
            var gridConfig = config.GetSection("UserAgentService");
            if (gridConfig.Exists() is true)
            {
                connString = gridConfig.GetValue("ConnectionString", connString);
                realm = gridConfig.GetValue("Realm", realm);
            }

            m_Database.Initialize(connString, realm);

            // Let's set this always, because we don't know the sequence
            // of instantiations
            if (friendsConnector is not null)
                m_FriendsLocalSimConnector = friendsConnector;

            m_logger.LogDebug("[HOME USERS SECURITY]: Starting...");

            string? gridService = gridConfig.GetValue("GridService", String.Empty);
            string? gridUserService = gridConfig.GetValue("GridUserService", String.Empty);
            string? gatekeeperService = gridConfig.GetValue("GatekeeperService", String.Empty);
            string? friendsService = gridConfig.GetValue("FriendsService", String.Empty);
            string? presenceService = gridConfig.GetValue("PresenceService", String.Empty);
            string? userAccountService = gridConfig.GetValue("UserAccountService", String.Empty);

            m_BypassClientVerification = gridConfig.GetValue<bool>("BypassClientVerification", false);

            if (string.IsNullOrEmpty(gridService) || 
                string.IsNullOrEmpty(gridUserService) || 
                string.IsNullOrEmpty(gatekeeperService))
            {
                throw new Exception(String.Format("Incomplete specifications, UserAgent Service cannot function."));
            }

            m_GatekeeperConnector = m_context.Resolve<GatekeeperServiceConnector>();

            m_GridService = m_context.ResolveNamed<IGridService>(gridService);
            m_GridUserService = m_context.ResolveNamed<IGridUserService>(gridUserService);
            m_GatekeeperService = m_context.ResolveNamed<IGatekeeperService>(gatekeeperService);

            if (string.IsNullOrEmpty(friendsService) is false)
                m_FriendsService = m_context.ResolveNamed<IFriendsService>(friendsService);

            if (string.IsNullOrEmpty(presenceService) is false)
                m_PresenceService = m_context.ResolveNamed<IPresenceService>(presenceService);

            if (string.IsNullOrEmpty(userAccountService)is false)
                m_UserAccountService = m_context.ResolveNamed<IUserAccountService>(userAccountService);

            m_LevelOutsideContacts = gridConfig.GetValue<int>("LevelOutsideContacts", 0);
            m_ShowDetails = gridConfig.GetValue<bool>("ShowUserDetailsInHGProfile", true);

            LoadTripPermissionsFromConfig(gridConfig, "ForeignTripsAllowed");
            LoadDomainExceptionsFromConfig(gridConfig, "AllowExcept", m_TripsAllowedExceptions);
            LoadDomainExceptionsFromConfig(gridConfig, "DisallowExcept", m_TripsDisallowedExceptions);

            m_GridName = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI",
                new string[] { "Startup", "Hypergrid", "UserAgentService" }, String.Empty);

            if (string.IsNullOrEmpty(m_GridName)) // Legacy. Remove soon.
            {
                m_GridName = gridConfig.GetValue("ExternalName", string.Empty);
                if (string.IsNullOrEmpty(m_GridName))
                {
                    var serverConfig = config.GetSection("GatekeeperService");
                    m_GridName = gridConfig.GetValue("ExternalName", string.Empty);
                }
            }

            if (!string.IsNullOrEmpty(m_GridName))
            {
                m_GridName = m_GridName.ToLowerInvariant();
                if (!m_GridName.EndsWith("/"))
                    m_GridName += "/";
                if (!Uri.TryCreate(m_GridName, UriKind.Absolute, out Uri gateURI))
                    throw new Exception(String.Format("[UserAgentService] could not parse gatekeeper uri"));
                string host = gateURI.DnsSafeHost;
                IPAddress ip = Util.GetHostFromDNS(host);
                if(ip is null)
                    throw new Exception(String.Format("[UserAgentService] failed to resolve gatekeeper host"));
                m_MyExternalIP = ip.ToString();
            }
            // Finally some cleanup
            m_Database.DeleteOld();
        }

        // XXX MCD
        protected void LoadTripPermissionsFromConfig(
            IConfigurationSection config, 
            string variable)
        {
            if (config.Exists() is false)
                return;

            foreach (var kvp in config.AsEnumerable())
            {
                if (kvp.Key.StartsWith(variable + "_Level_"))
                {
                    if (Int32.TryParse(kvp.Key.Replace(variable + "_Level_", ""), out int level))
                        m_ForeignTripsAllowed.Add(level, config.GetValue<bool>(kvp.Key, true));
                }
            }
        }

        protected void LoadDomainExceptionsFromConfig(
            IConfigurationSection config, 
            string variable, 
            Dictionary<int, List<string>> exceptions)
        {
            if (config.Exists() is false)
                return;

            foreach (var kvp in config.AsEnumerable())
            {
                if (kvp.Key.StartsWith(variable + "_Level_"))
                {
                    if (Int32.TryParse(kvp.Key.Replace(variable + "_Level_", ""), out int level) && !exceptions.ContainsKey(level))
                    {
                        exceptions.Add(level, new List<string>());
                        string value = config.GetValue(kvp.Key, string.Empty);
                        string[] parts = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string s in parts)
                        {
                            string ss = s.Trim();
                            if(!ss.EndsWith("/"))
                                ss += '/';

                            exceptions[level].Add(ss);
                        }
                    }
                }
            }
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = new Vector3(128, 128, 0); lookAt = Vector3.UnitY;

            m_logger.LogDebug($"[USER AGENT SERVICE]: Request to get home region of user {userID}");

            GridRegion? home = null;
            GridUserInfo uinfo = m_GridUserService.GetGridUserInfo(userID.ToString());
            if (uinfo is not null)
            {
                if (uinfo.HomeRegionID.IsNotZero())
                {
                    home = m_GridService.GetRegionByUUID(UUID.Zero, uinfo.HomeRegionID);
                    position = uinfo.HomePosition;
                    lookAt = uinfo.HomeLookAt;
                }
                if (home is null)
                {
                    List<GridRegion> defs = m_GridService.GetDefaultRegions(UUID.Zero);
                    if (defs is not null && defs.Count > 0)
                        home = defs[0];
                }
            }

            return home;
        }

        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, bool fromLogin, out string reason)
        {
            m_logger.LogDebug(
                $"[USER AGENT SERVICE]: Request to login user {agentCircuit.firstname} {agentCircuit.lastname} " +
                $"(@{(fromLogin ? agentCircuit.IPAddress : "stored IP")} to grid {gatekeeper.ServerURI}");

            string gridName = gatekeeper.ServerURI.ToLowerInvariant();

            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, agentCircuit.AgentID);
            if (account is null)
            {
                m_logger.LogWarning(
                    $"[USER AGENT SERVICE]: Someone attempted to lauch a foreign user from here " +
                    $"{agentCircuit.firstname} {agentCircuit.lastname}");
                    
                reason = "Forbidden to launch your agents from here";
                return false;
            }

            // Is this user allowed to go there?
            if (m_GridName != gridName)
            {
                if (m_ForeignTripsAllowed.ContainsKey(account.UserLevel))
                {
                    bool allowed = m_ForeignTripsAllowed[account.UserLevel];

                    if (m_ForeignTripsAllowed[account.UserLevel] && IsException(gridName, account.UserLevel, m_TripsAllowedExceptions))
                        allowed = false;

                    if (!m_ForeignTripsAllowed[account.UserLevel] && IsException(gridName, account.UserLevel, m_TripsDisallowedExceptions))
                        allowed = true;

                    if (!allowed)
                    {
                        reason = "Your world does not allow you to visit the destination";
                        m_logger.LogInformation($"[USER AGENT SERVICE]: Agents not permitted to visit {gridName}. Refusing service.");

                        return false;
                    }
                }
            }

            // Take the IP address + port of the gatekeeper (reg) plus the info of finalDestination
            GridRegion region = new(gatekeeper)
            {
                ServerURI = gatekeeper.ServerURI,
                ExternalHostName = finalDestination.ExternalHostName,
                InternalEndPoint = finalDestination.InternalEndPoint,
                RegionName = finalDestination.RegionName,
                RegionID = finalDestination.RegionID,
                RegionLocX = finalDestination.RegionLocX,
                RegionLocY = finalDestination.RegionLocY
            };

            // Generate a new service session
            agentCircuit.ServiceSessionID = region.ServerURI + ";" + UUID.Random();
            TravelingAgentInfo travel = CreateTravelInfo(agentCircuit, region, fromLogin, out TravelingAgentInfo old);

            if(!fromLogin && old is not null && !string.IsNullOrEmpty(old.ClientIPAddress))
            {
                m_logger.LogDebug($"[USER AGENT SERVICE]: stored IP = {old.ClientIPAddress}. Old circuit IP: {agentCircuit.IPAddress}");
                agentCircuit.IPAddress = old.ClientIPAddress;
            }

            bool success;

            m_logger.LogDebug($"[USER AGENT SERVICE]: this grid: {m_GridName}, desired grid: {gridName}, desired region: {region.RegionID}");

            if (m_GridName is not null && m_GridName.Equals(gridName, StringComparison.InvariantCultureIgnoreCase))
            {
                success = m_GatekeeperService.LoginAgent(source, agentCircuit, finalDestination, out reason);
            }
            else
            {
                //TODO: Should there not be a call to QueryAccess here?
                EntityTransferContext ctx = new();
                success = m_GatekeeperConnector.CreateAgent(source, region, agentCircuit, (uint)Constants.TeleportFlags.ViaLogin, ctx, out reason);
            }

            if (!success)
            {
                m_logger.LogDebug(
                    $"[USER AGENT SERVICE]: Unable to login user {agentCircuit.firstname} {agentCircuit.lastname} " +
                    $"to grid {region.ServerURI}, reason: {reason}");

                if (old is not null)
                    StoreTravelInfo(old);
                else
                    m_Database.Delete(agentCircuit.SessionID);

                return false;
            }

            // Everything is ok

            StoreTravelInfo(travel);

            return true;
        }

        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, out string reason)
        {
            return LoginAgentToGrid(source, agentCircuit, gatekeeper, finalDestination, false, out reason);
        }

        TravelingAgentInfo CreateTravelInfo(AgentCircuitData agentCircuit, GridRegion region, bool fromLogin, out TravelingAgentInfo existing)
        {
            HGTravelingData hgt = m_Database.Get(agentCircuit.SessionID);
            existing = null;

            if (hgt is not null)
            {
                // Very important! Override whatever this agent comes with.
                // UserAgentService always sets the IP for every new agent
                // with the original IP address.
                existing = new TravelingAgentInfo(hgt);
                agentCircuit.IPAddress = existing.ClientIPAddress;
            }

            TravelingAgentInfo travel = new(existing)
            {
                SessionID = agentCircuit.SessionID,
                UserID = agentCircuit.AgentID,
                GridExternalName = region.ServerURI,
                ServiceToken = agentCircuit.ServiceSessionID
            };

            if (fromLogin)
                travel.ClientIPAddress = agentCircuit.IPAddress;

            StoreTravelInfo(travel);

            return travel;
        }

        public void LogoutAgent(UUID userID, UUID sessionID)
        {
            m_logger.LogDebug($"[USER AGENT SERVICE]: User {userID} logged out", userID);

            m_Database.Delete(sessionID);

            GridUserInfo guinfo = m_GridUserService.GetGridUserInfo(userID.ToString());

            if (guinfo is not null)
            {
                m_GridUserService.LoggedOut(userID.ToString(), sessionID, guinfo.LastRegionID, guinfo.LastPosition, guinfo.LastLookAt);
            }
        }

        // We need to prevent foreign users with the same UUID as a local user
        public bool IsAgentComingHome(UUID sessionID, string thisGridExternalName)
        {
            HGTravelingData hgt = m_Database.Get(sessionID);
            if (hgt is null || hgt.Data is null)
                return false;
            if(!hgt.Data.TryGetValue("GridExternalName", out string htgGrid))
                return false;
            return htgGrid.Equals(thisGridExternalName, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool VerifyClient(UUID sessionID, string reportedIP)
        {
            if (m_BypassClientVerification)
                return true;

            m_logger.LogDebug($"[USER AGENT SERVICE]: Verifying Client session {sessionID} with reported IP {reportedIP}.");

            HGTravelingData hgt = m_Database.Get(sessionID);
            if (hgt is null)
                return false;

            TravelingAgentInfo travel = new(hgt);

            bool result = travel.ClientIPAddress == reportedIP;
            if(!result && !string.IsNullOrEmpty(m_MyExternalIP))
                result = reportedIP == m_MyExternalIP; // NATed

            m_logger.LogDebug(
                $"[USER AGENT SERVICE]: Comparing {reportedIP} with login IP {travel.ClientIPAddress} " +
                $"and MyIP {m_MyExternalIP}; result is {result}");

            return result;
        }

        public bool VerifyAgent(UUID sessionID, string token)
        {
            HGTravelingData hgt = m_Database.Get(sessionID);
            if (hgt is null)
            {
                m_logger.LogDebug($"[USER AGENT SERVICE]: Token verification for session {sessionID}: no such session");
                return false;
            }

            TravelingAgentInfo travel = new TravelingAgentInfo(hgt);
            m_logger.LogDebug($"[USER AGENT SERVICE]: Verifying agent token {token} against {travel.ServiceToken}");
            return travel.ServiceToken == token;
        }

        [Obsolete]
        public List<UUID> StatusNotification(List<string> friends, UUID foreignUserID, bool online)
        {
            if (m_FriendsService == null || m_PresenceService == null)
            {
                m_logger.LogWarning($"[USER AGENT SERVICE]: Unable to perform status notifications because friends or presence services are missing");
                return new List<UUID>();
            }

            List<UUID> localFriendsOnline = new();

            m_logger.LogDebug($"[USER AGENT SERVICE]: Status notification: foreign user {foreignUserID} wants to notify {friends.Count} local friends");

            // First, let's double check that the reported friends are, indeed, friends of that user
            // And let's check that the secret matches
            List<string> usersToBeNotified = new();
            foreach (string uui in friends)
            {
                if (Util.ParseUniversalUserIdentifier(uui, out UUID localUserID, out _, out _, out _, out string secret))
                {
                    FriendInfo[] friendInfos = m_FriendsService.GetFriends(localUserID);
                    foreach (FriendInfo finfo in friendInfos)
                    {
                        if (finfo.Friend.StartsWith(foreignUserID.ToString()) && finfo.Friend.EndsWith(secret))
                        {
                            // great!
                            usersToBeNotified.Add(localUserID.ToString());
                        }
                    }
                }
            }

            // Now, let's send the notifications
            m_logger.LogDebug($"[USER AGENT SERVICE]: Status notification: user has {usersToBeNotified.Count} local friends");

            // First, let's send notifications to local users who are online in the home grid
            PresenceInfo[] friendSessions = m_PresenceService.GetAgents(usersToBeNotified.ToArray());
            if (friendSessions != null && friendSessions.Length > 0)
            {
                PresenceInfo friendSession = null;
                foreach (PresenceInfo pinfo in friendSessions)
                {
                    if (pinfo.RegionID.IsNotZero()) // let's guard against traveling agents
                    {
                        friendSession = pinfo;
                        break;
                    }
                }
                if (friendSession is not null)
                {
                    ForwardStatusNotificationToSim(friendSession.RegionID, foreignUserID, friendSession.UserID, online);
                    usersToBeNotified.Remove(friendSession.UserID.ToString());
                    if (UUID.TryParse(friendSession.UserID, out UUID id))
                        localFriendsOnline.Add(id);

                }
            }

            //// Lastly, let's notify the rest who may be online somewhere else
            //foreach (string user in usersToBeNotified)
            //{
            //    UUID id = new UUID(user);
            //    if (m_Database.ContainsKey(id) && m_Database[id].GridExternalName != m_GridName)
            //    {
            //        string url = m_Database[id].GridExternalName;
            //        // forward
            //        m_logger.WarnFormat("[USER AGENT SERVICE]: User {0} is visiting {1}. HG Status notifications still not implemented.", user, url);
            //    }
            //}

            // and finally, let's send the online friends
            if (online)
            {
                return localFriendsOnline;
            }
            else
                return new List<UUID>();
        }

        [Obsolete]
        protected void ForwardStatusNotificationToSim(UUID regionID, UUID foreignUserID, string user, bool online)
        {
            if (UUID.TryParse(user, out UUID userID))
            {
                if (m_FriendsLocalSimConnector is not null)
                {
                    m_logger.LogDebug($"[USER AGENT SERVICE]: Local Notify, user {foreignUserID} is {(online ? "online" : "offline")}");
                    m_FriendsLocalSimConnector.StatusNotify(foreignUserID, userID, online);
                }
                else
                {
                    GridRegion region = m_GridService.GetRegionByUUID(UUID.Zero /* !!! */, regionID);
                    if (region is not null)
                    {
                        m_logger.LogDebug(
                            $"[USER AGENT SERVICE]: Remote Notify to region {region.RegionName}, user {foreignUserID} " +
                            $"is {(online ? "online" : "offline")}");

                        m_FriendsSimConnector.StatusNotify(/* MCD XXX region,*/ foreignUserID, userID, online);
                    }
                }
            }
        }

        public List<UUID> GetOnlineFriends(UUID foreignUserID, List<string> friends)
        {
            List<UUID> online = new();

            if (m_FriendsService is null || m_PresenceService is null)
            {
                m_logger.LogWarning("[USER AGENT SERVICE]: Unable to get online friends because friends or presence services are missing");
                return online;
            }

            m_logger.LogDebug($"[USER AGENT SERVICE]: Foreign user {foreignUserID} wants to know status of {friends.Count} local friends");

            // First, let's double check that the reported friends are, indeed, friends of that user
            // And let's check that the secret matches and the rights
            List<string> usersToBeNotified = new();
            foreach (string uui in friends)
            {
                if (Util.ParseUniversalUserIdentifier(uui, out UUID localUserID, out _, out _, out _, out string secret))
                {
                    FriendInfo[] friendInfos = m_FriendsService.GetFriends(localUserID);
                    foreach (FriendInfo finfo in friendInfos)
                    {
                        if (finfo.Friend.StartsWith(foreignUserID.ToString()) && finfo.Friend.EndsWith(secret) &&
                            (finfo.TheirFlags & (int)FriendRights.CanSeeOnline) != 0 && (finfo.TheirFlags != -1))
                        {
                            // great!
                            usersToBeNotified.Add(localUserID.ToString());
                        }
                    }
                }
            }

            // Now, let's find out their status
            m_logger.LogDebug($"[USER AGENT SERVICE]: GetOnlineFriends: user has {usersToBeNotified.Count} local friends with status rights");

            // First, let's send notifications to local users who are online in the home grid
            PresenceInfo[] friendSessions = m_PresenceService.GetAgents(usersToBeNotified.ToArray());
            if (friendSessions is not null && friendSessions.Length > 0)
            {
                foreach (PresenceInfo pi in friendSessions)
                {
                    if (UUID.TryParse(pi.UserID, out UUID presenceID))
                        online.Add(presenceID);
                }
            }

            return online;
        }

        public Dictionary<string, object> GetUserInfo(UUID  userID)
        {
            Dictionary<string, object> info = new();

            if (m_UserAccountService is null)
            {
                m_logger.LogWarning($"[USER AGENT SERVICE]: Unable to get user flags because user account service is missing");

                info["result"] = "fail";
                info["message"] = "UserAccountService is missing!";
                return info;
            }

            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero /*!!!*/, userID);

            if (account != null)
            {
                info.Add("user_firstname", account.FirstName);
                info.Add("user_lastname", account.LastName);
                info.Add("result", "success");

                if (m_ShowDetails)
                {
                    info.Add("user_flags", account.UserFlags);
                    info.Add("user_created", account.Created);
                    info.Add("user_title", account.UserTitle);
                }
                else
                {
                    info.Add("user_flags", 0);
                    info.Add("user_created", 0);
                    info.Add("user_title", string.Empty);
                }
            }

            return info;
        }

        public Dictionary<string, object> GetServerURLs(UUID userID)
        {
            if (m_UserAccountService is null)
            {
                m_logger.LogWarning($"[USER AGENT SERVICE]: Unable to get server URLs because user account service is missing");

                return new Dictionary<string, object>();
            }

            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero /*!!!*/, userID);
            if (account != null)
                return account.ServiceURLs;

            return new Dictionary<string, object>();
        }

        public string LocateUser(UUID userID)
        {
            HGTravelingData[] hgts = m_Database.GetSessions(userID);
            if (hgts == null)
                return string.Empty;

            foreach (HGTravelingData t in hgts)
                if (t.Data.ContainsKey("GridExternalName") && !m_GridName.Equals(t.Data["GridExternalName"]))
                    return t.Data["GridExternalName"];

            return string.Empty;
        }

        public string GetUUI(UUID userID, UUID targetUserID)
        {
            // Let's see if it's a local user
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, targetUserID);
            if (account is not null)
                return targetUserID.ToString() + ";" + m_GridName + ";" + account.FirstName + " " + account.LastName ;

            // Let's try the list of friends
            if(m_FriendsService is not null)
            {
                FriendInfo[] friends = m_FriendsService.GetFriends(userID);
                if (friends is not null && friends.Length > 0)
                {
                    foreach (FriendInfo f in friends)
                        if (f.Friend.StartsWith(targetUserID.ToString()))
                        {
                            // Let's remove the secret
                            if (Util.ParseUniversalUserIdentifier(f.Friend, out _,
                                    out _, out _, out _, out string secret))
                                return f.Friend.Replace(secret, "0");
                        }
                }
            }
            return string.Empty;
        }

        public UUID GetUUID(String first, String last)
        {
            // Let's see if it's a local user
            UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, first, last);
            if (account is not null)
            {
                // check user level
                if (account.UserLevel < m_LevelOutsideContacts)
                    return UUID.Zero;
                else
                    return account.PrincipalID;
            }
            else
                return UUID.Zero;
        }

        #region Misc

        private bool IsException(string dest, int level, Dictionary<int, List<string>> exceptions)
        {
            if (string.IsNullOrEmpty(dest))
                return false;
            if (!exceptions.TryGetValue(level, out List<string> excep) || excep.Count == 0)
                return false;

            string destination = dest;
            if (!destination.EndsWith("/"))
                destination += "/";

            foreach (string s in excep)
            {
                if (destination.Equals(s))
                    return true;
            }

            return false;
        }

        private void StoreTravelInfo(TravelingAgentInfo travel)
        {
            if (travel is null)
                return;

            HGTravelingData hgt = new()
            {
                SessionID = travel.SessionID,
                UserID = travel.UserID,
                Data = new Dictionary<string, string>
                {
                    ["GridExternalName"] = travel.GridExternalName,
                    ["ServiceToken"] = travel.ServiceToken,
                    ["ClientIPAddress"] = travel.ClientIPAddress
                }
            };

            m_Database.Store(hgt);
        }
        #endregion

    }

    class TravelingAgentInfo
    {
        public UUID SessionID;
        public UUID UserID;
        public string GridExternalName = string.Empty;
        public string ServiceToken = string.Empty;
        public string ClientIPAddress = string.Empty; // as seen from this user agent service

        public TravelingAgentInfo(HGTravelingData t)
        {
            if (t.Data is not null)
            {
                SessionID = new UUID(t.SessionID);
                UserID = new UUID(t.UserID);
                GridExternalName = t.Data["GridExternalName"];
                ServiceToken = t.Data["ServiceToken"];
                ClientIPAddress = t.Data["ClientIPAddress"];
            }
        }

        public TravelingAgentInfo(TravelingAgentInfo old)
        {
            if (old is not null)
            {
                SessionID = old.SessionID;
                UserID = old.UserID;
                GridExternalName = old.GridExternalName;
                ServiceToken = old.ServiceToken;
                ClientIPAddress = old.ClientIPAddress;
            }
        }
    }

}
