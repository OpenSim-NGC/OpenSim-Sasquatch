using System;
using System.Collections.Generic;

#nullable disable

namespace InventoryService.Models
{
    public partial class Inventoryfolder
    {
        public string FolderName { get; set; }
        public short Type { get; set; }
        public int Version { get; set; }
        public string FolderId { get; set; }
        public string AgentId { get; set; }
        public string ParentFolderId { get; set; }
    }
}
