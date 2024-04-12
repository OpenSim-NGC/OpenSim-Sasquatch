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

using System.Collections;
using System.Net;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Simulation;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nwc.XmlRpc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class UserAgentServiceConnector : SimulationServiceConnector, IUserAgentService
    {
        private string m_ServerURL;
        private GridRegion m_Gatekeeper;

        private readonly IConfiguration m_configuration;
        private readonly ILogger<UserAgentServiceConnector> m_logger;

        public UserAgentServiceConnector(
            IConfiguration configuration,
            ILogger<UserAgentServiceConnector> logger,
            string url)
            : this(configuration, logger)
        {
            setServiceURL(url);
        }

        public UserAgentServiceConnector(
            IConfiguration configuration,
            ILogger<UserAgentServiceConnector> logger
            ) : base(configuration, logger)
        {
            m_configuration = configuration;
            m_logger = logger;

            GridInfo tmp = new GridInfo(m_configuration);

            string serviceURI = tmp.HomeURL;

            if (String.IsNullOrWhiteSpace(serviceURI))
            {
                m_logger.LogError("No Home URI named in configuration");
                throw new Exception("UserAgent connector init error");
            }

            if (!setServiceURL(serviceURI))
            {
                throw new Exception("UserAgent connector init error");
            }
        }

        private bool setServiceURL(string url)
        {
            // validate url getting some extended error messages
            url = url.ToLower();
            try
            {
                Uri tmpuri = new Uri(url);
            }
            catch (Exception e)
            {
                m_logger.LogDebug(e, $"Malformed Uri {url}");
                return false;
            }

            m_ServerURL = url;
            if (!m_ServerURL.EndsWith("/"))
                m_ServerURL += "/";

            return true;
        }

        protected override string AgentPath()
        {
            return "homeagent/";
        }

        // The Login service calls this interface with fromLogin=true
        // Sims call it with fromLogin=false
        // Either way, this is verified by the handler
        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, bool fromLogin, out string reason)
        {
            reason = String.Empty;

            if (destination == null)
            {
                reason = "Destination is null";
                m_logger.LogDebug("Given destination is null");
                return false;
            }

            GridRegion home = new GridRegion()
            {
                ServerURI = m_ServerURL,
                RegionID = destination.RegionID,
                RegionLocX = destination.RegionLocX,
                RegionLocY = destination.RegionLocY
            };

            m_Gatekeeper = gatekeeper;

            //Console.WriteLine("   >>> LoginAgentToGrid <<< " + home.ServerURI);

            uint flags = fromLogin ? (uint)TeleportFlags.ViaLogin : (uint)TeleportFlags.ViaHome;

            return CreateAgent(source, home, aCircuit, flags, new EntityTransferContext(), out reason);
        }


        // The simulators call this interface
        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, out string reason)
        {
            return LoginAgentToGrid(source, aCircuit, gatekeeper, destination, false, out reason);
        }

        protected override void PackData(OSDMap args, GridRegion source, AgentCircuitData aCircuit, GridRegion destination, uint flags)
        {
            base.PackData(args, source, aCircuit, destination, flags);

            args["gatekeeper_serveruri"] = OSD.FromString(m_Gatekeeper.ServerURI);
            args["gatekeeper_host"] = OSD.FromString(m_Gatekeeper.ExternalHostName);
            args["gatekeeper_port"] = OSD.FromString(m_Gatekeeper.HttpPort.ToString());
            args["destination_serveruri"] = OSD.FromString(destination.ServerURI);
        }

        public void SetClientToken(UUID sessionID, string token)
        {
            // no-op
        }

        private Hashtable CallServer(string methodName, Hashtable hash)
        {
            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest(methodName, paramList);

            // Send and get reply
            XmlRpcResponse response = null;
            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                response = request.Send(m_ServerURL, hclient);
            }
            catch (Exception e)
            {
                m_logger.LogDebug(e, $"{methodName} call to {m_ServerURL} failed");
                throw;
            }

            if (response == null || response.IsFault)
            {
                throw new Exception(string.Format("{0} call to {1} returned an error: {2}", methodName, m_ServerURL, response.FaultString));
            }

            if (!(response.Value is Hashtable))
            {
                throw new Exception(string.Format("{0} call to {1} returned null", methodName, m_ServerURL));
            }

            return (Hashtable)response.Value;
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = Vector3.UnitY; lookAt = Vector3.UnitY;

            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("get_home_region", hash);

            bool success;
            if (!Boolean.TryParse((string)hash["result"], out success) || !success)
                return null;

            GridRegion region = new GridRegion();

            UUID.TryParse((string)hash["uuid"], out region.RegionID);
            int n = 0;

            if (hash["x"] != null)
            {
                Int32.TryParse((string)hash["x"], out n);
                region.RegionLocX = n;
            }
            
            if (hash["y"] != null)
            {
                Int32.TryParse((string)hash["y"], out n);
                region.RegionLocY = n;
            }
            
            if (hash["size_x"] != null)
            {
                Int32.TryParse((string)hash["size_x"], out n);
                region.RegionSizeX = n;
            }
            
            if (hash["size_y"] != null)
            {
                Int32.TryParse((string)hash["size_y"], out n);
                region.RegionSizeY = n;
            }
            
            if (hash["region_name"] != null)
            {
                region.RegionName = (string)hash["region_name"];
            }
            
            if (hash["hostname"] != null)
                region.ExternalHostName = (string)hash["hostname"];
            
            if (hash["http_port"] != null)
            {
                uint p = 0;
                UInt32.TryParse((string)hash["http_port"], out p);
                region.HttpPort = p;
            }
            
            if (hash.ContainsKey("server_uri") && hash["server_uri"] != null)
                region.ServerURI = (string)hash["server_uri"];

            if (hash["internal_port"] != null)
            {
                int p = 0;
                Int32.TryParse((string)hash["internal_port"], out p);
                region.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), p);
            }
            
            if (hash["position"] != null)
                Vector3.TryParse((string)hash["position"], out position);
            
            if (hash["lookAt"] != null)
                Vector3.TryParse((string)hash["lookAt"], out lookAt);

            // Successful return
            return region;
        }

        public bool IsAgentComingHome(UUID sessionID, string thisGridExternalName)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["externalName"] = thisGridExternalName;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("agent_is_coming_home", paramList);
            string reason = string.Empty;

            return GetBoolResponse(request, out reason);
        }

        public bool VerifyAgent(UUID sessionID, string token)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["token"] = token;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("verify_agent", paramList);
            string reason = string.Empty;

            return GetBoolResponse(request, out reason);
        }

        public bool VerifyClient(UUID sessionID, string token)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["token"] = token;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("verify_client", paramList);
            string reason = string.Empty;

            return GetBoolResponse(request, out reason);
        }

        public void LogoutAgent(UUID userID, UUID sessionID)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["userID"] = userID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("logout_agent", paramList);
            string reason = string.Empty;

            GetBoolResponse(request, out reason);
        }

        [Obsolete]
        public List<UUID> StatusNotification(List<string> friends, UUID userID, bool online)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            hash["online"] = online.ToString();
            int i = 0;

            foreach (string s in friends)
            {
                hash["friend_" + i.ToString()] = s;
                i++;
            }

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("status_notification", paramList);
//            string reason = string.Empty;

            // Send and get reply
            List<UUID> friendsOnline = new List<UUID>();
            XmlRpcResponse response = null;
            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                response = request.Send(m_ServerURL, hclient);
            }
            catch
            {
                m_logger.LogDebug($"Unable to contact remote server {m_ServerURL} for StatusNotification");
//                reason = "Exception: " + e.Message;
                return friendsOnline;
            }

            if (response.IsFault)
            {
                m_logger.LogError($"Remote call to {m_ServerURL} for StatusNotification returned an error: {response.FaultString}");
//                reason = "XMLRPC Fault";
                return friendsOnline;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_logger.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_logger.LogError($"GetOnlineFriends Got null response from {m_ServerURL}! THIS IS BAAAAD");
//                    reason = "Internal error 1";
                    return friendsOnline;
                }

                // Here is the actual response
                foreach (object key in hash.Keys)
                {
                    if (key is string && ((string)key).StartsWith("friend_") && hash[key] != null)
                    {
                        UUID uuid;
                        if (UUID.TryParse(hash[key].ToString(), out uuid))
                            friendsOnline.Add(uuid);
                    }
                }

            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Got exception on GetOnlineFriends response.");
