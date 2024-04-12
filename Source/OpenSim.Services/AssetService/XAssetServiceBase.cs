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
using OpenSim.Data;
using OpenSim.Services.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.AssetService
{
    public class XAssetServiceBase
    {
        protected static string _ConfigName = "AssetService";
        protected readonly IXAssetDataPlugin m_Database;
        protected readonly IComponentContext m_context;
        protected readonly IConfiguration m_configuration;
        protected readonly ILogger m_logger;

        protected IAssetLoader? m_AssetLoader;
        protected IAssetService? m_ChainedAssetService;

        public bool HasChainedAssetService { get => m_ChainedAssetService == null; }

        public string ConfigName { get => _ConfigName; }
        public IConfiguration Config { get => m_configuration; }
        public ILogger Logger { get => m_logger; }
        public IXAssetDataPlugin Database { get => m_Database; }

        public XAssetServiceBase(
            IComponentContext componentContext,
            IConfiguration config,
            ILogger logger,
            IXAssetDataPlugin xAssetDataPlugin
            )
        {
            m_context = componentContext;
            m_configuration = config;
            m_logger = logger;
            m_Database = xAssetDataPlugin;

            string? connString = String.Empty;

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists() is true)
            {
                connString = dbConfig.GetValue("ConnectionString", String.Empty);
            }
            
            //
            // Try reading the [AssetService] section first, if it exists
            // we will use that over the general DatabaseService config
            //
            var assetConfig = config.GetSection(ConfigName);
            if (assetConfig.Exists())
            {
                connString = assetConfig.GetValue("ConnectionString", connString);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (string.IsNullOrEmpty(connString))
                throw new Exception("No Storage Configuration provided");

            // Chained Asset Configuration
            string? chainedAssetServiceDesignator = assetConfig.GetValue("ChainedServiceModule", string.Empty);
            if (string.IsNullOrEmpty(chainedAssetServiceDesignator) is false)
            {
                Logger.LogInformation(
                    $"[XASSET SERVICE BASE]: Loading chained asset service from {chainedAssetServiceDesignator}");

                try 
                {
                    m_ChainedAssetService = m_context.ResolveNamed<IAssetService>(chainedAssetServiceDesignator);
                }
                catch (Exception)
                {
                    throw new Exception($"Failed to load ChainedAssetService from {chainedAssetServiceDesignator}");
                }
            }

            m_Database.Initialise(connString);

            if (m_ChainedAssetService != null)
            {
                string? loaderName = assetConfig.GetValue("DefaultAssetLoader", String.Empty);
                if (string.IsNullOrEmpty(loaderName) is false)
                {
                    try
                    {
                        m_AssetLoader = m_context.ResolveNamed<IAssetLoader>(loaderName);
                    }
                    catch (Exception)
                    {
                        throw new Exception("Asset loader could not be loaded");
                    }
                }
            }
        }
    }
}