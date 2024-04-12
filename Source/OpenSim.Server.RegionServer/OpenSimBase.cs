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

using log4net;
using log4net.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.ApplicationPlugins.RegionModulesController;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.UserAccountService;
using System.Net;
using System.Reflection;

namespace OpenSim.Server.RegionServer
{
    /// <summary>
    /// Common OpenSimulator simulator code
    /// </summary>
    public class OpenSimBase : BaseOpenSimServer, IOpenSimBase
    {
        // OpenSim.ini Section name for ESTATES Settings
        public const string ESTATE_SECTION_NAME = "Estates";

        public ConsoleCommand CreateAccount = null;

        public List<IApplicationPlugin> m_plugins = new List<IApplicationPlugin>();

        public string managedStatsPassword = String.Empty;
        public string managedStatsURI = String.Empty;
        public string userStatsURI = String.Empty;

        /// <value>
        /// The file used to load and save prim backup xml if no filename has been specified
        /// </value>
        protected const string DEFAULT_PRIM_BACKUP_FILENAME = "prim-backup.xml";

        protected bool m_autoCreateClientStack = true;
        protected Dictionary<EndPoint, uint> m_clientCircuits = new Dictionary<EndPoint, uint>();

        protected ConfigurationLoader m_configLoader;
        protected bool m_httpServerSSL;
        protected int proxyOffset = 0;
        protected string proxyUrl;
        private const string PLUGIN_ASSET_CACHE = "/OpenSim/AssetCache";

        // These are the names of the plugin-points extended by this
        // class during system startup.
        //
        private const string PLUGIN_ASSET_SERVER_CLIENT = "/OpenSim/AssetClient";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<string> m_permsModules;

        private bool m_securePermissionsLoading = true;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configSource"></param>
        public OpenSimBase(IConfiguration configSource) : base()
        {
            EnableInitialPluginLoad = true;
            LoadEstateDataService = true;

            ServiceProvider = Application.ServiceProvider;

            LoadConfigSettings(configSource);
        }

        public IRegistryCore ApplicationRegistry { get; set; } = new RegistryCore();

        /// <value>
        /// The config information passed into the OpenSimulator region server.
        /// </value>
        //public IConfiguration ConfigSource { get; private set; }

        /// <summary>
        /// ConfigurationSettings
        /// </summary>
        public ConfigSettings ConfigurationSettings { get; set; }

        /// <summary>
        /// Allow all plugin loading to be disabled for tests/debug.
        /// </summary>
        /// <remarks>
        /// true by default
        /// </remarks>
        public bool EnableInitialPluginLoad { get; set; } = true;

        /// <summary>
        /// EstateDataService
        /// </summary>
        public IEstateDataService EstateDataService { get; set; }

        /// <summary>
        /// HttpServerPort
        /// </summary>
        public uint HttpServerPort { get; set; }

        /// <summary>
        /// Control whether we attempt to load an estate data service.
        /// </summary>
        /// <remarks>For tests/debugging</remarks>
        public bool LoadEstateDataService { get; set; } = true;

        public NetworkServersInfo NetServersInfo { get; set; }

        public SceneManager SceneManager { get; protected set; }

        public IServiceProvider ServiceProvider { get; set; }

        public ISimulationDataService SimulationDataService { get; protected set; }

        /// <summary>
        /// Remove a region from the simulator without deleting it permanently.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public void CloseRegion(Scene scene)
        {
            // only need to check this if we are not at the
            // root level
            if ((SceneManager.CurrentScene != null) &&
                (SceneManager.CurrentScene.RegionInfo.RegionID == scene.RegionInfo.RegionID))
            {
                SceneManager.TrySetCurrentScene("..");
            }

            SceneManager.CloseScene(scene);
            //ShutdownClientServer(scene.RegionInfo);
        }

        /// <summary>
        /// Remove a region from the simulator without deleting it permanently.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public void CloseRegion(string name)
        {
            Scene target;
            if (SceneManager.TryGetScene(name, out target))
                CloseRegion(target);
        }

