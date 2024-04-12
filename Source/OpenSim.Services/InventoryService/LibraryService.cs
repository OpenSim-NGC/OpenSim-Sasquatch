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

using System.Xml;

using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;

using PermissionMask = OpenSim.Framework.PermissionMask;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace OpenSim.Services.InventoryService
{
    /// <summary>
    /// Basically a hack to give us a Inventory library while we don't have a inventory server
    /// once the server is fully implemented then should read the data from that
    /// </summary>
    public class LibraryService : ILibraryService
    {
        private static readonly UUID libOwner = Framework.Constants.m_MrOpenSimID;
        private const string m_LibraryRootFolderIDstr = "00000112-000f-0000-0000-000100bba000";
        private static readonly UUID m_LibraryRootFolderID = new UUID(m_LibraryRootFolderIDstr);

        static private InventoryFolderImpl m_LibraryRootFolder;

        public InventoryFolderImpl LibraryRootFolder
        {
            get { return m_LibraryRootFolder; }
        }

        /// <summary>
        /// Holds the root library folder and all its descendents.  This is really only used during inventory
        /// setup so that we don't have to repeatedly search the tree of library folders.
        /// </summary>
        static protected Dictionary<UUID, InventoryFolderImpl> libraryFolders = new(32);
        static protected Dictionary<UUID, InventoryItemBase> m_items = new(256);
        static LibraryService? m_root;
        static object m_rootLock = new object();
        static readonly uint m_BasePermissions = (uint)PermissionMask.AllAndExport;
        static readonly uint m_EveryOnePermissions = (uint)PermissionMask.AllAndExportNoMod;
        static readonly uint m_CurrentPermissions = (uint)PermissionMask.AllAndExport;
        static readonly uint m_NextPermissions = (uint)PermissionMask.AllAndExport;
        static readonly uint m_GroupPermissions = 0;

        protected const string _ConfigName = "LibraryService";

        protected readonly IConfiguration m_configuration;
        protected readonly ILogger<LibraryService> m_logger;

        public LibraryService(
            IConfiguration config,
            ILogger<LibraryService> logger)
        {
            m_configuration = config;
            m_logger = logger;

            lock(m_rootLock)
            {
                if (m_root != null)
                    return;

                m_root = this;
            }

            string? pLibrariesLocation = Path.Combine("inventory", "Libraries.xml");
            string? pLibName = "OpenSim Library";

            var libConfig = m_configuration.GetSection(_ConfigName);
            if (libConfig.Exists())
            {
                pLibrariesLocation = libConfig.GetValue("DefaultLibrary", pLibrariesLocation);
                pLibName = libConfig.GetValue("LibraryName", pLibName);
            }

            m_logger.LogDebug("[LIBRARY]: Starting library service...");

            m_LibraryRootFolder = new InventoryFolderImpl();

            m_LibraryRootFolder.Owner = libOwner;
            m_LibraryRootFolder.ID = m_LibraryRootFolderID;
            m_LibraryRootFolder.Name = pLibName;
            m_LibraryRootFolder.ParentID = UUID.Zero;
            m_LibraryRootFolder.Type = 8;
            m_LibraryRootFolder.Version = 1;
            
            libraryFolders.Add(m_LibraryRootFolder.ID, m_LibraryRootFolder);

            LoadLibraries(pLibrariesLocation);
        }

        public InventoryItemBase CreateItem(UUID inventoryID, UUID assetID, string name, string description,
                                            int assetType, int invType, UUID parentFolderID)
        {
            InventoryItemBase item = new InventoryItemBase();

            item.Owner = libOwner;
            item.CreatorId = libOwner.ToString();
            item.ID = inventoryID;
            item.AssetID = assetID;
            item.Description = description;
            item.Name = name;
            item.AssetType = assetType;
            item.InvType = invType;
            item.Folder = parentFolderID;
            item.BasePermissions = m_BasePermissions;
            item.EveryOnePermissions = m_EveryOnePermissions;
            item.CurrentPermissions = m_CurrentPermissions;
            item.NextPermissions = m_NextPermissions;
            item.GroupPermissions = m_GroupPermissions;

            return item;
        }

        /// <summary>
        /// Use the asset set information at path to load assets
        /// </summary>
        /// <param name="path"></param>
        /// <param name="assets"></param>
        protected void LoadLibraries(string? librariesControlPath)
        {
            if (string.IsNullOrEmpty(librariesControlPath) is false)
            {
                m_logger.LogInformation($"[LIBRARY INVENTORY]: Loading library control file {librariesControlPath}");
                LoadFromFile(librariesControlPath, "Libraries control", ReadLibraryFromConfig);
            }
            else
            {
                throw new Exception("LoadLibraries: librariesControlPath is null or uninitialized");
            }
        }

        /// <summary>
        /// Read a library set from config
        /// </summary>
        /// <param name="config"></param>
        protected void ReadLibraryFromConfig(IConfiguration config, string path)
        {
            string? basePath = Path.GetDirectoryName(path);
            // XXX MCD
            // if (config.Contains("RootVersion"))
            // {
            //     m_LibraryRootFolder.Version = config.GetValue<ushort>("RootVersion", m_LibraryRootFolder.Version);
            //     return;
            // }

            if (basePath is not null)
            {
                string? foldersFile = config.GetValue("foldersFile", String.Empty);
                if (foldersFile is not null)
                {
                    string foldersPath = Path.Combine(basePath, foldersFile);
                    LoadFromFile(foldersPath, "Library folders", ReadFolderFromConfig);
                }

                string? itemsFile = config.GetValue("itemsFile", String.Empty);
                if (itemsFile is not null)
                {
                    string itemsPath = Path.Combine( basePath, itemsFile);
                    LoadFromFile(itemsPath, "Library items", ReadItemFromConfig);
                }
            }
        }

        /// <summary>
        /// Read a library inventory folder from a loaded configuration
        /// </summary>
        /// <param name="source"></param>
        private void ReadFolderFromConfig(IConfiguration config, string path)
        {
            InventoryFolderImpl folderInfo = new InventoryFolderImpl();

            folderInfo.ID = new UUID(config.GetValue("folderID", m_LibraryRootFolderIDstr));
            folderInfo.Name = config.GetValue("name", "unknown");
            folderInfo.ParentID = new UUID(config.GetValue("parentFolderID", m_LibraryRootFolderIDstr));
            folderInfo.Type = config.GetValue<short>("type", 8);
            folderInfo.Version = config.GetValue<ushort>("version", 1);
            folderInfo.Owner = libOwner;

            if (libraryFolders.TryGetValue(folderInfo.ParentID, out InventoryFolderImpl? parentFolder))
            {
                libraryFolders.Add(folderInfo.ID, folderInfo);
                parentFolder.AddChildFolder(folderInfo);

                m_logger.LogDebug($"[LIBRARY INVENTORY]: Adding folder {folderInfo.Name} ({folderInfo.ID})");
            }
            else
            {
                m_logger.LogWarning(
                    $"[LIBRARY INVENTORY]: Couldn't add folder {folderInfo.Name} ({folderInfo.ID}) " +
                    $"since parent folder with ID {folderInfo.ParentID} does not exist!");
            }
        }

        /// <summary>
        /// Read a library inventory item metadata from a loaded configuration
        /// </summary>
        /// <param name="source"></param>
        private void ReadItemFromConfig(IConfiguration config, string path)
        {
            InventoryItemBase item = new InventoryItemBase();
            
            item.Owner = libOwner;
            item.CreatorId = libOwner.ToString();
            UUID itID = new UUID(config.GetValue("inventoryID", m_LibraryRootFolderIDstr));
            item.ID = itID; 
            item.AssetID = new UUID(config.GetValue("assetID", item.ID.ToString()));
            item.Folder = new UUID(config.GetValue("folderID", m_LibraryRootFolderIDstr));
            item.Name = config.GetValue("name", String.Empty);
            item.Description = config.GetValue("description", item.Name);
            item.InvType = config.GetValue<int>("inventoryType", 0);
            item.AssetType = config.GetValue<int>("assetType", item.InvType);
            item.CurrentPermissions = config.GetValue<uint>("currentPermissions", m_CurrentPermissions);
            item.NextPermissions = (uint)config.GetValue<uint>("nextPermissions", m_NextPermissions);
            item.EveryOnePermissions = config.GetValue<uint>("everyonePermissions", m_EveryOnePermissions);
            item.BasePermissions = config.GetValue<uint>("basePermissions", m_BasePermissions);
            item.GroupPermissions = config.GetValue<uint>("basePermissions", m_GroupPermissions);
            item.Flags = config.GetValue<uint>("flags", 0);

            if (libraryFolders.TryGetValue(item.Folder, out InventoryFolderImpl? parentFolder))
            {
                if(!parentFolder.Items.ContainsKey(itID))
                {
                    parentFolder.Items.Add(itID, item);
                    m_items[itID] = item;
                }
                else
                {
                    m_logger.LogWarning($"[LIBRARY INVENTORY] Item {item.Name} [{item.ID}] not added, duplicate item");
                }
            }
            else
            {
                m_logger.LogWarning(
                    $"[LIBRARY INVENTORY]: Couldn't add item {item.Name} ({item.ID}) " +
                    $"since parent folder with ID {item.Folder} does not exist!");
            }
        }

        private delegate void ConfigAction(IConfiguration config, string path);

        /// <summary>
        /// Load the given configuration at a path and perform an action on each Config contained within it
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileDescription"></param>
        /// <param name="action"></param>
        private void LoadFromFile(string path, string fileDescription, ConfigAction action)
        {
            if (File.Exists(path))
            {
                try
                {
                    IConfigurationBuilder builder = new ConfigurationBuilder().AddXmlFile(path, optional: false, reloadOnChange: true);
                    IConfigurationRoot root = builder.Build();

                    foreach (var source in root.GetChildren())
                    {
                        action(source, path);
                    }

                    //XmlConfigSource source = new XmlConfigSource(path);
                    // for (int i = 0; i < source.Configs.Count; i++)
                    // {
                    //     action(source.Configs[i], path);
                    // }
                }
                catch (XmlException e)
                {
                    m_logger.LogError(e, $"[LIBRARY INVENTORY]: Error loading {path}");
                }
            }
            else
            {
                m_logger.LogError($"[LIBRARY INVENTORY]: {fileDescription} file {path} does not exist!");
            }
        }

        /// <summary>
        /// Looks like a simple getter, but is written like this for some consistency with the other Request
        /// methods in the superclass
        /// </summary>
        /// <returns></returns>
        public Dictionary<UUID, InventoryFolderImpl> GetAllFolders()
        {
            Dictionary<UUID, InventoryFolderImpl> fs = new Dictionary<UUID, InventoryFolderImpl>();
            fs.Add(m_LibraryRootFolderID, m_LibraryRootFolder);
            List<InventoryFolderImpl> fis = TraverseFolder(m_LibraryRootFolder);
            foreach (InventoryFolderImpl f in fis)
            {
                fs.Add(f.ID, f);
            }
            //return libraryFolders;
            return fs;
        }

        private List<InventoryFolderImpl> TraverseFolder(InventoryFolderImpl node)
        {
            List<InventoryFolderImpl> folders = node.RequestListOfFolderImpls();
            List<InventoryFolderImpl> subs = new List<InventoryFolderImpl>();
            foreach (InventoryFolderImpl f in folders)
                subs.AddRange(TraverseFolder(f));

            folders.AddRange(subs);
            return folders;
        }

        public InventoryItemBase? GetItem(UUID itemID)
        {
            if(m_items.TryGetValue(itemID, out InventoryItemBase? it))
                return it;
            return null;
        }

        public InventoryItemBase[]? GetMultipleItems(UUID[] ids)
        {
            List<InventoryItemBase> items = new(ids.Length);
            foreach (UUID id in ids.AsSpan())
            {
                if (m_items.TryGetValue(id, out InventoryItemBase? it))
                    items.Add(it);
            }

            if(items.Count == 0)
                return null;
            return items.ToArray();
        }
    }
}
