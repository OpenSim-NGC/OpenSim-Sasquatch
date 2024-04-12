using System;
using System.Collections.Generic;

#nullable disable

namespace InventoryService.Models
{
    public partial class Inventoryitem
    {
        public string AssetId { get; set; }
        public int? AssetType { get; set; }
        public string InventoryName { get; set; }
        public string InventoryDescription { get; set; }
        public int? InventoryNextPermissions { get; set; }
        public int? InventoryCurrentPermissions { get; set; }
        public int? InvType { get; set; }
        public string CreatorId { get; set; }
        public int InventoryBasePermissions { get; set; }
        public int InventoryEveryOnePermissions { get; set; }
        public int SalePrice { get; set; }
        public byte SaleType { get; set; }
        public int CreationDate { get; set; }
        public string GroupId { get; set; }
        public byte GroupOwned { get; set; }
        public int Flags { get; set; }
        public string InventoryId { get; set; }
        public string AvatarId { get; set; }
        public string ParentFolderId { get; set; }
        public int InventoryGroupPermissions { get; set; }
    }
}
