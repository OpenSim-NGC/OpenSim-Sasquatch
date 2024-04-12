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
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Services.Interfaces;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenSim.Services.FSAssetService
{
    public class FSAssetConnector : IAssetService
    {
        protected static string _ConfigName = "AssetService";

        static System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();

        static byte[] ToCString(string s)
        {
            byte[] ret = enc.GetBytes(s);
            Array.Resize(ref ret, ret.Length + 1);
            ret[ret.Length - 1] = 0;

            return ret;
        }

        protected IAssetLoader m_AssetLoader;
        protected IFSAssetDataPlugin m_DataConnector;
        protected IAssetService m_FallbackService;
        protected Thread m_WriterThread;
        protected Thread m_StatsThread;
        protected string? m_SpoolDirectory;
        protected object m_readLock = new object();
        protected object m_statsLock = new object();
        protected int m_readCount = 0;
        protected int m_readTicks = 0;
        protected int m_missingAssets = 0;
        protected int m_missingAssetsFS = 0;
        protected string? m_FSBase;
        protected bool m_useOsgridFormat = false;
        protected bool m_showStats = true;

        private static bool m_mainInitialized;
        private static object m_initLock = new object();

        private bool m_isMainInstance;

        protected readonly IComponentContext m_context;
        protected readonly IConfiguration m_configuration;
        protected readonly ILogger<FSAssetConnector> m_logger;

        public FSAssetConnector(
            IComponentContext componentContext,
            IConfiguration config, 
            ILogger<FSAssetConnector> logger,
            IFSAssetDataPlugin dataPlugin
            )
        {
            m_context = componentContext;
            m_configuration = config;
            m_logger = logger;
            m_DataConnector = dataPlugin;

            var assetConfig = config.GetSection(_ConfigName);
            if (assetConfig.Exists() is false)
                throw new Exception("No AssetService configuration");

            lock (m_initLock)
            {
                if (!m_mainInitialized)
                {
                    m_mainInitialized = true;
                    m_isMainInstance = !assetConfig.GetValue<bool>("SecondaryInstance", false);

                    MainConsole.Instance.Commands.AddCommand("fs", false,
                            "show assets", "show assets", "Show asset stats",
                            HandleShowAssets);
                    MainConsole.Instance.Commands.AddCommand("fs", false,
                            "show digest", "show digest <ID>", "Show asset digest",
                            HandleShowDigest);
                    MainConsole.Instance.Commands.AddCommand("fs", false,
                            "delete asset", "delete asset <ID>",
                            "Delete asset from database",
                            HandleDeleteAsset);
                    MainConsole.Instance.Commands.AddCommand("fs", false,
                            "import", "import <conn> <table> [<start> <count>]",
                            "Import legacy assets",
                            HandleImportAssets);
                    MainConsole.Instance.Commands.AddCommand("fs", false,
                            "force import", "force import <conn> <table> [<start> <count>]",
                            "Import legacy assets, overwriting current content",
                            HandleImportAssets);
                }
                else
                {
                    m_isMainInstance = false; // yes redundant...
                }
            }

            // Get Database Connector from Asset Config (If present)
            string? connectionString = assetConfig.GetValue("ConnectionString", string.Empty);
            string? realm = assetConfig.GetValue("Realm", "fsassets");

            int SkipAccessTimeDays = assetConfig.GetValue<int>("DaysBetweenAccessTimeUpdates", 0);

            // If not found above, fallback to Database defaults
            var dbConfig = config.GetSection("DatabaseService");
            if (dbConfig.Exists())
            {
                if (string.IsNullOrEmpty(connectionString))
                    connectionString = dbConfig.GetValue("ConnectionString", String.Empty);
            }

            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("Missing database connection string");

            // Initialize DB And perform any migrations required
            m_DataConnector.Initialise(connectionString, realm, SkipAccessTimeDays);

            // Setup Fallback Service
            string? fallbackService = assetConfig.GetValue("FallbackService", string.Empty);

            if (!string.IsNullOrEmpty(fallbackService))
            {
                var serviceName = fallbackService.Split(":")[1];
                m_FallbackService = m_context.ResolveNamed<IAssetService>(serviceName);

                if (m_FallbackService is not null)
                {
                    m_logger.LogInformation("[FSASSETS]: Fallback service loaded");
                }
                else
                {
                    m_logger.LogError("[FSASSETS]: Failed to load fallback service");
                }
            }

            // Setup directory structure including temp directory
            m_SpoolDirectory = assetConfig.GetValue("SpoolDirectory", "/tmp");
            string spoolTmp = Path.Combine(m_SpoolDirectory, "spool");

            Directory.CreateDirectory(spoolTmp);

            m_FSBase = assetConfig.GetValue("BaseDirectory", String.Empty);
            if (string.IsNullOrEmpty(m_FSBase) is true)
            {
                m_logger.LogError("[FSASSETS]: BaseDirectory not specified");
                throw new Exception("Configuration error");
            }

            m_useOsgridFormat = assetConfig.GetValue<bool>("UseOsgridFormat", m_useOsgridFormat);

            // Default is to show stats to retain original behaviour
            m_showStats = assetConfig.GetValue<bool>("ShowConsoleStats", m_showStats);

            if (m_isMainInstance)
            {
                string? loader = assetConfig.GetValue("DefaultAssetLoader", string.Empty);
                if (!string.IsNullOrEmpty(loader))
                {
                    var serviceName = loader.Split(":")[1];
                    m_AssetLoader = m_context.ResolveNamed<IAssetLoader>(serviceName);
                    string? loaderArgs = assetConfig.GetValue("AssetLoaderArgs", string.Empty);

                    m_logger.LogInformation($"[FSASSETS]: Loading default asset set from {loaderArgs}");

                    m_AssetLoader.ForEachDefaultXmlAsset(
                        loaderArgs,
                        delegate(AssetBase a) { Store(a, false); });
                }

                if (m_WriterThread == null)
                {
                    m_WriterThread = new Thread(Writer);
                    m_WriterThread.Start();
                }

                if (m_showStats && m_StatsThread == null)
                {
                    m_StatsThread = new Thread(Stats);
                    m_StatsThread.Start();
                }
            }

            m_logger.LogInformation("[FSASSETS]: FS asset service enabled");
        }

        private void Stats()
        {
            while (true)
            {
                Thread.Sleep(60000);

                lock (m_statsLock)
                {
                    if (m_readCount > 0)
                    {
                        double avg = (double)m_readTicks / (double)m_readCount;
                        m_logger.LogInformation(
                            $"[FSASSETS]: Read stats: {m_readCount} files, {m_readTicks} ticks, "+
                            $"avg {avg}, missing {m_missingAssets}, FS {m_missingAssetsFS}");
                    }
                    m_readCount = 0;
                    m_readTicks = 0;
                    m_missingAssets = 0;
                    m_missingAssetsFS = 0;
                }
            }
        }

        private void Writer()
        {
            m_logger.LogInformation($"[FSASSETS]: Writer started with spooldir {m_SpoolDirectory} and basedir {m_FSBase}");

            while (true)
            {
                string[] files = Directory.GetFiles(m_SpoolDirectory);

                if (files.Length > 0)
                {
                    int tickCount = Environment.TickCount;
                    for (int i = 0 ; i < files.Length ; i++)
                    {
                        string hash = Path.GetFileNameWithoutExtension(files[i]);
                        string s = HashToFile(hash);
                        string diskFile = Path.Combine(m_FSBase, s);
                        bool pathOk = false;

                        // The cure for chicken bones!
                        while(true)
                        {
                            try
                            {
                                // Try to make the directory we need for this file
                                Directory.CreateDirectory(Path.GetDirectoryName(diskFile));
                                pathOk = true;
                                break;
                            }
                            catch (System.IO.IOException)
                            {
                                // Creating the directory failed. This can't happen unless
                                // a part of the path already exists as a file. Sadly the
                                // SRAS data contains such files.
                                string d = Path.GetDirectoryName(diskFile);

                                // Test each path component in turn. If we can successfully
                                // make a directory, the level below must be the chicken bone.
                                while (d.Length > 0)
                                {
                                    Console.WriteLine(d);
                                    try
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(d));
                                    }
                                    catch (System.IO.IOException)
                                    {
                                        d = Path.GetDirectoryName(d);

                                        // We failed making the directory and need to
                                        // go up a bit more
                                        continue;
                                    }

                                    // We succeeded in making the directory and (d) is
                                    // the chicken bone
                                    break;
                                }

                                // Is the chicken alive?
                                if (d.Length > 0)
                                {
                                    Console.WriteLine(d);

                                    FileAttributes attr = File.GetAttributes(d);

                                    if ((attr & FileAttributes.Directory) == 0)
                                    {
                                        // The chicken bone should be resolved.
                                        // Return to writing the file.
                                        File.Delete(d);
                                        continue;
                                    }
                                }
                            }
                            // Could not resolve, skipping
                            m_logger.LogError($"[FSASSETS]: Could not resolve path creation error for {diskFile}");
                            break;
                        }

                        if (pathOk)
                        {
                            try
                            {
                                byte[] data = File.ReadAllBytes(files[i]);

                                using (GZipStream gz = new GZipStream(new FileStream(diskFile + ".gz", FileMode.Create), CompressionMode.Compress))
                                {
                                    gz.Write(data, 0, data.Length);
                                }
                                File.Delete(files[i]);

                                //File.Move(files[i], diskFile);
                            }
                            catch(System.IO.IOException e)
                            {
                                if (e.Message.StartsWith("Win32 IO returned ERROR_ALREADY_EXISTS"))
                                    File.Delete(files[i]);
                                else
                                    throw;
                            }
                        }
                    }

                    int totalTicks = System.Environment.TickCount - tickCount;
                    if (totalTicks > 0) // Wrap?
                    {
                        var avg = (double)totalTicks / (double)files.Length;
                        m_logger.LogInformation($"[FSASSETS]: Write cycle complete, {files.Length} files, {totalTicks} ticks, avg {avg}");
                    }
                }

                Thread.Sleep(1000);
            }
        }

        string GetSHA256Hash(byte[] data)
        {
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(data);

            return BitConverter.ToString(hash).Replace("-", String.Empty);
        }

        public string HashToPath(string hash)
        {
            if (hash == null || hash.Length < 10)
                return "junkyard";

            if (m_useOsgridFormat)
            {
                /*
                 * The code below is the OSGrid code.
                 */
                return Path.Combine(hash.Substring(0, 3),
                       Path.Combine(hash.Substring(3, 3)));
            }
            else
            {
                /*
                 * The below is what core would normally use.
                 * This is modified to work in OSGrid, as seen
                 * above, because the SRAS data is structured
                 * that way.
                 */
                return Path.Combine(hash.Substring(0, 2),
                       Path.Combine(hash.Substring(2, 2),
                       Path.Combine(hash.Substring(4, 2),
                       hash.Substring(6, 4))));
            }
        }

        private bool AssetExists(string hash)
        {
            string s = HashToFile(hash);
            string diskFile = Path.Combine(m_FSBase, s);

            if (File.Exists(diskFile + ".gz") || File.Exists(diskFile))
                return true;

            return false;
        }

        public virtual bool[] AssetsExist(string[] ids)
        {
            UUID[] uuid = Array.ConvertAll(ids, id => UUID.Parse(id));
            return m_DataConnector.AssetsExist(uuid);
        }

        public string HashToFile(string hash)
        {
            return Path.Combine(HashToPath(hash), hash);
        }

        public virtual AssetBase Get(string id)
        {
            string hash;

            return Get(id, out hash);
        }

        public AssetBase Get(string id, string ForeignAssetService, bool dummy)
        {
            return null;
        }

        private AssetBase Get(string id, out string sha)
        {
            string hash = string.Empty;

            int startTime = System.Environment.TickCount;
            AssetMetadata metadata;

            lock (m_readLock)
            {
                metadata = m_DataConnector.Get(id, out hash);
            }

            sha = hash;

            if (metadata == null)
            {
                AssetBase asset = null;
                if (m_FallbackService != null)
                {
                    asset = m_FallbackService.Get(id);
                    if (asset != null)
                    {
                        asset.Metadata.ContentType =
                                SLUtil.SLAssetTypeToContentType((int)asset.Type);
                        sha = GetSHA256Hash(asset.Data);
                        m_logger.LogInformation($"[FSASSETS]: Added asset {id} from fallback to local store");
                        Store(asset);
                    }
                }

                if (asset == null && m_showStats)
                {
                    // m_log.InfoFormat("[FSASSETS]: Asset {0} not found", id);
                    m_missingAssets++;
                }

                return asset;
            }

            AssetBase newAsset = new AssetBase();
            newAsset.Metadata = metadata;
            try
            {
                newAsset.Data = GetFsData(hash);
                if (newAsset.Data.Length == 0)
                {
                    AssetBase asset = null;
                    if (m_FallbackService != null)
                    {
                        asset = m_FallbackService.Get(id);
                        if (asset != null)
                        {
                            asset.Metadata.ContentType =
                                    SLUtil.SLAssetTypeToContentType((int)asset.Type);
                            sha = GetSHA256Hash(asset.Data);
                            m_logger.LogInformation($"[FSASSETS]: Added asset {id} from fallback to local store");
                            Store(asset);
                        }
                    }
                    if (asset == null)
                    {
                        if (m_showStats)
                            m_missingAssetsFS++;
                        // m_log.InfoFormat("[FSASSETS]: Asset {0}, hash {1} not found in FS", id, hash);
                    }
                    else
                    {
                        // Deal with bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
                        // Fix bad assets before sending them elsewhere
                        if (asset.Type == (int)AssetType.Object && asset.Data != null)
                        {
                            string xml = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(asset.Data));
                            asset.Data = Utils.StringToBytes(xml);
                        }
                        return asset;
                    }
                }

                if (m_showStats)
                {
                    lock (m_statsLock)
                    {
                        m_readTicks += Environment.TickCount - startTime;
                        m_readCount++;
                    }
                }

                // Deal with bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
                // Fix bad assets before sending them elsewhere
                if (newAsset.Type == (int)AssetType.Object && newAsset.Data != null)
                {
                    string xml = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(newAsset.Data));
                    newAsset.Data = Utils.StringToBytes(xml);
                }

                return newAsset;
            }
            catch (Exception exception)
            {
                m_logger.LogError(exception, "Get()");
                Thread.Sleep(5000);
                Environment.Exit(1);
                return null;
            }
        }

        public virtual AssetMetadata GetMetadata(string id)
        {
            string hash;
            return m_DataConnector.Get(id, out hash);
        }

        public virtual byte[] GetData(string id)
        {
            string hash;
            if (m_DataConnector.Get(id, out hash) == null)
                return null;

            return GetFsData(hash);
        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            AssetBase asset = Get(id);

            handler(id, sender, asset);

            return true;
        }

        public byte[] GetFsData(string hash)
        {
            string spoolFile = Path.Combine(m_SpoolDirectory, hash + ".asset");

            if (File.Exists(spoolFile))
            {
                try
                {
                    byte[] content = File.ReadAllBytes(spoolFile);

                    return content;
                }
                catch
                {
                }
            }

            string file = HashToFile(hash);
            string diskFile = Path.Combine(m_FSBase, file);

            if (File.Exists(diskFile + ".gz"))
            {
                try
                {
                    using (GZipStream gz = new GZipStream(new FileStream(diskFile + ".gz", FileMode.Open, FileAccess.Read), CompressionMode.Decompress))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] data = new byte[32768];
                            int bytesRead;

                            do
                            {
                                bytesRead = gz.Read(data, 0, 32768);
                                if (bytesRead > 0)
                                    ms.Write(data, 0, bytesRead);
                            } while (bytesRead > 0);

                            return ms.ToArray();
                        }
                    }
                }
                catch (Exception)
                {
                    return Array.Empty<byte>();
                }
            }
            else if (File.Exists(diskFile))
            {
                try
                {
                    byte[] content = File.ReadAllBytes(diskFile);

                    return content;
                }
                catch
                {
                }
            }
            return Array.Empty<byte>();

        }

        public virtual string Store(AssetBase asset)
        {
            return Store(asset, false);
        }

        private string Store(AssetBase asset, bool force)
        {
            int tickCount = Environment.TickCount;
            string hash = GetSHA256Hash(asset.Data);

            if (asset.Name.Length > AssetBase.MAX_ASSET_NAME)
            {
                string assetName = asset.Name.Substring(0, AssetBase.MAX_ASSET_NAME);
                m_logger.LogWarning(
                    $"[FSASSETS]: Name '{asset.Name}' for asset {asset.ID} truncated " +
                    $"from {asset.Name.Length} to {assetName.Length} characters on add");
                asset.Name = assetName;
            }

            if (asset.Description.Length > AssetBase.MAX_ASSET_DESC)
            {
                string assetDescription = asset.Description.Substring(0, AssetBase.MAX_ASSET_DESC);
                m_logger.LogWarning(
                    $"[FSASSETS]: Description '{asset.Description}' for asset {asset.ID} truncated " +
                    $"from {asset.Description.Length} to {assetDescription.Length} characters on add");
                asset.Description = assetDescription;
            }

            if (!AssetExists(hash))
            {
                string tempFile = Path.Combine(Path.Combine(m_SpoolDirectory, "spool"), hash + ".asset");
                string finalFile = Path.Combine(m_SpoolDirectory, hash + ".asset");

                if (!File.Exists(finalFile))
                {
                    // Deal with bug introduced in Oct. 20 (1eb3e6cc43e2a7b4053bc1185c7c88e22356c5e8)
                    // Fix bad assets before storing on this server
                    if (asset.Type == (int)AssetType.Object && asset.Data != null)
                    {
                        string xml = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(asset.Data));
                        asset.Data = Utils.StringToBytes(xml);
                    }

                    FileStream fs = File.Create(tempFile);

                    fs.Write(asset.Data, 0, asset.Data.Length);

                    fs.Close();

                    File.Move(tempFile, finalFile);
                }
            }

            if (asset.ID.Length == 0)
            {
                if (asset.FullID.IsZero())
                {
                    asset.FullID = UUID.Random();
                }
                asset.ID = asset.FullID.ToString();
            }
            else if (asset.FullID.IsZero())
            {
                UUID uuid = UUID.Zero;
                if (UUID.TryParse(asset.ID, out uuid))
                {
                    asset.FullID = uuid;
                }
                else
                {
                    asset.FullID = UUID.Random();
                }
            }

            if (!m_DataConnector.Store(asset.Metadata, hash))
            {
                if (asset.Metadata.Type == -2)
                    return asset.ID;

                return UUID.Zero.ToString();
            }
            else
            {
                return asset.ID;
            }
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;

