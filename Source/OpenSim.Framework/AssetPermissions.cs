using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AssetPermissions
    {
        private bool[] m_DisallowExport, m_DisallowImport;
        private string[] m_AssetTypeNames;

        private readonly ILogger<AssetPermissions> m_logger;

        public AssetPermissions(ILogger<AssetPermissions> logger)
        {
            m_logger = logger;
        }

        public AssetPermissions(IConfigurationSection config)
        {
            Type enumType = typeof(AssetType);
            m_AssetTypeNames = Enum.GetNames(enumType);
            for (int i = 0; i < m_AssetTypeNames.Length; i++)
                m_AssetTypeNames[i] = m_AssetTypeNames[i].ToLower();
            int n = Enum.GetValues(enumType).Length;
            m_DisallowExport = new bool[n];
            m_DisallowImport = new bool[n];

            LoadPermsFromConfig(config, "DisallowExport", m_DisallowExport);
            LoadPermsFromConfig(config, "DisallowImport", m_DisallowImport);
        }


        private void LoadPermsFromConfig(IConfigurationSection assetConfig, string variable, bool[] bitArray)
        {
            if (assetConfig == null)
                return;

            string perms = assetConfig.GetValue(variable, String.Empty);
            string[] parts = perms.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in parts)
            {
                int index = Array.IndexOf(m_AssetTypeNames, s.Trim().ToLower());
                if (index >= 0)
                    bitArray[index] = true;
                else
                    m_logger.LogWarning($"[Asset Permissions]: Invalid AssetType {s}");
            }
        }

        public bool AllowedExport(sbyte type)
        {
            string assetTypeName = ((AssetType)type).ToString();

            int index = Array.IndexOf(m_AssetTypeNames, assetTypeName.ToLower());
            if (index >= 0 && m_DisallowExport[index])
            {
                m_logger.LogDebug($"[Asset Permissions]: Export denied: configuration does not allow export of AssetType {assetTypeName}");
                return false;
            }

            return true;
        }

        public bool AllowedImport(sbyte type)
        {
            string assetTypeName = ((AssetType)type).ToString();

            int index = Array.IndexOf(m_AssetTypeNames, assetTypeName.ToLower());
            if (index >= 0 && m_DisallowImport[index])
            {
                m_logger.LogDebug($"[Asset Permissions]: Import denied: configuration does not allow import of AssetType {assetTypeName}");
                return false;
            }

            return true;
        }
    }
}
