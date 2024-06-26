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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Framework.ServiceAuth;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class UserAliasServicesConnector : BaseServiceConnector, IUserAliasService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public UserAliasServicesConnector()
        {
        }

        public UserAliasServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public UserAliasServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig aliasConfig = source.Configs["UserAliasService"];
            if (aliasConfig == null)
            {
                m_log.Error("[ALIAS CONNECTOR]: UserAliasService missing from OpenSim.ini");
                throw new Exception("User Alias connector init error");
            }

            string serviceURI = aliasConfig.GetString("UserAliasServerURI", string.Empty);

            if (string.IsNullOrWhiteSpace(serviceURI))
            {
                m_log.Error("[ACCOUNT CONNECTOR]: UserAliasServerURI not found in section UserAliasService");
                throw new Exception("User Alias connector init error");
            }

            OSHHTPHost tmp = new OSHHTPHost(serviceURI, true);
            if (!tmp.IsResolvedHost)
            {
                m_log.ErrorFormat("[ALIAS CONNECTOR]: {0}", tmp.IsValidHost ? "Could not resolve UserAliasServerURI" : "UserAliasServerURI is a invalid host");
                throw new Exception("User Alias connector init error");
            }

            m_ServerURI = tmp.URI;

            base.Initialise(source, "UserAliasService");
        }

        public UserAlias GetUserForAlias(UUID aliasID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getuserforalias";
            sendData["AliasID"] = aliasID.ToString();

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/useralias";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, m_Auth);

                if (string.IsNullOrEmpty(reply))
                {
                    m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: GetUserForAlias received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: Exception when contacting user alias server at {0}: {1}", uri, e.Message);
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if ((replyData != null) && replyData.ContainsKey("result") && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                {
                    var alias = new UserAlias((Dictionary<string, object>)replyData["result"]);
                    return alias;
                }
            }

            return null;
        }

        public List<UserAlias> GetUserAliases(UUID userID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getuseraliases";
            sendData["UserID"] = userID.ToString();

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/useralias";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, m_Auth);

                if (string.IsNullOrEmpty(reply))
                {
                    m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: GetUserLiases received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: Exception when contacting user alias server at {0}: {1}", uri, e.Message);
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if ((replyData == null) || 
                (replyData.ContainsKey("result") && replyData["result"].ToString() == "null"))
            {
                return null;
            }

            Dictionary<string, object>.ValueCollection aliasList = replyData.Values;
            List<UserAlias> userAliases = new List<UserAlias>();

            foreach (object elements in aliasList)
            {
                if (elements is Dictionary<string, object>)
                {
                    var alias = new UserAlias((Dictionary<string, object>)elements);
                    userAliases.Add(alias);
                }
                else
                {
                    m_log.DebugFormat(
                        "[USER ALIAS CONNECTOR]: GetUserAliases received invalid response type {0}",
                        elements.GetType());
                }
            }

            return userAliases;
        }

        public UserAlias CreateAlias(UUID AliasID, UUID UserID, string Description)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "createalias";
            sendData["AliasID"] = AliasID.ToString();
            sendData["UserID"] = UserID.ToString();
            sendData["Description"] = Description.ToString();

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/useralias";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, m_Auth);

                if (string.IsNullOrEmpty(reply))
                {
                    m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: CreateAlias received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: Exception when contacting user alias server at {0}: {1}", uri, e.Message);
                return null;
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if ((replyData != null) && replyData.ContainsKey("result") && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                {
                    var alias = new UserAlias((Dictionary<string, object>)replyData["result"]);
                    return alias;
                }
            }

            return null;
        }

        public bool DeleteAlias(UUID aliasID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "deletealias";
            sendData["AliasID"] = aliasID.ToString();

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/useralias";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, m_Auth);

                if (string.IsNullOrEmpty(reply))
                {
                    m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: DeleteAlias received null or empty reply");
                    return false;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT ALIAS CONNECTOR]: Exception when contacting user alias server at {0}: {1}", uri, e.Message);
                return false;
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
            if ((replyData != null) && replyData.ContainsKey("result") && (replyData["result"] != null))
            {
                var result = (bool)replyData["result"];
                return result;
            }

            return false;
        }
    }
}