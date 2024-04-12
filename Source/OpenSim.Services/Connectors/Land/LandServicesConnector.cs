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
using OpenSim.Framework;

using OpenSim.Services.Interfaces;
using OpenMetaverse;
using Nwc.XmlRpc;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using Microsoft.Extensions.Logging;

namespace OpenSim.Services.Connectors
{
    public class LandServicesConnector : ILandService
    {
        protected IGridService? m_GridService = null;
        protected readonly ILogger<LandServicesConnector> m_logger;

        public LandServicesConnector(ILogger<LandServicesConnector> logger)
        {
            m_logger = logger;
        }

        public virtual void Initialise(IGridService gridServices)
        {
            m_GridService = gridServices;
        }

        public virtual LandData? GetLandData(UUID scopeID, ulong regionHandle, uint x, uint y, out byte regionAccess)
        {
            LandData? landData = null;

            IList paramList = new ArrayList();
            regionAccess = 42; // Default to adult. Better safe...

            try
            {
                uint xpos = 0, ypos = 0;
                Util.RegionHandleToWorldLoc(regionHandle, out xpos, out ypos);

                GridRegion? info = m_GridService?.GetRegionByPosition(scopeID, (int)xpos, (int)ypos);
                if (info != null) // just to be sure
                {
                    string targetHandlestr = info.RegionHandle.ToString();
                    if( ypos == 0 ) //HG proxy?
                    {
                        // this is real region handle on hg proxies hack
                        targetHandlestr = info.RegionSecret;
                    }

                    Hashtable hash = new Hashtable();
                    hash["region_handle"] = targetHandlestr;
                    hash["x"] = x.ToString();
                    hash["y"] = y.ToString();
                    paramList.Add(hash);

                    XmlRpcRequest request = new XmlRpcRequest("land_data", paramList);
                    using HttpClient hclient = WebUtil.GetNewGlobalHttpClient(10000);
                    XmlRpcResponse response = request.Send(info.ServerURI, hclient);

                    if (response.IsFault)
                    {
                        m_logger.LogError($"[LAND CONNECTOR]: remote call returned an error: {response.FaultString}");
                    }
                    else
                    {
                        hash = (Hashtable)response.Value;
                        try
                        {
                            landData = new LandData();

                            landData.AABBMax = Vector3.Parse(hash["AABBMax"] as string);
                            landData.AABBMin = Vector3.Parse(hash["AABBMin"] as string);
                            landData.Area = Convert.ToInt32(hash["Area"]);
                            landData.AuctionID = Convert.ToUInt32(hash["AuctionID"]);
                            landData.Description = hash["Description"] as string;
                            landData.Flags = Convert.ToUInt32(hash["Flags"]);
                            landData.GlobalID = new UUID(hash["GlobalID"] as string);
                            landData.Name = hash["Name"] as string;
                            landData.OwnerID = new UUID(hash["OwnerID"] as string);
                            landData.SalePrice = Convert.ToInt32(hash["SalePrice"]);
                            landData.SnapshotID = new UUID(hash["SnapshotID"] as string);
                            landData.UserLocation = Vector3.Parse(hash["UserLocation"] as string);

                            if (hash["RegionAccess"] != null)
                            {
                                regionAccess = (byte)Convert.ToInt32(hash["RegionAccess"] as string);
                            }

                            if(hash["Dwell"] != null)
                            {
                                landData.Dwell = Convert.ToSingle(hash["Dwell"] as string);
                            }
                            
                            m_logger.LogDebug($"[LAND CONNECTOR]: Got land data for parcel {landData.Name}");
                        }
                        catch (Exception e)
                        {
                            m_logger.LogError(e, "[LAND CONNECTOR]: Got exception while parsing land-data");
                        }
                    }
                }
                else
                    m_logger.LogWarning($"[LAND CONNECTOR]: Couldn't find region with handle {regionHandle}");
            }
            catch (Exception e)
            {
                m_logger.LogError(e, $"[LAND CONNECTOR]: Couldn't contact region {regionHandle}");
            }

            return landData;
        }
    }
}