        /// <summary>
        /// Create an estate with an initial region.
        /// </summary>
        /// <remarks>
        /// This method doesn't allow an estate to be created with the same name as existing estates.
        /// </remarks>
        /// <param name="regInfo"></param>
        /// <param name="estatesByName">A list of estate names that already exist.</param>
        /// <param name="estateName">Estate name to create if already known</param>
        /// <returns>true if the estate was created, false otherwise</returns>
        public bool CreateEstate(RegionInfo regInfo, Dictionary<string, EstateSettings> estatesByName, string estateName)
        {
            // Create a new estate
            regInfo.EstateSettings = EstateDataService.LoadEstateSettings(regInfo.RegionID, true);

            string newName;
            if (!string.IsNullOrEmpty(estateName))
                newName = estateName;
            else
                newName = MainConsole.Instance.Prompt("New estate name", regInfo.EstateSettings.EstateName);

            if (estatesByName.ContainsKey(newName))
            {
                MainConsole.Instance.Output("An estate named {0} already exists.  Please try again.", newName);
                return false;
            }

            regInfo.EstateSettings.EstateName = newName;

            // FIXME: Later on, the scene constructor will reload the estate settings no matter what.
            // Therefore, we need to do an initial save here otherwise the new estate name will be reset
            // back to the default.  The reloading of estate settings by scene could be eliminated if it
            // knows that the passed in settings in RegionInfo are already valid.  Also, it might be
            // possible to eliminate some additional later saves made by callers of this method.
            EstateDataService.StoreEstateSettings(regInfo.EstateSettings);

            return true;
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <returns></returns>
        public void CreateRegion(RegionInfo regionInfo, bool portadd_flag, out IScene scene)
        {
            CreateRegion(regionInfo, portadd_flag, false, out scene);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public void CreateRegion(RegionInfo regionInfo, out IScene scene)
        {
            CreateRegion(regionInfo, false, true, out scene);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <param name="do_post_init"></param>
        /// <returns></returns>
        public void CreateRegion(RegionInfo regionInfo, bool portadd_flag, bool do_post_init, out IScene mscene)
        {
            RegionModulesControllerPlugin controller = null;

            if (!ApplicationRegistry.TryGet(out controller))
            {
                m_log.Fatal("[REGIONMODULES]: The new RegionModulesController is missing...");
                Environment.Exit(0);
            }

            int port = regionInfo.InternalEndPoint.Port;

            // set initial RegionID to originRegionID in RegionInfo. (it needs for loding prims)
            // Commented this out because otherwise regions can't register with
            // the grid as there is already another region with the same UUID
            // at those coordinates. This is required for the load balancer to work.
            // --Mike, 2009.02.25
            //regionInfo.originRegionID = regionInfo.RegionID;

            // set initial ServerURI
            regionInfo.HttpPort = HttpServerPort;
            if (m_httpServerSSL)
            {
                if (!m_httpServer.CheckSSLCertHost(regionInfo.ExternalHostName))
                    throw new Exception("main http cert CN doesn't match region External IP");

                regionInfo.ServerURI = "https://" + regionInfo.ExternalHostName +
                         ":" + regionInfo.HttpPort.ToString() + "/";
            }
            else
                regionInfo.ServerURI = "http://" + regionInfo.ExternalHostName +
                         ":" + regionInfo.HttpPort.ToString() + "/";

            regionInfo.osSecret = m_osSecret;

            if ((proxyUrl.Length > 0) && (portadd_flag))
            {
                // set proxy url to RegionInfo
                regionInfo.proxyUrl = proxyUrl;
                regionInfo.ProxyOffset = proxyOffset;
                Util.XmlRpcCommand(proxyUrl, "AddPort", port, port + proxyOffset, regionInfo.ExternalHostName);
            }

            Scene scene = SetupScene(regionInfo, proxyOffset, Config);

            m_log.Info("[REGIONMODULES]: Loading Region's modules");

            if (controller != null)
                controller.AddRegionToModules(scene);

            if (m_securePermissionsLoading)
            {
                foreach (string s in m_permsModules)
                {
                    if (!scene.RegionModules.ContainsKey(s))
                    {
                        m_log.Fatal("[MODULES]: Required module " + s + " not found.");
                        Environment.Exit(0);
                    }
                }

                m_log.InfoFormat("[SCENE]: Secure permissions loading enabled, modules loaded: {0}", String.Join(" ", m_permsModules.ToArray()));
            }

            scene.SetModuleInterfaces();
            // First Step of bootreport sequence
            if (scene.SnmpService != null)
            {
                scene.SnmpService.ColdStart(1, scene);
                scene.SnmpService.LinkDown(scene);
            }

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Loading prims", scene);
            }

            while (regionInfo.EstateSettings.EstateOwner.IsZero() && MainConsole.Instance != null)
                SetUpEstateOwner(scene);

            scene.loadAllLandObjectsFromStorage(regionInfo.originRegionID);

            // Prims have to be loaded after module configuration since some modules may be invoked during the load
            scene.LoadPrimsFromStorage(regionInfo.originRegionID);

            // TODO : Try setting resource for region xstats here on scene
            MainServer.Instance.AddSimpleStreamHandler(new RegionStatsSimpleHandler(regionInfo));

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Grid Registration in progress", scene);
            }

            try
            {
                scene.RegisterRegionWithGrid();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[STARTUP]: Registration of region with grid failed, aborting startup due to {0} {1}",
                    e.Message, e.StackTrace);

                if (scene.SnmpService != null)
                {
                    scene.SnmpService.Critical("Grid registration failed. Startup aborted.", scene);
                }
                // Carrying on now causes a lot of confusion down the
                // line - we need to get the user's attention
                Environment.Exit(1);
            }

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Grid Registration done", scene);
            }

