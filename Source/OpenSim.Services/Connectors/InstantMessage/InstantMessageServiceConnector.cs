﻿/*
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

using OpenMetaverse;
using Nwc.XmlRpc;

using OpenSim.Framework;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.Connectors.InstantMessage
{
    public class InstantMessageServiceConnector
    {
        protected readonly ILogger<InstantMessageServiceConnector> m_logger;

        public InstantMessageServiceConnector(ILogger<InstantMessageServiceConnector> logger)
        {
            m_logger = logger;
        }

        /// <summary>
        /// This actually does the XMLRPC Request
        /// </summary>
        /// <param name="url">URL we pull the data out of to send the request to</param>
        /// <param name="im">The Instant Message </param>
        /// <returns>Bool if the message was successfully delivered at the other side.</returns>
        public bool SendInstantMessage(string url, GridInstantMessage im, string messageKey)
        {
            Hashtable xmlrpcdata = ConvertGridInstantMessageToXMLRPC(im, messageKey);
            xmlrpcdata["region_handle"] = 0;

            XmlRpcRequest GridReq = new("grid_instant_message", new ArrayList { xmlrpcdata });
            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                XmlRpcResponse GridResp = GridReq.Send(url, hclient);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("success"))
                {
                    return (responseData["success"] as string == "TRUE");
                }
                else
                {
                    m_logger.LogDebug($"[GRID INSTANT MESSAGE]: No response from {url}");
                    return false;
                }
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "[GRID INSTANT MESSAGE]: Error sending message to {0}", url);
            }

            return false;
        }

        /// <summary>
        /// Takes a GridInstantMessage and converts it into a Hashtable for XMLRPC
        /// </summary>
        /// <param name="msg">The GridInstantMessage object</param>
        /// <returns>Hashtable containing the XMLRPC request</returns>
        protected Hashtable ConvertGridInstantMessageToXMLRPC(GridInstantMessage msg, string messageKey)
        {
            Hashtable gim = new()
            {
                ["from_agent_id"] = msg.fromAgentID.ToString(),
                // Kept for compatibility
                ["from_agent_session"] = UUID.Zero.ToString(),
                ["to_agent_id"] = msg.toAgentID.ToString(),
                ["im_session_id"] = msg.imSessionID.ToString(),
                ["timestamp"] = msg.timestamp.ToString(),
                ["from_agent_name"] = msg.fromAgentName,
                ["message"] = msg.message,
                ["from_group"] = msg.fromGroup ? "TRUE" : "FALSE",
                ["parent_estate_id"] = msg.ParentEstateID.ToString(),
                ["position_x"] = msg.Position.X.ToString(),
                ["position_y"] = msg.Position.Y.ToString(),
                ["position_z"] = msg.Position.Z.ToString(),
                ["region_id"] = msg.RegionID.ToString(),

                ["binary_bucket"] = Convert.ToBase64String(msg.binaryBucket, Base64FormattingOptions.None),
                ["region_id"] = new UUID(msg.RegionID).ToString(),

                ["dialog"] = Convert.ToBase64String(new byte[] { msg.dialog }, Base64FormattingOptions.None),
                ["offline"] = Convert.ToBase64String(new byte[] { msg.offline }, Base64FormattingOptions.None)
            };

            if (!string.IsNullOrEmpty(messageKey))
                gim["message_key"] = messageKey;

            return gim;
        }

    }
}