//            string oldhash;
//            AssetMetadata meta = m_DataConnector.Get(id, out oldhash);
//
//            if (meta == null)
//                return false;
//
//            AssetBase asset = new AssetBase();
//            asset.Metadata = meta;
//            asset.Data = data;
//
//            Store(asset);
//
//            return true;
        }

        public virtual bool Delete(string id)
        {
            m_DataConnector.Delete(id);

            return true;
        }

        private void HandleShowAssets(string module, string[] args)
        {
            int num = m_DataConnector.Count();
            MainConsole.Instance.Output(string.Format("Total asset count: {0}", num));
        }

        private void HandleShowDigest(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: show digest <ID>");
                return;
            }

            string hash;
            AssetBase asset = Get(args[2], out hash);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Output("Asset not found");
                return;
            }

            int i;

            MainConsole.Instance.Output(String.Format("Name: {0}", asset.Name));
            MainConsole.Instance.Output(String.Format("Description: {0}", asset.Description));
            MainConsole.Instance.Output(String.Format("Type: {0}", asset.Type));
            MainConsole.Instance.Output(String.Format("Content-type: {0}", asset.Metadata.ContentType));
            MainConsole.Instance.Output(String.Format("Flags: {0}", asset.Metadata.Flags.ToString()));
            MainConsole.Instance.Output(String.Format("FS file: {0}", HashToFile(hash)));

            for (i = 0 ; i < 5 ; i++)
            {
                int off = i * 16;
                if (asset.Data.Length <= off)
                    break;
                int len = 16;
                if (asset.Data.Length < off + len)
                    len = asset.Data.Length - off;

                byte[] line = new byte[len];
                Array.Copy(asset.Data, off, line, 0, len);

                string text = BitConverter.ToString(line);
                MainConsole.Instance.Output(String.Format("{0:x4}: {1}", off, text));
            }
        }

        private void HandleDeleteAsset(string module, string[] args)
        {
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: delete asset <ID>");
                return;
            }

            AssetBase asset = Get(args[2]);

            if (asset == null || asset.Data.Length == 0)
            {
                MainConsole.Instance.Output("Asset not found");
                return;
            }

            m_DataConnector.Delete(args[2]);

            MainConsole.Instance.Output("Asset deleted");
        }

        private void HandleImportAssets(string module, string[] args)
        {
            bool force = false;
            if (args[0] == "force")
            {
                force = true;
                List<string> list = new List<string>(args);
                list.RemoveAt(0);
                args = list.ToArray();
            }
            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Syntax: import <conn> <table> [<start> <count>]");
            }
            else
            {
                string conn = args[1];
                string table = args[2];
                int start = 0;
                int count = -1;
                if (args.Length > 3)
                {
                    start = Convert.ToInt32(args[3]);
                }
                if (args.Length > 4)
                {
                    count = Convert.ToInt32(args[4]);
                }
                m_DataConnector.Import(conn, table, start, count, force, new FSStoreDelegate(Store));
            }
        }

        public AssetBase GetCached(string id)
        {
            return Get(id);
        }

        public void Get(string id, string ForeignAssetService, bool StoreOnLocalGrid, SimpleAssetRetrieved callBack)
        {
            return;
        }
    }
}
