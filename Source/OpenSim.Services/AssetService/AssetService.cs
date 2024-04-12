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
using Autofac;

namespace OpenSim.Services.AssetService
{
    public class AssetService : IAssetService
    {
        static readonly string _ConfigName = "AssetService";

        protected IAssetLoader? m_AssetLoader = null;

        protected readonly IConfiguration m_configuration;
        protected readonly ILogger<AssetService> m_logger;
        protected readonly IComponentContext m_context;
        protected readonly IAssetDataPlugin m_Database;

        protected static AssetService? m_RootInstance;

        public AssetService(
            IConfiguration config, 
            ILogger<AssetService> logger,
            IComponentContext componentContext,
            IAssetDataPlugin assetDataPlugin
            )
        {
            m_configuration = config;
            m_logger = logger;
            m_context = componentContext;
            m_Database = assetDataPlugin;

            string? connString = String.Empty;
            string? realm = string.Empty;
            string? loaderName = string.Empty;
            string? loaderArgs = string.Empty;
            bool assetLoaderEnabled = true;
            
            //
            // Try reading the [DatabaseService] section, if it exists
            //
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
                realm = dbConfig.GetValue("Realm", String.Empty);
            }

            //
            // Try reading the [AssetService] section, if it exists
            // Replace the defaults from the database section if defined here
            //
            var assetConfig = m_configuration.GetSection(_ConfigName);
            if (assetConfig.Exists() is true)
            {
                connString = assetConfig.GetValue("ConnectionString", connString);
                realm = assetConfig.GetValue("Realm", realm);
                loaderName = assetConfig.GetValue("DefaultAssetLoader", String.Empty);
                loaderArgs = assetConfig.GetValue("AssetLoaderArgs", String.Empty);
                assetLoaderEnabled = assetConfig.GetValue<bool>("AssetLoaderEnabled", true);
            }

            m_Database.Initialise(connString);

            if (string.IsNullOrEmpty(loaderName) is false)
            {
                var serviceName = loaderName.Split(":")[1];
                m_AssetLoader = m_context.ResolveNamed<IAssetLoader>(serviceName);
                if (m_AssetLoader == null)
                {
                    throw new Exception($"Asset loader could not be loaded for {serviceName}");
                }
            }

            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                if (m_AssetLoader != null)
                {
                    if (assetLoaderEnabled)
                    {
                        m_logger.LogDebug($"[ASSET SERVICE]: Loading default asset set from {loaderArgs}");

                        m_AssetLoader.ForEachDefaultXmlAsset(
                            loaderArgs,
                            delegate(AssetBase a)
                            {
                                AssetBase? existingAsset = Get(a.ID);
//                                AssetMetadata existingMetadata = GetMetadata(a.ID);

                                if (existingAsset == null || Util.SHA1Hash(existingAsset.Data) != Util.SHA1Hash(a.Data))
                                {
//                                    m_log.DebugFormat("[ASSET]: Storing {0} {1}", a.Name, a.ID);
                                    m_Database.StoreAsset(a);
                                }
                            });
                    }

                    m_logger.LogDebug("[ASSET SERVICE]: Local asset service enabled");
                }
            }
        }

        public virtual AssetBase? Get(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset for {0}", id);

            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
            {
                m_logger.LogWarning($"[ASSET SERVICE]: Could not parse requested asset id {id}");
                return null;
            }

            try
            {
                return m_Database.GetAsset(assetID);
            }
            catch (Exception e)
            {
                m_logger.LogError(e, $"[ASSET SERVICE]: Exception getting asset {assetID}");
                return null;
            }
        }

        public virtual AssetBase? Get(string id, string ForeignAssetService, bool dummy)
        {
            return null;
        }

        public virtual AssetBase? GetCached(string id)
        {
            return Get(id);
        }

        public virtual AssetMetadata? GetMetadata(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset metadata for {0}", id);

            AssetBase? asset = Get(id);

            if (asset != null)
                return asset.Metadata;
            else
                return null;
        }

        public virtual byte[]? GetData(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset data for {0}", id);

            AssetBase? asset = Get(id);

            if (asset != null)
                return asset.Data;
            else
                return null;
        }

        public virtual bool Get(string id, Object sender, AssetRetrieved handler)
        {
            //m_log.DebugFormat("[AssetService]: Get asset async {0}", id);

            handler(id, sender, Get(id));

            return true;
        }

        public virtual bool[] AssetsExist(string[] ids)
        {
            try
            {
                UUID[] uuid = Array.ConvertAll(ids, id => UUID.Parse(id));
                return m_Database.AssetsExist(uuid);
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "[ASSET SERVICE]: Exception getting assets");
                return new bool[ids.Length];
            }
        }

        public virtual string Store(AssetBase asset)
        {
            bool exists = m_Database.AssetsExist(new[] { asset.FullID })[0];
            if (!exists)
            {
//                m_log.DebugFormat(
//                    "[ASSET SERVICE]: Storing asset {0} {1}, bytes {2}", asset.Name, asset.FullID, asset.Data.Length);
               if (!m_Database.StoreAsset(asset))
                {
                return UUID.Zero.ToString();
                }
            }
//            else
//            {
//                m_log.DebugFormat(
//                    "[ASSET SERVICE]: Not storing asset {0} {1}, bytes {2} as it already exists", asset.Name, asset.FullID, asset.Data.Length);
//            }

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public virtual bool Delete(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Deleting asset {0}", id);

            UUID assetID;
            if (!UUID.TryParse(id, out assetID))
                return false;

            return m_Database.Delete(id);
        }

        public void Get(string id, string ForeignAssetService, bool StoreOnLocalGrid, SimpleAssetRetrieved callBack)
        {
        }
    }
}