//                reason = "Exception: " + e.Message;
            }

            return friendsOnline;
        }

        [Obsolete]
        public List<UUID> GetOnlineFriends(UUID userID, List<string> friends)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            int i = 0;

            foreach (string s in friends)
            {
                hash["friend_" + i.ToString()] = s;
                i++;
            }

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_online_friends", paramList);
//            string reason = string.Empty;

            // Send and get reply
            List<UUID> online = new List<UUID>();
            XmlRpcResponse response = null;
            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                response = request.Send(m_ServerURL, hclient);
            }
            catch
            {
                m_logger.LogDebug($"Unable to contact remote server {m_ServerURL} for GetOnlineFriends");
//                reason = "Exception: " + e.Message;
                return online;
            }

            if (response.IsFault)
            {
                m_logger.LogError($"remote call to {m_ServerURL} for GetOnlineFriends returned an error: {response.FaultString}");
//                reason = "XMLRPC Fault";
                return online;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_logger.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_logger.LogError($"GetOnlineFriends Got null response from {m_ServerURL}! THIS IS BAAAAD");
//                    reason = "Internal error 1";
                    return online;
                }

                // Here is the actual response
                foreach (object key in hash.Keys)
                {
                    if (key is string && ((string)key).StartsWith("friend_") && hash[key] != null)
                    {
                        UUID uuid;
                        if (UUID.TryParse(hash[key].ToString(), out uuid))
                            online.Add(uuid);
                    }
                }

            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Got exception on GetOnlineFriends response.");
