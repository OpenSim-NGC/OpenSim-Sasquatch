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

using OpenMetaverse;

using OpenSim.Services.Interfaces;
using OpenSim.Services.InventoryService;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Server.Base;

using Autofac;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Hypergrid inventory service. It serves the IInventoryService interface,
    /// but implements it in ways that are appropriate for inter-grid
    /// inventory exchanges. Specifically, it does not performs deletions
    /// and it responds to GetRootFolder requests with the ID of the
    /// Suitcase folder, not the actual "My Inventory" folder.
    /// </summary>
    public class HGSuitcaseInventoryService : XInventoryService, IInventoryService
    {
        private IUserAccountService m_UserAccountService;
        private IAvatarService m_AvatarService;

        private ExpiringCache<UUID, List<XInventoryFolder>> m_SuitcaseTrees = new ExpiringCache<UUID, List<XInventoryFolder>>();
        private ExpiringCache<UUID, AvatarAppearance> m_Appearances = new ExpiringCache<UUID, AvatarAppearance>();

        public HGSuitcaseInventoryService(
            IComponentContext componentContext,
            IConfiguration config, 
            ILogger<HGSuitcaseInventoryService> logger)
            : base(componentContext, config, logger)
        {
            m_logger.LogDebug($"[HG SUITCASE INVENTORY SERVICE]: Starting with config name {_ConfigName}");

            if (m_Database == null)
                m_logger.LogError("[HG SUITCASE INVENTORY SERVICE]: m_Database is null!");

            //
            // Try reading the [InventoryService] section, if it exists
            //
            var invConfig = config.GetSection(_ConfigName);
            if (invConfig.Exists() is true)
            {
                string? userAccountsService = invConfig.GetValue("UserAccountsService", string.Empty);
                if (string.IsNullOrEmpty(userAccountsService) is true)
                    throw new Exception("Please specify UserAccountsService in HGInventoryService configuration");

                m_UserAccountService = m_context.ResolveNamed<IUserAccountService>(userAccountsService);
                if (m_UserAccountService == null)
                    throw new Exception($"Unable to create UserAccountService from {userAccountsService}");

                string? avatarService = invConfig.GetValue("AvatarService", string.Empty);
                if (string.IsNullOrEmpty(avatarService) is true)
                    throw new Exception("Please specify AvatarService in HGInventoryService configuration");

                m_AvatarService = m_context.ResolveNamed<IAvatarService>(avatarService);
                if (m_AvatarService == null)
                    throw new Exception($"Unable to create m_AvatarService from {avatarService}");

//                m_HomeURL = Util.GetConfigVarFromSections<string>(config, "HomeURI",
//                    new string[] { "Startup", "Hypergrid", m_ConfigName }, String.Empty);

//                m_Cache = UserAccountCache.CreateUserAccountCache(m_UserAccountService);
            }

            m_logger.LogDebug("[HG SUITCASE INVENTORY SERVICE]: Starting...");
        }

        public override bool CreateUserInventory(UUID principalID)
        {
            // NOGO
            return false;
        }

        public override List<InventoryFolderBase> GetInventorySkeleton(UUID principalID)
        {
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);

            if (suitcase == null)
            {
                m_logger.LogWarning($"[HG SUITCASE INVENTORY SERVICE]: Found no suitcase folder for user {principalID} when looking for inventory skeleton");
                return null;
            }

            List<XInventoryFolder> tree = GetFolderTree(principalID, suitcase.folderID);
            if (tree.Count == 0)
                return null;

            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            foreach (XInventoryFolder x in tree)
            {
                folders.Add(ConvertToOpenSim(x));
            }

            SetAsNormalFolder(suitcase);
            folders.Add(ConvertToOpenSim(suitcase));

            return folders;
        }

        public override InventoryFolderBase GetRootFolder(UUID principalID)
        {
            m_logger.LogDebug($"[HG SUITCASE INVENTORY SERVICE]: GetRootFolder for {principalID}");

            // Let's find out the local root folder
            XInventoryFolder root = GetRootXFolder(principalID);

            if (root == null)
            {
                m_logger.LogWarning($"[HG SUITCASE INVENTORY SERVICE]: Unable to retrieve local root folder for user {principalID}");
                return null;
            }

            // Warp! Root folder for travelers is the suitcase folder
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);

            if (suitcase == null)
            {
                m_logger.LogDebug($"[HG SUITCASE INVENTORY SERVICE]: User {principalID} does not have a Suitcase folder. Creating it...");

                // Create the My Suitcase folder under the user's root folder.
                // In the DB we tag it as type 100, but we use type 8 (Folder) outside, as this affects the sort order.
                suitcase = CreateFolder(principalID, root.folderID, (int)FolderType.Suitcase, InventoryFolderBase.SUITCASE_FOLDER_NAME);
                if (suitcase == null)
                {
                    m_logger.LogError("[HG SUITCASE INVENTORY SERVICE]: Unable to create suitcase folder");
                    return null;
                }

                CreateSystemFolders(principalID, suitcase.folderID);
            }

            SetAsNormalFolder(suitcase);

            return ConvertToOpenSim(suitcase);
        }

        protected void CreateSystemFolders(UUID principalID, UUID rootID)
        {
            m_logger.LogDebug("[HG SUITCASE INVENTORY SERVICE]: Creating System folders under Suitcase...");

            XInventoryFolder[] sysFolders = GetSystemFolders(principalID, rootID);

            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Animation) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Animation, "Animations");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.BodyPart) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.BodyPart, "Body Parts");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.CallingCard) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.CallingCard, "Calling Cards");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Clothing) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Clothing, "Clothing");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.CurrentOutfit) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.CurrentOutfit, "Current Outfit");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Favorites) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Favorites, "Favorites");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Gesture) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Gesture, "Gestures");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Landmark) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Landmark, "Landmarks");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.LostAndFound) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.LostAndFound, "Lost And Found");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Notecard) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Notecard, "Notecards");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Object) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Object, "Objects");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Snapshot) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Snapshot, "Photo Album");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.LSLText) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.LSLText, "Scripts");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Sound) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Sound, "Sounds");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Texture) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Texture, "Textures");
            if (!Array.Exists(sysFolders, delegate(XInventoryFolder f) { if (f.type == (int)FolderType.Trash) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Trash, "Trash");
            if (!Array.Exists(sysFolders, delegate (XInventoryFolder f) { if (f.type == (int)FolderType.Settings) return true; return false; }))
                CreateFolder(principalID, rootID, (int)FolderType.Settings, "Settings");
        }

        public override InventoryFolderBase GetFolderForType(UUID principalID, FolderType type)
        {
            m_logger.LogDebug($"[HG INVENTORY SERVICE]: GetFolderForType for {principalID} {type}");
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);

            if (suitcase == null)
            {
                m_logger.LogWarning($"[HG SUITCASE INVENTORY SERVICE]: Found no suitcase folder for user {principalID} when looking for child type folder {type}");
                return null;
            }

            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "type", "parentFolderID" },
                    new string[] { principalID.ToString(), ((int)type).ToString(), suitcase.folderID.ToString() });

            if (folders.Length == 0)
            {
                m_logger.LogWarning($"[HG SUITCASE INVENTORY SERVICE]: Found no folder for type {type} for user {principalID}");
                return null;
            }

            m_logger.LogDebug(
                $"[HG SUITCASE INVENTORY SERVICE]: Found folder {folders[0].folderName} {folders[0].folderID} " +
                $"for type {type} for user {principalID}");

            return ConvertToOpenSim(folders[0]);
        }

        public override InventoryCollection GetFolderContent(UUID principalID, UUID folderID)
        {
            InventoryCollection coll = null;

            if (!IsWithinSuitcaseTree(principalID, folderID))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: GetFolderContent: folder {folderID} " +
                    $"(user {principalID}) is not within Suitcase tree");

                return new InventoryCollection();
            }

            coll = base.GetFolderContent(principalID, folderID);

            if (coll == null)
            {
                m_logger.LogWarning("[HG SUITCASE INVENTORY SERVICE]: Something wrong with user {0}'s suitcase folder", principalID);
                coll = new InventoryCollection();
            }
            return coll;
        }

        public override List<InventoryItemBase> GetFolderItems(UUID principalID, UUID folderID)
        {
            // Let's do a bit of sanity checking, more than the base service does
            // make sure the given folder exists under the suitcase tree of this user
            if (!IsWithinSuitcaseTree(principalID, folderID))
            {
                m_logger.LogDebug(
                $"[HG SUITCASE INVENTORY SERVICE]: GetFolderItems: folder {folderID} " +
                $"(user {principalID}) is not within Suitcase tree");

                return new List<InventoryItemBase>();
            }

            return base.GetFolderItems(principalID, folderID);
        }

        public override bool AddFolder(InventoryFolderBase folder)
        {
            //m_log.WarnFormat("[HG SUITCASE INVENTORY SERVICE]: AddFolder {0} {1}", folder.Name, folder.ParentID);
            // Let's do a bit of sanity checking, more than the base service does
            // make sure the given folder's parent folder exists under the suitcase tree of this user

            if (!IsWithinSuitcaseTree(folder.Owner, folder.ParentID))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: AddFolder: folder {folder.ParentID} " +
                    $"(user {folder.Owner}) is not within Suitcase tree");

                return false;
            }

            // OK, it's legit
            if (base.AddFolder(folder))
            {
                List<XInventoryFolder> tree;
                if (m_SuitcaseTrees.TryGetValue(folder.Owner, out tree))
                    tree.Add(ConvertFromOpenSim(folder));

                return true;
            }

            return false;
        }

        public override bool UpdateFolder(InventoryFolderBase folder)
        {
            //m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: Update folder {0}, version {1}", folder.ID, folder.Version);
            if (!IsWithinSuitcaseTree(folder.Owner, folder.ID))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: UpdateFolder: folder {folder.Name}/{folder.ID} " +
                    $"(user {folder.Owner}) is not within Suitcase tree");
                return false;

            }

            // For all others
            return base.UpdateFolder(folder);
        }

        public override bool MoveFolder(InventoryFolderBase folder)
        {
            if (!IsWithinSuitcaseTree(folder.Owner, folder.ID))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: MoveFolder: folder {folder.ID} "+
                    $"(user {folder.Owner}) is not within Suitcase tree");

                return false;
            }

            if (!IsWithinSuitcaseTree(folder.Owner, folder.ParentID))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: MoveFolder: folder {folder.ParentID} " +
                    $"(user {folder.Owner}) is not within Suitcase tree");

                return false;
            }

            return base.MoveFolder(folder);
        }

        public override bool DeleteFolders(UUID principalID, List<UUID> folderIDs)
        {
            // NOGO
            return false;
        }

        public override bool PurgeFolder(InventoryFolderBase folder)
        {
            // NOGO
            return false;
        }

        public override bool AddItem(InventoryItemBase item)
        {
            // Let's do a bit of sanity checking, more than the base service does
            // make sure the given folder's parent folder exists under the suitcase tree of this user
            if (!IsWithinSuitcaseTree(item.Owner, item.Folder))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: AddItem: folder {item.Folder} " +
                    $"(user {item.Owner}) is not within Suitcase tree");

                return false;
            }

            // OK, it's legit
            return base.AddItem(item);

        }

        public override bool UpdateItem(InventoryItemBase item)
        {
            if (!IsWithinSuitcaseTree(item.Owner, item.Folder))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: UpdateItem: folder {item.Folder} " +
                    $"(user {item.Owner}) is not within Suitcase tree");

                return false;
            }

            return base.UpdateItem(item);
        }

        public override bool MoveItems(UUID principalID, List<InventoryItemBase> items)
        {
            // Principal is b0rked. *sigh*

            // Check the items' destination folders
            foreach (InventoryItemBase item in items)
            {
                if (!IsWithinSuitcaseTree(item.Owner, item.Folder))
                {
                    m_logger.LogDebug(
                        $"[HG SUITCASE INVENTORY SERVICE]: MoveItems: folder {item.Folder} " +
                        $"(user {item.Owner}) is not within Suitcase tree");

                    return false;
                }
            }

            // Check the items' current folders
            foreach (InventoryItemBase item in items)
            {
                InventoryItemBase originalItem = base.GetItem(item.Owner, item.ID);
                if (!IsWithinSuitcaseTree(originalItem.Owner, originalItem.Folder))
                {
                    m_logger.LogDebug(
                        $"[HG SUITCASE INVENTORY SERVICE]: MoveItems: folder {item.Folder} " +
                        $"(user {item.Owner}) is not within Suitcase tree");

                    return false;
                }
            }

            return base.MoveItems(principalID, items);
        }

        public override bool DeleteItems(UUID principalID, List<UUID> itemIDs)
        {
            return false;
        }

        public override InventoryItemBase GetItem(UUID principalID, UUID itemID)
        {
            InventoryItemBase? it = base.GetItem(principalID, itemID);
            if (it == null)
            {
                m_logger.LogDebug($"[HG SUITCASE INVENTORY SERVICE]: Unable to retrieve item {itemID}");
                return null;
            }

            if (!IsWithinSuitcaseTree(it.Owner, it.Folder) && !IsPartOfAppearance(it.Owner, it.ID))
            {
                m_logger.LogDebug(
                    $"[HG SUITCASE INVENTORY SERVICE]: GetItem: item {it.Name}/{it.ID} " +
                    $"(folder {it.Folder}) (user {it.Owner}) is not within Suitcase tree or Appearance");

                return null;
            }

            //    UserAccount user = m_Cache.GetUser(it.CreatorId);

            //    // Adjust the creator data
            //    if (user != null && it != null && (it.CreatorData == null || it.CreatorData == string.Empty))
            //        it.CreatorData = m_HomeURL + ";" + user.FirstName + " " + user.LastName;
            //}

            return it;
        }

        public new InventoryFolderBase GetFolder(UUID principalID, UUID folderID)
        {
            InventoryFolderBase? f = base.GetFolder(principalID, folderID);

            if (f != null)
            {
                if (!IsWithinSuitcaseTree(f.Owner, f.ID))
                {
                    m_logger.LogDebug(
                        $"[HG SUITCASE INVENTORY SERVICE]: GetFolder: folder {f.Name}/{f.ID} " +
                        $"(user {f.Owner}) is not within Suitcase tree");

                    return null;
                }
            }

            return f;
        }

        //public List<InventoryItemBase> GetActiveGestures(UUID principalID)
        //{
        //}

        //public int GetAssetPermissions(UUID principalID, UUID assetID)
        //{
        //}

        #region Auxiliary functions
        private XInventoryFolder GetXFolder(UUID userID, UUID folderID)
        {
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "folderID" },
                    new string[] { userID.ToString(), folderID.ToString() });

            if (folders.Length == 0)
                return null;

            return folders[0];
        }

        private XInventoryFolder GetRootXFolder(UUID principalID)
        {
            XInventoryFolder[] folders = m_Database.GetFolders(
                new string[] { "agentID", "folderName", "type" },
                new string[] { principalID.ToString(), InventoryFolderBase.ROOT_FOLDER_NAME, ((int)FolderType.Root).ToString() });

            if (folders != null && folders.Length > 0)
                return folders[0];

            // OK, so the RootFolder type didn't work. Let's look for any type with parent UUID.Zero.
            folders = m_Database.GetFolders(
                new string[] { "agentID", "folderName", "parentFolderID" },
                new string[] { principalID.ToString(), InventoryFolderBase.ROOT_FOLDER_NAME, UUID.Zero.ToString() });

            if (folders != null && folders.Length > 0)
                return folders[0];

            return null;
        }

        private XInventoryFolder GetCurrentOutfitXFolder(UUID userID)
        {
            XInventoryFolder root = GetRootXFolder(userID);
            if (root == null)
                return null;

            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "type", "parentFolderID" },
                    new string[] { userID.ToString(), ((int)FolderType.CurrentOutfit).ToString(), root.folderID.ToString() });

            if (folders.Length == 0)
                return null;

            return folders[0];
        }

        private XInventoryFolder GetSuitcaseXFolder(UUID principalID)
        {
            // Warp! Root folder for travelers
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "agentID", "type" },
                    new string[] { principalID.ToString(), ((int)FolderType.Suitcase).ToString() });

            if (folders != null && folders.Length > 0)
                return folders[0];

            // check to see if we have the old Suitcase folder
            folders = m_Database.GetFolders(
                    new string[] { "agentID", "folderName", "parentFolderID" },
                    new string[] { principalID.ToString(), InventoryFolderBase.SUITCASE_FOLDER_NAME, UUID.Zero.ToString() });
            if (folders != null && folders.Length > 0)
            {
                // Move it to under the root folder
                XInventoryFolder root = GetRootXFolder(principalID);
                folders[0].parentFolderID = root.folderID;
                folders[0].type = (int)FolderType.Suitcase;
                m_Database.StoreFolder(folders[0]);
                return folders[0];
            }

            return null;
        }

        private void SetAsNormalFolder(XInventoryFolder suitcase)
        {
            //suitcase.type = InventoryItemBase.SUITCASE_FOLDER_FAKE_TYPE;
        }

        private List<XInventoryFolder> GetFolderTree(UUID principalID, UUID folder)
        {
            List<XInventoryFolder> t;
            if (m_SuitcaseTrees.TryGetValue(principalID, out t))
                return t;

            // Get the tree of the suitcase folder
            t = GetFolderTreeRecursive(folder);
            m_SuitcaseTrees.AddOrUpdate(principalID, t, 5*60); // 5 minutes
            return t;
        }

        private List<XInventoryFolder> GetFolderTreeRecursive(UUID root)
        {
            List<XInventoryFolder> tree = new List<XInventoryFolder>();
            XInventoryFolder[] folders = m_Database.GetFolders(
                    new string[] { "parentFolderID" },
                    new string[] { root.ToString() });

            if (folders == null || folders.Length == 0)
            {
                return tree; // empty tree
            }
            else
            {
                foreach (XInventoryFolder f in folders)
                {
                    tree.Add(f);
                    tree.AddRange(GetFolderTreeRecursive(f.folderID));
                }
                return tree;
            }

        }

        /// <summary>
        /// Return true if the folderID is a subfolder of the Suitcase or the suitcase folder  itself
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="root"></param>
        /// <param name="suitcase"></param>
        /// <returns></returns>
        private bool IsWithinSuitcaseTree(UUID principalID, UUID folderID)
        {
            XInventoryFolder suitcase = GetSuitcaseXFolder(principalID);

            if (suitcase == null)
            {
                m_logger.LogWarning($"[HG SUITCASE INVENTORY SERVICE]: User {principalID} does not have a Suitcase folder");
                
                return false;
            }

            List<XInventoryFolder> tree = new List<XInventoryFolder>();
            tree.Add(suitcase); // Warp! the tree is the real root folder plus the children of the suitcase folder
            tree.AddRange(GetFolderTree(principalID, suitcase.folderID));

            // Also add the Current Outfit folder to the list of available folders
            XInventoryFolder folder = GetCurrentOutfitXFolder(principalID);
            if (folder != null)
                tree.Add(folder);

            XInventoryFolder f = tree.Find(delegate(XInventoryFolder fl) { return (fl.folderID == folderID); });

            return (f != null);
        }
        #endregion

        #region Avatar Appearance

        private AvatarAppearance GetAppearance(UUID principalID)
        {
            AvatarAppearance a = null;
            if (m_Appearances.TryGetValue(principalID, out a))
                return a;

            a = m_AvatarService.GetAppearance(principalID);
            m_Appearances.AddOrUpdate(principalID, a, 5 * 60); // 5minutes
            return a;
        }

        private bool IsPartOfAppearance(UUID principalID, UUID itemID)
        {
            AvatarAppearance a = GetAppearance(principalID);

            if (a == null)
                return false;

            // Check wearables (body parts and clothes)
            for (int i = 0; i < a.Wearables.Length; i++)
            {
                for (int j = 0; j < a.Wearables[i].Count; j++)
                {
                    if (a.Wearables[i][j].ItemID == itemID)
                    {
                        //m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: item {0} is a wearable", itemID);
                        return true;
                    }
                }
            }

            // Check attachments
            if (a.GetAttachmentForItem(itemID) != null)
            {
                //m_log.DebugFormat("[HG SUITCASE INVENTORY SERVICE]: item {0} is an attachment", itemID);
                return true;
            }

            return false;
        }

        #endregion

    }

}