            // We need to do this after we've initialized the scripting engines.
            scene.CreateScriptInstances();

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("ScriptEngine started", scene);
            }

            SceneManager.Add(scene);

            //if (m_autoCreateClientStack)
            //{
            //    foreach (IClientNetworkServer clientserver in clientServers)
            //    {
            //        m_clientServers.Add(clientserver);
            //        clientserver.Start();
            //    }
            //}

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Initializing region modules", scene);
            }
            scene.EventManager.OnShutdown += delegate () { ShutdownRegion(scene); };

            mscene = scene;

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("The region is operational", scene);
                scene.SnmpService.LinkUp(scene);
            }

            //return clientServers;
        }

        /// <summary>
        /// Get the number of the avatars in the Region server
        /// </summary>
        /// <param name="usernum">The first out parameter describing the number of all the avatars in the Region server</param>
        public void GetAvatarNumber(out int usernum)
        {
            usernum = SceneManager.GetCurrentSceneAvatars().Count;
        }

        /// <summary>
        /// Get the number of regions
        /// </summary>
        /// <param name="regionnum">The first out parameter describing the number of regions</param>
        public void GetRegionNumber(out int regionnum)
        {
            regionnum = SceneManager.Scenes.Count;
        }

        /// <summary>
        /// Get the start time and up time of Region server
        /// </summary>
        /// <param name="starttime">The first out parameter describing when the Region server started</param>
        /// <param name="uptime">The second out parameter describing how long the Region server has run</param>
        public void GetRunTime(out string starttime, out string uptime)
        {
            starttime = m_startuptime.ToString();
            uptime = (DateTime.Now - m_startuptime).ToString();
        }

        /// <summary>
        /// Load the estate information for the provided RegionInfo object.
        /// </summary>
        /// <param name="regInfo"></param>
        public bool PopulateRegionEstateInfo(RegionInfo regInfo)
        {
            if (EstateDataService != null)
                regInfo.EstateSettings = EstateDataService.LoadEstateSettings(regInfo.RegionID, false);

            if (regInfo.EstateSettings.EstateID != 0)
                return false;    // estate info in the database did not change

            m_log.WarnFormat("[ESTATE] Region {0} is not part of an estate.", regInfo.RegionName);

            List<EstateSettings> estates = EstateDataService.LoadEstateSettingsAll();
            Dictionary<string, EstateSettings> estatesByName = new Dictionary<string, EstateSettings>();

            foreach (EstateSettings estate in estates)
                estatesByName[estate.EstateName] = estate;

            //##
            // Target Estate Specified in Region.ini
            string targetEstateIDstr = regInfo.GetSetting("TargetEstate");

            if (!string.IsNullOrWhiteSpace(targetEstateIDstr))
            {
                bool targetEstateJoined = false;

                if (Int32.TryParse(targetEstateIDstr, out int targetEstateID) && targetEstateID > 99)
                {
                    // Attempt to join the target estate given in Config by ID
                    foreach (EstateSettings estate in estates)
                    {
                        if (estate.EstateID == targetEstateID)
                        {
                            if (EstateDataService.LinkRegion(regInfo.RegionID, targetEstateID))
                                targetEstateJoined = true;

                            break;
                        }
                    }
                }
                else
                {
                    // Attempt to join the target estate given in Config by name
                    if (estatesByName.TryGetValue(targetEstateIDstr, out EstateSettings targetEstate))
                    {
                        if (EstateDataService.LinkRegion(regInfo.RegionID, (int)targetEstate.EstateID))
                            targetEstateJoined = true;
                    }
                }

                if (targetEstateJoined)
                    return true; // need to update the database
                else
                    m_log.ErrorFormat(
                        "[OPENSIM BASE]: Joining target estate specified in region config {0} failed", targetEstateIDstr);
            }
            //##

            // Default Estate
            if (Config.Configs[ESTATE_SECTION_NAME] != null)
            {
                string defaultEstateName = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateName", null);

                if (defaultEstateName != null)
                {
                    bool defaultEstateJoined = false;
                    if (estatesByName.TryGetValue(defaultEstateName, out EstateSettings defaultEstate))
                    {
                        if (EstateDataService.LinkRegion(regInfo.RegionID, (int)defaultEstate.EstateID))
                            defaultEstateJoined = true;
                    }
                    else
                    {
                        if (CreateEstate(regInfo, estatesByName, defaultEstateName))
                            defaultEstateJoined = true;
                    }

                    if (defaultEstateJoined)
                        return true; // need to update the database
                    else
                        m_log.ErrorFormat(
                            "[OPENSIM BASE]: Joining default estate {0} failed", defaultEstateName);
                }
            }

            // If we have no default estate or creation of the default estate failed then ask the user.
            while (true)
            {
                if (estates.Count == 0)
                {
                    m_log.Info("[ESTATE]: No existing estates found.  You must create a new one.");

                    if (CreateEstate(regInfo, estatesByName, null))
                        break;
                    else
                        continue;
                }
                else
                {
                    string response = MainConsole.Instance.Prompt(
                            string.Format(
                                "Do you wish to join region {0} to an existing estate (yes/no)?", regInfo.RegionName),
                                "yes",
                                new List<string>() { "yes", "no" });

                    if (response == "no")
                    {
                        if (CreateEstate(regInfo, estatesByName, null))
                            break;
                        else
                            continue;
                    }
                    else
                    {
                        string[] estateNames = estatesByName.Keys.ToArray();
                        response
                            = MainConsole.Instance.Prompt(
                                string.Format(
                                    "Name of estate to join.  Existing estate names are ({0})",
                                    string.Join(", ", estateNames)),
                                estateNames[0]);

                        List<int> estateIDs = EstateDataService.GetEstates(response);
                        if (estateIDs.Count < 1)
                        {
                            MainConsole.Instance.Output("The name you have entered matches no known estate.  Please try again.");
                            continue;
                        }

                        int estateID = estateIDs[0];

                        regInfo.EstateSettings = EstateDataService.LoadEstateSettings(estateID);

                        if (EstateDataService.LinkRegion(regInfo.RegionID, estateID))
                            break;

                        MainConsole.Instance.Output("Joining the estate failed. Please try again.");
                    }
                }
            }

            return true;    // need to update the database
        }

        public void RemoveRegion(Scene scene, bool cleanup)
        {
            // only need to check this if we are not at the
            // root level
            if ((SceneManager.CurrentScene != null) &&
                (SceneManager.CurrentScene.RegionInfo.RegionID == scene.RegionInfo.RegionID))
            {
                SceneManager.TrySetCurrentScene("..");
            }

            scene.DeleteAllSceneObjects();
            SceneManager.CloseScene(scene);
            //ShutdownClientServer(scene.RegionInfo);

            if (!cleanup)
                return;

            if (!String.IsNullOrEmpty(scene.RegionInfo.RegionFile))
            {
                if (scene.RegionInfo.RegionFile.ToLower().EndsWith(".xml"))
                {
                    File.Delete(scene.RegionInfo.RegionFile);
                    m_log.InfoFormat("[OPENSIM]: deleting region file \"{0}\"", scene.RegionInfo.RegionFile);
                }
                if (scene.RegionInfo.RegionFile.ToLower().EndsWith(".ini"))
                {
                    try
                    {
                        IniConfigSource source = new IniConfigSource(scene.RegionInfo.RegionFile);
                        if (source.Configs[scene.RegionInfo.RegionName] != null)
                        {
                            source.Configs.Remove(scene.RegionInfo.RegionName);

                            if (source.Configs.Count == 0)
                            {
                                File.Delete(scene.RegionInfo.RegionFile);
                            }
                            else
                            {
                                source.Save(scene.RegionInfo.RegionFile);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public void RemoveRegion(string name, bool cleanUp)
        {
            Scene target;
            if (SceneManager.TryGetScene(name, out target))
                RemoveRegion(target, cleanUp);
        }

        protected virtual void AddPluginCommands(ICommandConsole console)
        {
            List<string> topics = GetHelpTopics();

            foreach (string topic in topics)
            {
                string capitalizedTopic = char.ToUpper(topic[0]) + topic.Substring(1);

                // This is a hack to allow the user to enter the help command in upper or lowercase.  This will go
                // away at some point.
                console.Commands.AddCommand(capitalizedTopic, false, "help " + topic,
                                              "help " + capitalizedTopic,
                                              "Get help on plugin command '" + topic + "'",
                                              HandleCommanderHelp);
                console.Commands.AddCommand(capitalizedTopic, false, "help " + capitalizedTopic,
                                              "help " + capitalizedTopic,
                                              "Get help on plugin command '" + topic + "'",
                                              HandleCommanderHelp);

                ICommander commander = null;

                Scene s = SceneManager.CurrentOrFirstScene;

                if (s != null && s.GetCommanders() != null)
                {
                    if (s.GetCommanders().ContainsKey(topic))
                        commander = s.GetCommanders()[topic];
                }

                if (commander == null)
                    continue;

                foreach (string command in commander.Commands.Keys)
                {
                    console.Commands.AddCommand(capitalizedTopic, false,
                                                  topic + " " + command,
                                                  topic + " " + commander.Commands[command].ShortHelp(),
                                                  String.Empty, HandleCommanderCommand);
                }
            }
        }

        protected Scene CreateScene(RegionInfo regionInfo, ISimulationDataService simDataService,
            IEstateDataService estateDataService, AgentCircuitManager circuitManager)
        {
            return new Scene(
                regionInfo, circuitManager,
                simDataService, estateDataService,
                Config, Version);
        }

        protected override List<string> GetHelpTopics()
        {
            List<string> topics = base.GetHelpTopics();
            Scene s = SceneManager.CurrentOrFirstScene;
            if (s != null && s.GetCommanders() != null)
                topics.AddRange(s.GetCommanders().Keys);

            return topics;
        }

        protected virtual void HandleRestartRegion(RegionInfo whichRegion)
        {
            m_log.InfoFormat(
                "[OPENSIM]: Got restart signal from SceneManager for region {0} ({1},{2})",
                whichRegion.RegionName, whichRegion.RegionLocX, whichRegion.RegionLocY);

            //ShutdownClientServer(whichRegion);
            IScene scene;
            CreateRegion(whichRegion, true, out scene);
            scene.Start();
        }

        protected void Initialize()
        {
            // Called from base.StartUp()

            IConfig startupConfig = Config.Configs["Startup"];
            if (startupConfig == null || startupConfig.GetBoolean("JobEngineEnabled", true))
                WorkManager.JobEngine.Start();

            if (NetServersInfo.HttpUsesSSL)
            {
                m_httpServerSSL = true;
                HttpServerPort = NetServersInfo.httpSSLPort;
            }
            else
            {
                m_httpServerSSL = false;
                HttpServerPort = NetServersInfo.HttpListenerPort;
            }

            SceneManager.OnRestartSim += HandleRestartRegion;

            // Only enable the watchdogs when all regions are ready.  Otherwise we get false positives when cpu is
            // heavily used during initial startup.
            //
            // FIXME: It's also possible that region ready status should be flipped during an OAR load since this
            // also makes heavy use of the CPU.
            SceneManager.OnRegionsReadyStatusChange
                += sm => { MemoryWatchdog.Enabled = sm.AllRegionsReady; Watchdog.Enabled = sm.AllRegionsReady; };
        }

        protected virtual void LoadConfigSettings(IConfiguration configSource)
        {
            m_configLoader = new ConfigurationLoader();

            Config = m_configLoader.LoadConfigSettings(configSource);

            ConfigurationSettings = m_configLoader.ConfigSettings;
            NetServersInfo = m_configLoader.NetworkServersInfo;

            ReadExtraConfigSettings();
        }

        protected virtual void ReadExtraConfigSettings()
        {
            IConfig networkConfig = Config.Configs["Network"];
            if (networkConfig != null)
            {
                proxyUrl = networkConfig.GetString("proxy_url", "");
                proxyOffset = Int32.Parse(networkConfig.GetString("proxy_offset", "0"));
            }

            IConfig startupConfig = Config.Configs["Startup"];
            if (startupConfig != null)
            {
                Util.LogOverloads = startupConfig.GetBoolean("LogOverloads", true);
            }
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(RegionInfo regionInfo)
        {
            return SetupScene(regionInfo, 0, null);
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="proxyOffset"></param>
        /// <param name="configSource"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(RegionInfo regionInfo, int proxyOffset, IConfiguration configSource)
        {
            //List<IClientNetworkServer> clientNetworkServers = null;

            AgentCircuitManager circuitManager = new AgentCircuitManager();
            Scene scene = CreateScene(regionInfo, SimulationDataService, EstateDataService, circuitManager);

            scene.LoadWorldMap();

            return scene;
        }

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        protected override void ShutdownSpecific()
        {
            if (proxyUrl.Length > 0)
            {
                Util.XmlRpcCommand(proxyUrl, "Stop");
            }

            m_log.Info("[SHUTDOWN]: Closing all threads");
            m_log.Info("[SHUTDOWN]: Killing listener thread");
            m_log.Info("[SHUTDOWN]: Killing clients");
            m_log.Info("[SHUTDOWN]: Closing console and terminating");

            try
            {
                SceneManager.Close();

                foreach (IApplicationPlugin plugin in m_plugins)
                    plugin.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error("[SHUTDOWN]: Ignoring failure during shutdown - ", e);
            }

            base.ShutdownSpecific();
        }

        /// <summary>
        /// Performs startup specific to the region server, including initialization of the scene
        /// such as loading configuration from disk.
        /// </summary>
        protected override void StartupSpecific()
        {
            IConfig startupConfig = Config.Configs["Startup"];
            if (startupConfig != null)
            {
                // refuse to run MegaRegions
                if (startupConfig.GetBoolean("CombineContiguousRegions", false))
                {
                    m_log.Fatal("CombineContiguousRegions (MegaRegions) option is no longer suported. Use a older version to save region contents as OAR, then import into a fresh install of this new version");
                    throw new Exception("CombineContiguousRegions not suported");
                }

                string pidFile = startupConfig.GetString("PIDFile", String.Empty);
                if (pidFile != String.Empty)
                    CreatePIDFile(pidFile);

                userStatsURI = startupConfig.GetString("Stats_URI", String.Empty);

                m_securePermissionsLoading = startupConfig.GetBoolean("SecurePermissionsLoading", true);

                string permissionModules = Util.GetConfigVarFromSections<string>(Config, "permissionmodules",
                    new string[] { "Startup", "Permissions" }, "DefaultPermissionsModule");

                m_permsModules = new List<string>(permissionModules.Split(',').Select(m => m.Trim()));

                managedStatsURI = startupConfig.GetString("ManagedStatsRemoteFetchURI", String.Empty);
                managedStatsPassword = startupConfig.GetString("ManagedStatsRemoteFetchPassword", String.Empty);
            }

            /*
            ** Load the simulation data service
            **/
            IConfig simDataConfig = Config.Configs["SimulationDataStore"];
            if (simDataConfig == null)
            {
                throw new Exception($"Configuration file is missing the [SimulationDataStore] section.  Have you copied OpenSim.ini.example to OpenSim.ini to reference config-include/ files?");
            }

            string module = simDataConfig.GetString("LocalServiceModule", String.Empty);
            if (String.IsNullOrEmpty(module))
            {
                throw new Exception($"Configuration file is missing the LocalServiceModule parameter in the [SimulationDataStore] section.");
            }

            var SimulationDataService = Application.ServiceProvider.GetService<ISimulationDataService>();
            if (SimulationDataService == null)
            {
                throw new Exception(
                    $"Could not load an ISimulationDataService implementation from {module} as configured in the LocalServiceModule parameter of the [SimulationDataStore] config section.");
            }

            // Load the estate data service
            module = Util.GetConfigVarFromSections<string>(Config, "LocalServiceModule", new string[] { "EstateDataStore", "EstateService" }, String.Empty);
            if (String.IsNullOrEmpty(module))
            {
                throw new Exception($"Configuration file is missing the LocalServiceModule parameter in the [EstateDataStore] or [EstateService] section");
            }

            if (LoadEstateDataService)
            {
                EstateDataService = Application.ServiceProvider.GetService<IEstateDataService>();
                if (EstateDataService == null)
                {
                    throw new Exception(
                        $"Could not load an IEstateDataService implementation from {module}, as configured in the LocalServiceModule parameter of the [EstateDataStore] config section.");
                }
            }

            SceneManager = SceneManager.Instance;

            Initialize();

            uint mainport = NetServersInfo.HttpListenerPort;
            uint mainSSLport = NetServersInfo.httpSSLPort;

            if (NetServersInfo.HttpUsesSSL && (mainport == mainSSLport))
            {
                m_log.Error("[REGION SERVER]: HTTP Server config failed.   HTTP Server and HTTPS server must be on different ports");
            }

            if (NetServersInfo.HttpUsesSSL)
            {
                m_httpServer = new BaseHttpServer(
                        mainSSLport, NetServersInfo.HttpUsesSSL,
                        NetServersInfo.HttpSSLCN,
                        NetServersInfo.HttpSSLCertPath, NetServersInfo.HttpSSLCNCertPass);

                m_httpServer.Start();
                MainServer.AddHttpServer(m_httpServer);
            }

            // unsecure main server
            BaseHttpServer server = new BaseHttpServer(mainport);
            if (!NetServersInfo.HttpUsesSSL)
            {
                m_httpServer = server;
                server.Start();
            }
            else
                server.Start();

            MainServer.AddHttpServer(server);
            MainServer.UnSecureInstance = server;

            MainServer.Instance = m_httpServer;

            // "OOB" Server
            if (NetServersInfo.ssl_listener)
            {
                if (!NetServersInfo.ssl_external)
                {
                    server = new BaseHttpServer(
                        NetServersInfo.https_port, NetServersInfo.ssl_listener,
                        NetServersInfo.cert_path,
                        NetServersInfo.cert_pass);

                    m_log.InfoFormat("[REGION SERVER]: Starting OOB HTTPS server on port {0}", server.SSLPort);
                    server.Start();
                    MainServer.AddHttpServer(server);
                }
                else
                {
                    server = new BaseHttpServer(NetServersInfo.https_port);

                    m_log.InfoFormat("[REGION SERVER]: Starting HTTP server on port {0} for external HTTPS", server.Port);
                    server.Start();
                    MainServer.AddHttpServer(server);
                }
            }

            base.StartupSpecific();

            if (EnableInitialPluginLoad)
            {
                var plugins = Application.ServiceProvider.GetServices<IApplicationPlugin>();

                foreach (var plugin in plugins)
                {
                    m_log.Info($"[REGION SERVER] Initialising Application Plugin: {plugin.Name}");
                    plugin.Initialise(this);
                    m_plugins.Add(plugin);
                }
            }

            // We still want to post initalize any plugins even if loading has been disabled since a test may have
            // inserted them manually.
            foreach (IApplicationPlugin plugin in m_plugins)
            {
                plugin.PostInitialise();
            }

            if (m_console != null)
            {
                AddPluginCommands(m_console);
            }
        }

        private void HandleCommanderCommand(string module, string[] cmd)
        {
            SceneManager.SendCommandToPluginModules(cmd);
        }

        private void HandleCommanderHelp(string module, string[] cmd)
        {
            // Only safe for the interactive console, since it won't
            // let us come here unless both scene and commander exist
            //
            ICommander moduleCommander = SceneManager.CurrentOrFirstScene.GetCommander(cmd[1].ToLower());
            if (moduleCommander != null)
                m_console.Output(moduleCommander.Help);
        }

        /// <summary>
        /// Try to set up the estate owner for the given scene.
        /// </summary>
        /// <remarks>
        /// The involves asking the user for information about the user on the console.  If the user does not already
        /// exist then it is created.
        /// </remarks>
        /// <param name="scene"></param>
        private void SetUpEstateOwner(Scene scene)
        {
            RegionInfo regionInfo = scene.RegionInfo;

            string estateOwnerFirstName = null;
            string estateOwnerLastName = null;
            string estateOwnerEMail = null;
            string estateOwnerPassword = null;
            string rawEstateOwnerUuid = null;

            if (Config.Configs[ESTATE_SECTION_NAME] != null)
            {
                string defaultEstateOwnerName
                    = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerName", "").Trim();
                string[] ownerNames = defaultEstateOwnerName.Split(' ');

                if (ownerNames.Length >= 2)
                {
                    estateOwnerFirstName = ownerNames[0];
                    estateOwnerLastName = ownerNames[1];
                }

                // Info to be used only on Standalone Mode
                rawEstateOwnerUuid = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerUUID", null);
                estateOwnerEMail = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerEMail", null);
                estateOwnerPassword = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerPassword", null);
            }

            MainConsole.Instance.Output("Estate {0} has no owner set.", regionInfo.EstateSettings.EstateName);
            List<char> excluded = new List<char>(new char[1] { ' ' });

            if (estateOwnerFirstName == null || estateOwnerLastName == null)
            {
                estateOwnerFirstName = MainConsole.Instance.Prompt("Estate owner first name", "Test", excluded);
                estateOwnerLastName = MainConsole.Instance.Prompt("Estate owner last name", "User", excluded);
            }

            UserAccount account
                = scene.UserAccountService.GetUserAccount(regionInfo.ScopeID, estateOwnerFirstName, estateOwnerLastName);

            if (account == null)
            {
                // XXX: The LocalUserAccountServicesConnector is currently registering its inner service rather than
                // itself!
                //                    if (scene.UserAccountService is LocalUserAccountServicesConnector)
                //                    {
                //                        IUserAccountService innerUas
                //                            = ((LocalUserAccountServicesConnector)scene.UserAccountService).UserAccountService;
                //
                //                        m_log.DebugFormat("B {0}", innerUas.GetType());
                //
                //                        if (innerUas is UserAccountService)
                //                        {
                if (scene.UserAccountService is UserAccountService)
                {
                    if (estateOwnerPassword == null)
                        estateOwnerPassword = MainConsole.Instance.Prompt("Password", null, null, false);

                    if (estateOwnerEMail == null)
                        estateOwnerEMail = MainConsole.Instance.Prompt("Email");

                    if (rawEstateOwnerUuid == null)
                        rawEstateOwnerUuid = MainConsole.Instance.Prompt("User ID", UUID.Random().ToString());

                    UUID estateOwnerUuid = UUID.Zero;
                    if (!UUID.TryParse(rawEstateOwnerUuid, out estateOwnerUuid))
                    {
                        m_log.ErrorFormat("[OPENSIM]: ID {0} is not a valid UUID", rawEstateOwnerUuid);
                        return;
                    }

                    // If we've been given a zero uuid then this signals that we should use a random user id
                    if (estateOwnerUuid.IsZero())
                        estateOwnerUuid = UUID.Random();

                    account
                        = ((UserAccountService)scene.UserAccountService).CreateUser(
                            regionInfo.ScopeID,
                            estateOwnerUuid,
                            estateOwnerFirstName,
                            estateOwnerLastName,
                            estateOwnerPassword,
                            estateOwnerEMail);
                }
            }

            if (account == null)
            {
                m_log.ErrorFormat(
                    "[OPENSIM]: Unable to store account. If this simulator is connected to a grid, you must create the estate owner account first at the grid level.");
            }
            else
            {
                regionInfo.EstateSettings.EstateOwner = account.PrincipalID;
                EstateDataService.StoreEstateSettings(regionInfo.EstateSettings);
            }
        }

        private void ShutdownRegion(Scene scene)
        {
            m_log.DebugFormat("[SHUTDOWN]: Shutting down region {0}", scene.RegionInfo.RegionName);
            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("The region is shutting down", scene);
                scene.SnmpService.LinkDown(scene);
            }
            IRegionModulesController controller;
            if (ApplicationRegistry.TryGet<IRegionModulesController>(out controller))
            {
                controller.RemoveRegionFromModules(scene);
            }
        }

        # region Setup methods

        /// <summary>
        /// handler to supply serving http://domainname:port/robots.txt
        /// </summary>
        public class SimRobotsHandler : SimpleStreamHandler
        {
            private readonly byte[] binmsg;

            public SimRobotsHandler() : base("/robots.txt", "SimRobots")
            {
                binmsg = Util.UTF8.GetBytes("# go away\nUser-agent: *\nDisallow: /\n");
            }

            protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                httpResponse.KeepAlive = false;
                httpResponse.RawBuffer = binmsg;
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
            }
        }

        /// <summary>
        /// Handler to supply the current status of this sim
        /// </summary>
        /// <remarks>
        /// Currently this is always OK if the simulator is still listening for connections on its HTTP service
        /// </remarks>
        public class SimStatusHandler : SimpleStreamHandler
        {
            public SimStatusHandler() : base("/simstatus", "SimStatus")
            {
            }

            protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                httpResponse.KeepAlive = false;
                httpResponse.RawBuffer = Util.UTF8.GetBytes("OK");
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
            }
        }

        /// <summary>
        /// Handler to supply the current extended status of this sim
        /// Sends the statistical data in a json serialization
        /// </summary>
        public class XSimStatusHandler : SimpleStreamHandler
        {
            private OpenSimBase m_opensim;

            public XSimStatusHandler(OpenSimBase sim)
                : base("/" + Util.SHA1Hash(sim.osSecret), "XSimStatus")
            {
                m_opensim = sim;
            }

            protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                httpResponse.KeepAlive = false;
                try
                {
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(m_opensim.StatReport(httpRequest));
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                }
                catch
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
        }

        /// <summary>
        /// Handler to supply the current extended status of this sim to a user configured URI
        /// Sends the statistical data in a json serialization
        /// If the request contains a key, "callback" the response will be wrappend in the
        /// associated value for jsonp used with ajax/javascript
        /// </summary>
        protected class UXSimStatusHandler : SimpleStreamHandler
        {
            private OpenSimBase m_opensim;

            public UXSimStatusHandler(OpenSimBase sim)
                : base("/" + sim.userStatsURI, "UXSimStatus")
            {
                m_opensim = sim;
            }

            protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                httpResponse.KeepAlive = false;
                try
                {
                    httpResponse.RawBuffer = Util.UTF8.GetBytes(m_opensim.StatReport(httpRequest));
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                }
                catch
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
        }

        #endregion
    }
}