//                reason = "Exception: " + e.Message;
            }

            return online;
        }

        public Dictionary<string,object> GetUserInfo (UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("get_user_info", hash);

            Dictionary<string, object> info = new Dictionary<string, object>();

            foreach (object key in hash.Keys)
            {
                if (hash[key] != null)
                {
                    info.Add(key.ToString(), hash[key]);
                }
            }

            return info;
        }

        public Dictionary<string, object> GetServerURLs(UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("get_server_urls", hash);

            Dictionary<string, object> serverURLs = new Dictionary<string, object>();
            foreach (object key in hash.Keys)
            {
                if (key is string && ((string)key).StartsWith("SRV_") && hash[key] != null)
                {
                    string serverType = key.ToString().Substring(4); // remove "SRV_"
                    serverURLs.Add(serverType, hash[key].ToString());
                }
            }

            return serverURLs;
        }

        public string LocateUser(UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("locate_user", hash);

            string url = string.Empty;

            // Here's the actual response
            if (hash.ContainsKey("URL"))
                url = hash["URL"].ToString();

            return url;
        }

        public string GetUUI(UUID userID, UUID targetUserID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            hash["targetUserID"] = targetUserID.ToString();

            hash = CallServer("get_uui", hash);

            string uui = string.Empty;

            // Here's the actual response
            if (hash.ContainsKey("UUI"))
                uui = hash["UUI"].ToString();

            return uui;
        }

        public UUID GetUUID(String first, String last)
        {
            Hashtable hash = new Hashtable();
            hash["first"] = first;
            hash["last"] = last;

            hash = CallServer("get_uuid", hash);

            if (!hash.ContainsKey("UUID"))
            {
                throw new Exception(string.Format("get_uuid call to {0} didn't return a UUID", m_ServerURL));
            }

            UUID uuid;
            if (!UUID.TryParse(hash["UUID"].ToString(), out uuid))
            {
                throw new Exception(string.Format("get_uuid call to {0} returned an invalid UUID: {1}", m_ServerURL, hash["UUID"].ToString()));
            }

            return uuid;
        }

        private bool GetBoolResponse(XmlRpcRequest request, out string reason)
        {
            //m_logger.Debug("GetBoolResponse from/to " + m_ServerURL);
            XmlRpcResponse response = null;
            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                response = request.Send(m_ServerURL, hclient);
            }
            catch (Exception e)
            {
                m_logger.LogDebug($"Unable to contact remote server {m_ServerURL} for GetBoolResponse");
                reason = "Exception: " + e.Message;
                return false;
            }

            if (response.IsFault)
            {
                m_logger.LogError($"remote call to {m_ServerURL} for GetBoolResponse returned an error: {response.FaultString}");
                reason = "XMLRPC Fault";
                return false;
            }

            Hashtable hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_logger.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_logger.LogError($"Got null response from {m_ServerURL}! THIS IS BAAAAD");
                    reason = "Internal error 1";
                    return false;
                }

                bool success = false;
                reason = string.Empty;
                if (hash.ContainsKey("result"))
                    Boolean.TryParse((string)hash["result"], out success);
                else
                {
                    reason = "Internal error 2";
                    m_logger.LogWarning($"Response from {m_ServerURL} does not have expected key 'result'");
                }

                return success;
            }
            catch (Exception e)
            {
                m_logger.LogError("Got exception on GetBoolResponse response.");
                
                if (hash.ContainsKey("result") && hash["result"] != null)
                    m_logger.LogError($"Reply was {hash["result"]}");

                reason = "Exception: " + e.Message;
                return false;
            }

        }

    }
}
