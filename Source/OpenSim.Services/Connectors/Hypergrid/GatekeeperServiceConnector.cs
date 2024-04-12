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
using System.Drawing;
using System.Net;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using Nwc.XmlRpc;
using OpenSim.Services.Connectors.Simulation;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class GatekeeperServiceConnector : SimulationServiceConnector
    {
        private static UUID m_HGMapImage = new UUID("00000000-0000-1111-9999-000000000013");

        private readonly IConfiguration m_configuration;
        private readonly ILogger<GatekeeperServiceConnector> m_logger;
        private readonly IAssetService m_AssetService;

        public GatekeeperServiceConnector(
            IConfiguration configuration,
            ILogger<GatekeeperServiceConnector> logger,
            IAssetService assetService) 
            : base(configuration, logger)
        {
            m_configuration = configuration;
            m_logger = logger;
            m_AssetService = assetService;
        }

        protected override string AgentPath()
        {
            return "foreignagent/";
        }

        protected override string ObjectPath()
        {
            return "foreignobject/";
        }

        public bool LinkRegion(GridRegion info, out UUID regionID, out ulong realHandle, out string externalName, out string imageURL, out string reason, out int sizeX, out int sizeY)
        {
            regionID = UUID.Zero;
            imageURL = string.Empty;
            realHandle = 0;
            externalName = string.Empty;
            reason = string.Empty;
            sizeX = (int)Constants.RegionSize;
            sizeY = (int)Constants.RegionSize;

            Hashtable hash = new Hashtable();
            hash["region_name"] = info.RegionName;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("link_region", paramList);

            m_logger.LogDebug($"Linking to {info.ServerURI}");
            XmlRpcResponse? response = null;

            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                response = request.Send(info.ServerURI, hclient);
            }
            catch (Exception e)
            {
                m_logger.LogDebug(e, "Error contacting remote server");
                reason = "Error contacting remote server";
                return false;
            }

            if (response.IsFault)
            {
                reason = response.FaultString;
                m_logger.LogError($"Remote call returned an error: {response.FaultString}");
                return false;
            }

            hash = (Hashtable)response.Value;

            try
            {
                bool success = false;
                Boolean.TryParse((string)hash["result"], out success);
                if (success)
                {
                    UUID.TryParse((string)hash["uuid"], out regionID);
                    if ((string)hash["handle"] != null)
                    {
                        realHandle = Convert.ToUInt64((string)hash["handle"]);
                    }
                    if (hash["region_image"] != null)
                    {
                        imageURL = (string)hash["region_image"];
                    }
                    if (hash["external_name"] != null)
                    {
                        externalName = (string)hash["external_name"];
                    }
                    if (hash["size_x"] != null)
                    {
                        Int32.TryParse((string)hash["size_x"], out sizeX);
                    }
                    if (hash["size_y"] != null)
                    {
                        Int32.TryParse((string)hash["size_y"], out sizeY);
                    }
                }
            }
            catch (Exception e)
            {
                reason = "Error parsing return arguments";
                m_logger.LogError(e, "Got exception while parsing hyperlink response");
                return false;
            }

            return true;
        }

        public UUID GetMapImage(UUID regionID, string imageURL, string storagePath)
        {
            if (m_AssetService == null)
            {
                m_logger.LogDebug("No AssetService defined. Map tile not retrieved.");
                return m_HGMapImage;
            }

            UUID mapTile = m_HGMapImage;
            string filename = string.Empty;

            try
            {
                //m_logger.Debug("JPEG: " + imageURL);
                string name = regionID.ToString();
                filename = Path.Combine(storagePath, name + ".jpg");

                m_logger.LogDebug($"Map image at {imageURL}, cached at {filename}");
                
                if (!File.Exists(filename))
                {
                    m_logger.LogDebug("Downloading...");

                    using(WebClient c = new WebClient())
                        c.DownloadFile(imageURL, filename);
                }
                else
                {
                    m_logger.LogDebug("Using cached image");
                }

                byte[]? imageData = null;

                using (Bitmap bitmap = new Bitmap(filename))
                {
                    imageData = OpenJPEG.EncodeFromImage(bitmap, false);
                }

                AssetBase ass = new AssetBase(UUID.Random(), "region " + name, (sbyte)AssetType.Texture, regionID.ToString());

                ass.Data = imageData;

                m_AssetService.Store(ass);

                // finally
                mapTile = ass.FullID;
            }
            catch // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                m_logger.LogInformation("Failed getting/storing map image, because it is probably already in the cache");
            }

            return mapTile;
        }

        public GridRegion? GetHyperlinkRegion(GridRegion gatekeeper, UUID regionID, UUID agentID, string agentHomeURI, out string message)
        {
            Hashtable hash = new Hashtable();
            hash["region_uuid"] = regionID.ToString();
            
            if (!agentID.IsZero())
            {
                hash["agent_id"] = agentID.ToString();
                if (agentHomeURI != null)
                    hash["agent_home_uri"] = agentHomeURI;
            }

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_region", paramList);
            m_logger.LogDebug($"Contacting {gatekeeper.ServerURI}");
            XmlRpcResponse response = null;

            try
            {
                using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                response = request.Send(gatekeeper.ServerURI, hclient);
            }
            catch (Exception e)
            {
                message = "Error contacting grid.";
                m_logger.LogDebug(e, message);
                return null;
            }

            if (response.IsFault)
            {
                message = "Error contacting grid.";
                m_logger.LogError($"Remote call returned an error: {response.FaultString}");
                return null;
            }

            hash = (Hashtable)response.Value;

            try
            {
                bool success = false;
                Boolean.TryParse((string)hash["result"], out success);

                if (hash["message"] != null)
                    message = (string)hash["message"];
                else if (success)
                    message = null;
                else
                    message = "The teleport destination could not be found.";   // probably the dest grid is old and doesn't send 'message', but the most common problem is that the region is unavailable

                if (success)
                {
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
                    {
                        region.ExternalHostName = (string)hash["hostname"];
                    }

                    if (hash["http_port"] != null)
                    {
                        uint p = 0;
                        UInt32.TryParse((string)hash["http_port"], out p);
                        region.HttpPort = p;
                    }

                    if (hash["internal_port"] != null)
                    {
                        int p = 0;
                        Int32.TryParse((string)hash["internal_port"], out p);
                        region.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), p);
                    }

                    if (hash["server_uri"] != null)
                    {
                        region.ServerURI = (string)hash["server_uri"];
                        //m_logger.Debug(">> HERE, server_uri: " + region.ServerURI);
                    }

                    // Successful return
                    return region;
                }

            }
            catch (Exception e)
            {
                message = "Error parsing response from grid.";
                m_logger.LogError(e, $"Got exception while parsing hyperlink response");
                return null;
            }

            return null;
        }
    }
}
