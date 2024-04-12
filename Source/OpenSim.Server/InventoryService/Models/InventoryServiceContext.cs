using System;
using System.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace InventoryService.Models
{
    public partial class InventoryServiceContext : DbContext
    {
        public InventoryServiceContext()
        {
        }

        public InventoryServiceContext(DbContextOptions<InventoryServiceContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Inventoryfolder> Inventoryfolders { get; set; }
        public virtual DbSet<Inventoryitem> Inventoryitems { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = ConfigurationManager.ConnectionStrings["OpenSimDatabase"].ConnectionString;
                optionsBuilder.UseMySQL(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Inventoryfolder>(entity =>
            {
                entity.HasKey(e => e.FolderId)
                    .HasName("PRIMARY");

                entity.ToTable("inventoryfolders");

                entity.HasIndex(e => e.AgentId, "inventoryfolders_agentid");

                entity.HasIndex(e => e.ParentFolderId, "inventoryfolders_parentFolderid");

                entity.Property(e => e.FolderId)
                    .HasMaxLength(36)
                    .HasColumnName("folderID")
                    .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                    .IsFixedLength(true);

                entity.Property(e => e.AgentId)
                    .HasMaxLength(36)
                    .HasColumnName("agentID")
                    .IsFixedLength(true);

                entity.Property(e => e.FolderName)
                    .HasMaxLength(64)
                    .HasColumnName("folderName");

                entity.Property(e => e.ParentFolderId)
                    .HasMaxLength(36)
                    .HasColumnName("parentFolderID")
                    .IsFixedLength(true);

                entity.Property(e => e.Type).HasColumnName("type");

                entity.Property(e => e.Version).HasColumnName("version");
            });

            modelBuilder.Entity<Inventoryitem>(entity =>
            {
                entity.HasKey(e => e.InventoryId)
                    .HasName("PRIMARY");

                entity.ToTable("inventoryitems");

                entity.HasIndex(e => e.AvatarId, "inventoryitems_avatarid");

                entity.HasIndex(e => e.ParentFolderId, "inventoryitems_parentFolderid");

                entity.Property(e => e.InventoryId)
                    .HasMaxLength(36)
                    .HasColumnName("inventoryID")
                    .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'")
                    .IsFixedLength(true);

                entity.Property(e => e.AssetId)
                    .HasMaxLength(36)
                    .HasColumnName("assetID");

                entity.Property(e => e.AssetType).HasColumnName("assetType");

                entity.Property(e => e.AvatarId)
                    .HasMaxLength(36)
                    .HasColumnName("avatarID")
                    .IsFixedLength(true);

                entity.Property(e => e.CreationDate).HasColumnName("creationDate");

                entity.Property(e => e.CreatorId)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("creatorID")
                    .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'");

                entity.Property(e => e.Flags)
                    .HasColumnType("int unsigned")
                    .HasColumnName("flags");

                entity.Property(e => e.GroupId)
                    .IsRequired()
                    .HasMaxLength(36)
                    .HasColumnName("groupID")
                    .HasDefaultValueSql("'00000000-0000-0000-0000-000000000000'");

                entity.Property(e => e.GroupOwned).HasColumnName("groupOwned");

                entity.Property(e => e.InvType).HasColumnName("invType");

                entity.Property(e => e.InventoryBasePermissions)
                    .HasColumnType("int unsigned")
                    .HasColumnName("inventoryBasePermissions");

                entity.Property(e => e.InventoryCurrentPermissions)
                    .HasColumnType("int unsigned")
                    .HasColumnName("inventoryCurrentPermissions");

                entity.Property(e => e.InventoryDescription)
                    .HasMaxLength(128)
                    .HasColumnName("inventoryDescription");

                entity.Property(e => e.InventoryEveryOnePermissions)
                    .HasColumnType("int unsigned")
                    .HasColumnName("inventoryEveryOnePermissions");

                entity.Property(e => e.InventoryGroupPermissions)
                    .HasColumnType("int unsigned")
                    .HasColumnName("inventoryGroupPermissions");

                entity.Property(e => e.InventoryName)
                    .HasMaxLength(64)
                    .HasColumnName("inventoryName");

                entity.Property(e => e.InventoryNextPermissions)
                    .HasColumnType("int unsigned")
                    .HasColumnName("inventoryNextPermissions");

                entity.Property(e => e.ParentFolderId)
                    .HasMaxLength(36)
                    .HasColumnName("parentFolderID")
                    .IsFixedLength(true);

                entity.Property(e => e.SalePrice).HasColumnName("salePrice");

                entity.Property(e => e.SaleType).HasColumnName("saleType");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
