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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenSim.Framework;
using OpenSim.Services.Connectors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Neighbour
{
    public class NeighbourServicesOutConnector :
            NeighbourServicesConnector, ISharedRegionModule, INeighbourService
    {
        private static ILogger m_logger;

        private List<Scene> m_Scenes = new List<Scene>();
        private bool m_Enabled = false;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "NeighbourServicesOutConnector"; }
        }

        public void Initialise(IConfiguration source)
        {
            m_logger ??= OpenSimServer.Instance.ServiceProvider.GetRequiredService<ILogger<NeighbourServicesOutConnector>>();
            IConfigurationSection moduleConfig = source.GetSection("Modules");
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetValue<string>("NeighbourServices");
                if (!String.IsNullOrEmpty(name) && name == Name)
                {
                    m_Enabled = true;
                    m_logger?.LogInformation("[NEIGHBOUR CONNECTOR]: Neighbour out connector enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenes.Add(scene);
            scene.RegisterModuleInterface<INeighbourService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            // Always remove
            if (m_Scenes.Contains(scene))
                m_Scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_GridService = scene.GridService;
            m_logger?.LogInformation("[NEIGHBOUR CONNECTOR]: Enabled out neighbours for region {0}", scene.RegionInfo.RegionName);

        }

        #region INeighbourService

        public override GridRegion HelloNeighbour(ulong regionHandle, RegionInfo thisRegion)
        {
            if (!m_Enabled)
                return null;

            foreach (Scene s in m_Scenes)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //uint x, y;
                    //Util.RegionHandleToRegionLoc(regionHandle, out x, out y);
                    //m_log.DebugFormat("[NEIGHBOUR SERVICE OUT CONNECTOR]: HelloNeighbour from region {0} to neighbour {1} at {2}-{3}",
                    //                            thisRegion.RegionName, s.Name, x, y );
                    return s.IncomingHelloNeighbour(thisRegion);
                }
            }

            return base.HelloNeighbour(regionHandle, thisRegion);
        }

        #endregion INeighbourService
    }
}