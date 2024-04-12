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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Hypergrid asset service. It serves the IAssetService interface,
    /// but implements it in ways that are appropriate for inter-grid
    /// asset exchanges.
    /// </summary>
    public class HGRemoteAssetService : IAssetService
    {
        private const string _ConfigName = "HGAssetService";
        private string m_HomeURL;
        private IUserAccountService m_UserAccountService;
        private IAssetService m_assetConnector;

        private UserAccountCache m_Cache;

        private AssetPermissions m_AssetPerms;

        private readonly IComponentContext m_context;
        private readonly IConfiguration m_configuration;
        private readonly ILogger<HGRemoteAssetService> m_logger;

        public HGRemoteAssetService(
            IComponentContext componentContext,
            IConfiguration config, 
            ILogger<HGRemoteAssetService> logger
            )
        {
            m_context = componentContext;
            m_configuration = config;
            m_logger = logger;

            m_logger.LogDebug("[HGRemoteAsset Service]: Starting");

            var assetConfig = config.GetSection(_ConfigName);
            if (assetConfig.Exists() is false)
                throw new Exception("No HGAssetService configuration");

            string? assetConnectorService = assetConfig.GetValue("AssetConnector", String.Empty);
            if (string.IsNullOrEmpty(assetConnectorService))
                throw new Exception("Please specify AssetConnector in HGAssetService configuration");

            m_assetConnector = m_context.ResolveNamed<IAssetService>(assetConnectorService);
            if (m_assetConnector == null)
                throw new Exception($"Unable to create AssetConnector from {assetConnectorService}");

            string? userAccountsService = assetConfig.GetValue("UserAccountsService", string.Empty);
            if (string.IsNullOrEmpty(userAccountsService))
                throw new Exception("Please specify UserAccountsService in HGAssetService configuration");

            m_UserAccountService = m_context.ResolveNamed<IUserAccountService>(userAccountsService);
            if (m_UserAccountService == null)
                throw new Exception(String.Format("Unable to create UserAccountService from {0}", userAccountsService));

            var sections = new string[] { "Startup", "Hypergrid", _ConfigName };
            m_HomeURL = Util.GetConfigVarFromSections<string>(config, "HomeURI", sections, string.Empty);
            if (string.IsNullOrEmpty(m_HomeURL))
                throw new Exception("[HGAssetService] No HomeURI specified");

            m_Cache = UserAccountCache.CreateUserAccountCache(m_UserAccountService);

            // Permissions
            m_AssetPerms = new AssetPermissions(assetConfig);
        }

        #region IAssetService overrides
        public AssetBase? Get(string id)
        {
            AssetBase asset = m_assetConnector.Get(id);

            if (asset == null)
                return null;

            if (!m_AssetPerms.AllowedExport(asset.Type))
                return null;

            if (asset.Metadata.Type == (sbyte)AssetType.Object)
                asset.Data = AdjustIdentifiers(asset.Data);

            AdjustIdentifiers(asset.Metadata);

            return asset;
        }

        public AssetBase? Get(string id, string ForeignAssetService, bool dummy)
        {
            return null;
        }

        public AssetMetadata? GetMetadata(string id)
        {
            AssetMetadata meta = m_assetConnector.GetMetadata(id);

            if (meta == null)
                return null;

            AdjustIdentifiers(meta);

            return meta;
        }

        public byte[]? GetData(string id)
        {
            AssetBase? asset = Get(id);

            if (asset == null)
                return null;

            if (!m_AssetPerms.AllowedExport(asset.Type))
                return null;

            // Deal with bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
            // Fix bad assets before sending them elsewhere
            if (asset.Type == (int)AssetType.Object && asset.Data != null)
            {
                string xml = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(asset.Data));
                asset.Data = Utils.StringToBytes(xml);
            }

            return asset.Data;
        }

        // public delegate void AssetRetrieved(string id, Object sender, AssetBase asset);
        public virtual bool Get(string id, Object sender, AssetRetrieved handler)
        {
            return m_assetConnector.Get(id, sender, (i, s, asset) =>
            {
                if (asset != null)
                {
                    if (!m_AssetPerms.AllowedExport(asset.Type))
                    {
                        asset = null;
                    }
                    else
                    {
                        if (asset.Metadata.Type == (sbyte)AssetType.Object)
                            asset.Data = AdjustIdentifiers(asset.Data);

                        AdjustIdentifiers(asset.Metadata);
                    }
                }

                handler(i, s, asset);
            });
        }

        public void Get(string id, string ForeignAssetService, bool StoreOnLocalGrid, SimpleAssetRetrieved callBack)
        {
            m_assetConnector.Get(id, null, (i, s, asset) =>
            {
                if (asset != null)
                {
                    if (!m_AssetPerms.AllowedExport(asset.Type))
                    {
                        asset = null;
                    }
                    else
                    {
                        if (asset.Metadata.Type == (sbyte)AssetType.Object)
                            asset.Data = AdjustIdentifiers(asset.Data);

                        AdjustIdentifiers(asset.Metadata);
                    }
                }

                callBack(asset);
            });
        }

        public string Store(AssetBase asset, bool AllowRetry)
        {
            return Store(asset);
        }

        public string Store(AssetBase asset)
        {
            if (!m_AssetPerms.AllowedImport(asset.Type))
                return string.Empty;

            // Deal with bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
            // Fix bad assets before storing on this server
            if (asset.Type == (int)AssetType.Object && asset.Data != null)
            {
                string xml = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(asset.Data));
                asset.Data = Utils.StringToBytes(xml);
            }

            return m_assetConnector.Store(asset);
        }

        public bool Delete(string id)
        {
            // NOGO
            return false;
        }

        #endregion

        protected void AdjustIdentifiers(AssetMetadata meta)
        {
            if (meta == null || m_Cache == null)
                return;

            UserAccount creator = m_Cache.GetUser(meta.CreatorID);
            if (creator != null)
                meta.CreatorID = meta.CreatorID + ";" + m_HomeURL + "/" + creator.FirstName + " " + creator.LastName;
        }

        // Only for Object
        protected byte[] AdjustIdentifiers(byte[] data)
        {
            string xml = Utils.BytesToString(data);

            // Deal with bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
            // Fix bad assets before sending them elsewhere
            xml = ExternalRepresentationUtils.SanitizeXml(xml);

            return Utils.StringToBytes(ExternalRepresentationUtils.RewriteSOP(xml, "HGAssetService", m_HomeURL, m_Cache, UUID.Zero));
        }

        public AssetBase? GetCached(string id)
        {
            return Get(id);
        }

        public bool[] AssetsExist(string[] ids)
        {
            return m_assetConnector.AssetsExist(ids);
        }

        public bool UpdateContent(string id, byte[] data)
        {
            // SO not happening!!
            return false;
        }
    }

}
