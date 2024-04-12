// * Modified by Fumi.Iseki for Unix/Linix  http://www.nsl.tuis.ac.jp
// *
// * Copyright (c) Contributors, http://opensimulator.org/, http://www.nsl.tuis.ac.jp/
// * See CONTRIBUTORS.TXT for a full list of copyright holders.
// *
// * Redistribution and use in source and binary forms, with or without
// * modification, are permitted provided that the following conditions are met:
// *	 * Redistributions of source code must retain the above copyright
// *	   notice, this list of conditions and the following disclaimer.
// *	 * Redistributions in binary form must reproduce the above copyright
// *	   notice, this list of conditions and the following disclaimer in the
// *	   documentation and/or other materials provided with the distribution.
// *	 * Neither the name of the OpenSim Project nor the
// *	   names of its contributors may be used to endorse or promote products
// *	   derived from this software without specific prior written permission.
// *
// * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
// * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
// * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES
// * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// */

//	2018-03-03	Change to not run when not selected. Permits other money modules (like Gloebit) to have control when they are selected.
//	2019-07-26	Use the extras interface to set the currency to the local symbol (WIP).
//	2020/12/13	Integrate previous changes with 0.9.2
//	2021/05/04	Permit HG visitors to transact. Ensure The money symbol is sent to the viewer.
//  2022/09/29  Send the helper URI to the viewer only when the EconomyHelper setting in OpenSim.ini

using NSL.Certificate.Tools;
using NSL.Network.XmlRpc;
using Nwc.XmlRpc;

// using OpenMetaverse;
// using OpenMetaverse.StructuredData;
// using OpenSim.Data.MySQL.MoneyData;
// using OpenSim.Framework;
// using OpenSim.Framework.Servers;
// using OpenSim.Framework.Servers.HttpServer;
// using OpenSim.Region.Framework.Interfaces;
// using OpenSim.Region.Framework.Scenes;
// using OpenSim.Services.Interfaces;
// using System.Collections;
// using System.Net;
// using System.Net.Security;
// using System.Reflection;
// using System.Security.Cryptography;
// using System.Security.Cryptography.X509Certificates;
// using System.Text;

using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using OpenSim.Services.Interfaces;
using OpenSim.Data;

namespace OpenSim.Region.OptionalModules.Currency
{
    /// <summary>
    /// Transaction Type
    /// </summary>
    public enum TransactionType : int
    {
        None = 0,

        // Extend
        BirthGift = 900,

        AwardPoints = 901,

        // One-Time Charges
        ObjectClaim = 1000,

        LandClaim = 1001,
        GroupCreate = 1002,
        GroupJoin = 1004,
        TeleportCharge = 1100,
        UploadCharge = 1101,
        LandAuction = 1102,
        ClassifiedCharge = 1103,

        // Recurrent Charges
        ObjectTax = 2000,

        LandTax = 2001,
        LightTax = 2002,
        ParcelDirFee = 2003,
        GroupTax = 2004,
        ClassifiedRenew = 2005,
        ScheduledFee = 2900,

        // Inventory Transactions
        GiveInventory = 3000,

        // Transfers Between Users
        ObjectSale = 5000,

        Gift = 5001,
        LandSale = 5002,
        ReferBonus = 5003,
        InvntorySale = 5004,
        RefundPurchase = 5005,
        LandPassSale = 5006,
        DwellBonus = 5007,
        PayObject = 5008,
        ObjectPays = 5009,
        BuyMoney = 5010,
        MoveMoney = 5011,
        SendMoney = 5012,

        // Group Transactions
        GroupLandDeed = 6001,

        GroupObjectDeed = 6002,
        GroupLiability = 6003,
        GroupDividend = 6004,
        GroupMembershipDues = 6005,

        // Stipend Credits
        StipendBasic = 10000
    }

    /*
		// Refer to OpenMetaverse
		public enum OpenMetaverse.MoneyTransactionType : int
		{
			None = 0,
			FailSimulatorTimeout = 1,
			FailDataserverTimeout = 2,
			ObjectClaim = 1000,
			LandClaim = 1001,
			GroupCreate = 1002,
			ObjectPublicClaim = 1003,
			GroupJoin = 1004,
			TeleportCharge = 1100,
			UploadCharge = 1101,
			LandAuction = 1102,
			ClassifiedCharge = 1103,
			ObjectTax = 2000,
			LandTax = 2001,
			LightTax = 2002,
			ParcelDirFee = 2003,
			GroupTax = 2004,
			ClassifiedRenew = 2005,
			GiveInventory = 3000,
			ObjectSale = 5000,
			Gift = 5001,
			LandSale = 5002,
			ReferBonus = 5003,
			InventorySale = 5004,
			RefundPurchase = 5005,
			LandPassSale = 5006,
			DwellBonus = 5007,
			PayObject = 5008,
			ObjectPays = 5009,
			GroupLandDeed = 6001,
			GroupObjectDeed = 6002,
			GroupLiability = 6003,
			GroupDividend = 6004,
			GroupMembershipDues = 6005,
			ObjectRelease = 8000,
			LandRelease = 8001,
			ObjectDelete = 8002,
			ObjectPublicDecay = 8003,
			ObjectPublicDelete = 8004,
			LindenAdjustment = 9000,
			LindenGrant = 9001,
			LindenPenalty = 9002,
			EventFee = 9003,
			EventPrize = 9004,
			StipendBasic = 10000,
			StipendDeveloper = 10001,
			StipendAlways = 10002,
			StipendDaily = 10003,
			StipendRating = 10004,
			StipendDelta = 10005
		}
	*/

    public class DTLNSLMoneyModule : IMoneyModule, ISharedRegionModule
    {
        #region Constant numbers and members.

        private bool m_debug = true;

        // Constant memebers
        private const int MONEYMODULE_REQUEST_TIMEOUT = 10000;

        // Private data members.
        private Dictionary<string, XmlRpcMethod> m_rpcHandlers;

        //private bool  m_enabled = true;
        private bool m_sellEnabled = false;

        private bool m_enable_server = false;   // start Money Server disabled
        
        private string m_moneyHelperURI = string.Empty;
        private string m_moneyServURL = string.Empty;
        private string m_moneySymbol = string.Empty;
        private bool m_entryalert = true;
        public BaseHttpServer HttpServer;

        private string m_certFilename = "";
        private string m_certPassword = "";
        private bool m_checkServerCert = false;
        private string m_cacertFilename = "";
        private X509Certificate2 m_cert = null;

        private bool m_use_web_settle = false;
        private string m_settle_url = "";
        private string m_settle_message = "";
        private bool m_settle_user = false;

        private int m_hg_avatarClass = (int)AvatarType.HG_AVATAR;

        /// <summary>
        /// Scene dictionary indexed by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_sceneList = new Dictionary<ulong, Scene>();

        /// <summary>
        /// To cache the balance data while the money server is not available.
        /// </summary>
        private Dictionary<UUID, int> m_moneyServer = new Dictionary<UUID, int>();

        // Events
        public event ObjectPaid OnObjectPaid;

        // Price
        private int ObjectCount = 0;

        private int PriceEnergyUnit = 100;
        private int PriceObjectClaim = 10;
        private int PricePublicObjectDecay = 4;
        private int PricePublicObjectDelete = 4;
        private int PriceParcelClaim = 1;
        private float PriceParcelClaimFactor = 1.0f;
        private int PriceUpload = 0;
        private int PriceRentLight = 5;
        private float PriceObjectRent = 1.0f;
        private float PriceObjectScaleFactor = 10.0f;
        private int PriceParcelRent = 1;
        private int PriceGroupCreate = 0;
        private int TeleportMinPrice = 2;
        private float TeleportPriceExponent = 2.0f;
        private float EnergyEfficiency = 1.0f;
        
        private readonly IConfiguration m_configuration;
        private readonly ILogger<DTLNSLMoneyModule> m_logger;
        private readonly NSLCertificateVerify m_certVerify;

        #endregion Constant numbers and members.

        public DTLNSLMoneyModule(
            IConfiguration configuration,
            ILogger<DTLNSLMoneyModule> logger,
            NSLCertificateVerify verify
            )
        {
            m_configuration = configuration;
            m_logger = logger;
            m_certVerify = verify;
        }

        #region ISharedRegionModule interface

        public void Initialise()
        {
            if (m_debug) m_logger.LogInformation($"Initialise:");

            m_enable_server = false;
            m_sellEnabled = false;

            // Handle the parameters errors.
            if (m_configuration == null)
            {
                return;
            }

            try
            {
                // [Economy] section
                var economyConfig = m_configuration.GetSection("Economy");

                // If the simulator config does not choose this money module,
                // terminate initialization.
                if (economyConfig.GetValue<string>("EconomyModule") != EconomyModule)
                {
                    m_enable_server = false;
                    m_logger.LogInformation($"The DTL/NSL MoneyModule is disabled (not selected in OpenSim.ini)");
                    return;
                }
                else
                {
                    m_logger.LogInformation($"Initialise: The DTL/NSL MoneyModule is selected");
                }

                // An optional URI pointing to the economy helpers. If missing, the Robust 'economy'
                // setting is used.
                m_moneyHelperURI = economyConfig.GetValue("EconomyHelper", m_moneyHelperURI);
                if (string.IsNullOrEmpty(m_moneyHelperURI))
                {
                    m_logger.LogWarning("EconomyHelper missing; the robust specified economy helper URL is used.");
                }
                else
                {
                    m_logger.LogInformation($"The economy helper URL is {0}", m_moneyHelperURI);
                }

                // A URL pointing to the money server is required. If missing,
                // terminate initialization.
                m_moneyServURL = economyConfig.GetValue("CurrencyServer", m_moneyServURL);
                if (string.IsNullOrEmpty(m_moneyServURL))
                {
                    m_logger.LogInformation($"The required server URL is missing");
                    m_enable_server = false;
                    return;
                }

                // Get the money symbol
                m_moneySymbol = economyConfig.GetValue<string>("CurrencySymbol");
                if (string.IsNullOrEmpty(m_moneySymbol))
                {
                    m_moneySymbol = "$";
                    m_logger.LogWarning("The currency symbol is missing, defaulting to '{0}' ", m_moneySymbol);
                }
                else
                {
                    m_logger.LogInformation($"The currency symbol is set to '{0}' ", m_moneySymbol);
                }

                // Check for send alert on region arrival
                m_entryalert = economyConfig.GetValue<bool>("SendAlert", m_entryalert);

                // Client Certification
                m_certFilename = economyConfig.GetValue("ClientCertFilename", m_certFilename);
                m_certPassword = economyConfig.GetValue("ClientCertPassword", m_certPassword);
                if (m_certFilename != "")
                {
                    m_cert = new X509Certificate2(m_certFilename, m_certPassword);
                    m_logger.LogInformation($"Initialise: Issue Authentication of Client. Cert File is " + m_certFilename);
                }

                // Server Authentication
                m_checkServerCert = economyConfig.GetValue<bool>("CheckServerCert", m_checkServerCert);
                m_cacertFilename = economyConfig.GetValue("CACertFilename", m_cacertFilename);
                if (m_cacertFilename != "")
                {
                    m_certVerify.SetPrivateCA(m_cacertFilename);
                    m_logger.LogInformation($"Execute Authentication of Server. CA Cert File is " + m_cacertFilename);
                }
                else
                {
                    m_checkServerCert = false;
                    m_logger.LogInformation("Initialise: CACertFilename is empty. Therefor, CheckServerCert is forced to false");
                }

                if (m_checkServerCert)
                {
                    m_logger.LogInformation("Initialise: Execute Authentication of Server. CA Cert File is " + m_cacertFilename);
                }
                else
                {
                    m_logger.LogInformation("Initialise: No check Money Server or CACertFilename is empty. CheckServerCert is false.");
                }

                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(m_certVerify.ValidateServerCertificate);

                // Settlement
                m_use_web_settle = economyConfig.GetValue<bool>("SettlementByWeb", m_use_web_settle);
                m_settle_url = economyConfig.GetValue("SettlementURL", m_settle_url);
                m_settle_message = economyConfig.GetValue("SettlementMessage", m_settle_message);

                // Price
                PriceEnergyUnit = economyConfig.GetValue<int>("PriceEnergyUnit", PriceEnergyUnit);
                PriceObjectClaim = economyConfig.GetValue<int>("PriceObjectClaim", PriceObjectClaim);
                PricePublicObjectDecay = economyConfig.GetValue<int>("PricePublicObjectDecay", PricePublicObjectDecay);
                PricePublicObjectDelete = economyConfig.GetValue<int>("PricePublicObjectDelete", PricePublicObjectDelete);
                PriceParcelClaim = economyConfig.GetValue<int>("PriceParcelClaim", PriceParcelClaim);
                PriceParcelClaimFactor = economyConfig.GetValue<float>("PriceParcelClaimFactor", PriceParcelClaimFactor);
                PriceUpload = economyConfig.GetValue<int>("PriceUpload", PriceUpload);
                PriceRentLight = economyConfig.GetValue<int>("PriceRentLight", PriceRentLight);
                PriceObjectRent = economyConfig.GetValue<float>("PriceObjectRent", PriceObjectRent);
                PriceObjectScaleFactor = economyConfig.GetValue<float>("PriceObjectScaleFactor", PriceObjectScaleFactor);
                PriceParcelRent = economyConfig.GetValue<int>("PriceParcelRent", PriceParcelRent);
                PriceGroupCreate = economyConfig.GetValue<int>("PriceGroupCreate", PriceGroupCreate);
                TeleportMinPrice = economyConfig.GetValue<int>("TeleportMinPrice", TeleportMinPrice);
                TeleportPriceExponent = economyConfig.GetValue<float>("TeleportPriceExponent", TeleportPriceExponent);
                EnergyEfficiency = economyConfig.GetValue<float>("EnergyEfficiency", EnergyEfficiency);

                // for HG Avatar
                string avatar_class = economyConfig.GetValue("HGAvatarAs", "HGAvatar").ToLower();
                if (avatar_class == "localavatar") m_hg_avatarClass = (int)AvatarType.LOCAL_AVATAR;
                else if (avatar_class == "guestavatar") m_hg_avatarClass = (int)AvatarType.GUEST_AVATAR;
                else if (avatar_class == "hgavatar") m_hg_avatarClass = (int)AvatarType.HG_AVATAR;
                else if (avatar_class == "foreignavatar") m_hg_avatarClass = (int)AvatarType.FOREIGN_AVATAR;
                else m_hg_avatarClass = (int)AvatarType.UNKNOWN_AVATAR;

                // Enable this money module on this simulator.
                m_enable_server = true;
                m_sellEnabled = economyConfig.GetValue<bool>("SellEnabled", m_sellEnabled);
            }

            // config errors are caught here.
            catch (Exception e)
            {
                m_logger.LogError(e, $"Initialise: Failed to read configuration file");
                m_enable_server = false;
                m_sellEnabled = false;
            }
        }

        public void AddRegion(Scene scene)
        {
            m_logger.LogInformation($"AddRegion:");

            if ((scene == null) || (m_enable_server == false))
            {
                m_logger.LogError($"AddRegion ignored; module is disabled");
                return;
            }
            scene.RegisterModuleInterface<IMoneyModule>(this);  // 競合するモジュールの排除

            lock (m_sceneList)
            {
                if (m_sceneList.Count == 0)
                {
                    if (m_enable_server)
                    {
                        //HttpServer = new BaseHttpServer(9000);
                        //HttpServer.AddStreamHandler(new Region.Framework.Scenes.RegionStatsHandler(scene.RegionInfo));
                        //HttpServer.AddXmlRPCHandler("OnMoneyTransfered", OnMoneyTransferedHandler);
                        //HttpServer.AddXmlRPCHandler("UpdateBalance", BalanceUpdateHandler);
                        //HttpServer.AddXmlRPCHandler("UserAlert", UserAlertHandler);
                        //HttpServer.AddXmlRPCHandler("GetBalance", GetBalanceHandler);                       // added
                        //HttpServer.AddXmlRPCHandler("AddBankerMoney", AddBankerMoneyHandler);               // added
                        //HttpServer.AddXmlRPCHandler("SendMoney", SendMoneyHandler);                         // added
                        //HttpServer.AddXmlRPCHandler("MoveMoney", MoveMoneyHandler);                         // added

                        MainServer.Instance.AddXmlRPCHandler("OnMoneyTransfered", OnMoneyTransferedHandler);
                        MainServer.Instance.AddXmlRPCHandler("UpdateBalance", BalanceUpdateHandler);
                        MainServer.Instance.AddXmlRPCHandler("UserAlert", UserAlertHandler);
                        MainServer.Instance.AddXmlRPCHandler("GetBalance", GetBalanceHandler);              // added
                        MainServer.Instance.AddXmlRPCHandler("AddBankerMoney", AddBankerMoneyHandler);      // added
                        MainServer.Instance.AddXmlRPCHandler("SendMoney", SendMoneyHandler);                // added
                        MainServer.Instance.AddXmlRPCHandler("MoveMoney", MoveMoneyHandler);                // added

                        // * For land sales, buy-currency button, and the insufficent funds flows to operate,
                        // * the economy helper uri needs to be present.
                        // *
                        // * The GMM provides this helper-uri and the currency symbol via the OpenSim Extras.
                        // * Some viewers (Firestorm & Alchemy at time of writing) consume these so this requires no
                        // * configuration to work for a user on a Gloebit enabled region.  For users with other or older viewers,
                        // * the helper-uri will have to be conifgured properly, and if not pointed at a Gloebit enabled sim,
                        // * the grid will have to handle these calls, which it has traditionally done with an XMLRPC server and
                        // * currency.php and landtool.php helper scripts.  That is rather complex, so we recommend that all
                        // * viewers adopt this patch and that grids request that their users update to a viewer with this patch.
                        // * --- Patch Info: http://dev.gloebit.com/blog/Upgrade-Viewer/
                        // * --- Patch Info: https://medium.com/@colosi/multi-currency-support-coming-to-opensim-viewers-cd20e75f7990
                        // * --- Patch Download: http://dev.gloebit.com/opensim/downloads/ColosiOpenSimMultiCurrencySupport.patch
                        // * --- Firestorm Jira: https://jira.phoenixviewer.com/browse/FIRE-21587

                        // These functions can handle the calls to the economy helper-uri if it is configured to point at the sim.
                        // They will enable land purchasing, buy-currency, and insufficient-funds flows.
                        // *NOTE* gloebits can not currently be purchased from a viewer, but this allows Gloebit to control the
                        // messaging in this flow and send users to the Gloebit website for purchasing.
                        //MainServer.AddXmlRPCHandler("getCurrencyQuote", quote_func);
                        //MainServer.AddXmlRPCHandler("buyCurrency", buy_func);
                        //MainServer.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
                        //MainServer.AddXmlRPCHandler("buyLandPrep", landBuy_func);

                        //MainServer.Instance.AddXmlRPCHandler("getCurrencyQuote", quote_func);
                        //MainServer.Instance.AddXmlRPCHandler("buyCurrency", buy_func);
                        //MainServer.Instance.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
                        //MainServer.Instance.AddXmlRPCHandler("buyLandPrep", landBuy_func);

                        //MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler("/currency.php", processPHP));
                        //MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler("/landtool.php", processPHP));
                    }
                }

                if (m_sceneList.ContainsKey(scene.RegionInfo.RegionHandle))
                {
                    m_sceneList[scene.RegionInfo.RegionHandle] = scene;
                }
                else
                {
                    m_sceneList.Add(scene.RegionInfo.RegionHandle, scene);
                }
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;

            // for OpenSim
            scene.EventManager.OnMoneyTransfer += MoneyTransferAction;
            scene.EventManager.OnValidateLandBuy += ValidateLandBuy;
            scene.EventManager.OnLandBuy += processLandBuy;
        }

        public void processPHP(IOSHttpRequest request, IOSHttpResponse response)
        {
            MainServer.Instance.HandleXmlRpcRequests((OSHttpRequest)request, (OSHttpResponse)response, m_rpcHandlers);
        }

        public void RemoveRegion(Scene scene)
        {
            if ((scene == null) || (m_enable_server == false))
            {
                m_logger.LogError($"RemoveRegion ignored; module is disabled");
                return;
            }

            lock (m_sceneList)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;
                scene.EventManager.OnMakeChildAgent -= MakeChildAgent;

                // for OpenSim
                scene.EventManager.OnMoneyTransfer -= MoneyTransferAction;
                scene.EventManager.OnValidateLandBuy -= ValidateLandBuy;
                scene.EventManager.OnLandBuy -= processLandBuy;
            }
        }

        #region local Fund Management

        /// <summary>
        /// Scenes by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene>();


        /// <summary>
        /// Ensures that the agent accounting data is set up in this instance.
        /// </summary>
        /// <param name="agentID"></param>
        private void CheckExistAndRefreshFunds(UUID agentID)
        {
        }

        /// <summary>
        /// Gets the amount of Funds for an agent
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private int GetFundsForAgentID(UUID AgentID)
        {
            int returnfunds = 0;

            return returnfunds;
        }

        // private void SetLocalFundsForAgentID(UUID AgentID, int amount)
        // {
        // }

        #endregion local Fund Management

        #region Utility Helpers

        /// <summary>
        /// Locates a IClientAPI for the client specified
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private IClientAPI LocateClientObject(UUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }
            }
            return null;
        }

        private Scene LocateSceneClientIn(UUID AgentId)
        {
            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    ScenePresence tPresence = _scene.GetScenePresence(AgentId);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            return _scene;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Utility function Gets a Random scene in the instance.  For when which scene exactly you're doing something with doesn't matter
        /// </summary>
        /// <returns></returns>
        public Scene GetRandomScene()
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                    return rs;
            }
            return null;
        }

        /// <summary>
        /// Utility function to get a Scene by RegionID in a module
        /// </summary>
        /// <param name="RegionID"></param>
        /// <returns></returns>
        public Scene GetSceneByUUID(UUID RegionID)
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                {
                    if (rs.RegionInfo.originRegionID == RegionID)
                    {
                        return rs;
                    }
                }
            }
            return null;
        }

        #endregion Utility Helpers

        // Manni Test 15-06-2020 Ende

        public void RegionLoaded(Scene scene)
        {
            m_logger.LogInformation($"RegionLoaded:");
            if ((scene == null) || (m_enable_server == false))
            {
                m_logger.LogError($"RegionLoaded ignored; module is disabled");
                return;
            }

            // Add the currency symbol to the simulator extras.
            ISimulatorFeaturesModule featuresModule = scene.RequestModuleInterface<ISimulatorFeaturesModule>();
            if (featuresModule != null)
            {
                featuresModule.OnSimulatorFeaturesRequest += (UUID x, ref OSDMap y) => OnSimulatorFeaturesRequest(x, ref y, scene);
            }
        }

        private void OnSimulatorFeaturesRequest(UUID agentID, ref OSDMap features, Scene scene)
        {
            UUID regionID = scene.RegionInfo.RegionID;

            // Get or create the extras section of the features map
            OSDMap extrasMap;
            if (features.ContainsKey("OpenSimExtras"))
            {
                extrasMap = (OSDMap)features["OpenSimExtras"];
            }
            else
            {
                extrasMap = new OSDMap();
                features["OpenSimExtras"] = extrasMap;
            }

            // Add our values to the extras map
            extrasMap["currency"] = m_moneySymbol;
            //extrasMap["currency-base-uri"] = GetCurrencyBaseURI(scene);
            if (!string.IsNullOrEmpty(m_moneyHelperURI))
            {
                extrasMap["currency-base-uri"] = m_moneyHelperURI;
            }
        }

        private string GetCurrencyBaseURI(Scene scene)
        {
            return scene.RegionInfo.ServerURI;
        }

        public Type ReplaceableInterface
        {
            //get { return typeof(IMoneyModule); }
            get { return null; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public string Name
        {
            get { return "DTLNSLMoneyModule"; }
        }

        public string EconomyModule
        {
            get { return Name; }
        }

        public void PostInitialise()
        {
            m_logger.LogInformation($"PostInitialise:");
        }

        public void Close()
        {
            m_logger.LogInformation($"Close:");
        }

        #endregion ISharedRegionModule interface

        #region IMoneyModule interface.

        private XmlRpcResponse quote_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            string agentIdStr = requestData["agentId"] as string;
            UUID agentId = UUID.Parse(agentIdStr);
            UUID sessionId = UUID.Parse(requestData["secureSessionId"] as string);
            int amount = (int)requestData["currencyBuy"];

            m_logger.LogInformation($"[MONEYMODULE] quote_func agentId: {0} sessionId: {1} currencyBuy: {2}", agentId, sessionId, amount);
            // foreach(DictionaryEntry e in requestData) { m_logger.LogInformation($"{0}: {1}", e.Key, e.Value); }

            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable quoteResponse = new Hashtable();
            Hashtable currencyResponse = new Hashtable();

            currencyResponse.Add("estimatedCost", amount / 2);
            currencyResponse.Add("currencyBuy", amount);

            quoteResponse.Add("success", true);
            quoteResponse.Add("currency", currencyResponse);

            // TODO - generate a unique confirmation token
            quoteResponse.Add("confirm", "123456789");

            returnval.Value = quoteResponse;
            return returnval;
        }

        private XmlRpcResponse buy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            UUID agentId = UUID.Parse(requestData["agentId"] as string);
            string confirm = requestData["confirm"] as string;
            int currencyBuy = (int)requestData["currencyBuy"];
            int estimatedCost = (int)requestData["estimatedCost"];
            string secureSessionId = requestData["secureSessionId"] as string;

            // currencyBuy:viewerMinorVersion:secureSessionId:viewerBuildVersion:estimatedCost:confirm:agentId:viewerPatchVersion:viewerMajorVersion:viewerChannel:language
            // m_logger.LogInformation($"[MONEYMODULE] buy_func params {0}", String.Join(":", requestData.Keys.Cast<String>()));
            m_logger.LogInformation($"[MONEYMODULE] buy_func agentId {0} confirm {1} currencyBuy {2} estimatedCost {3} secureSessionId {4}",
                agentId, confirm, currencyBuy, estimatedCost, secureSessionId);

            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable();
            returnresp.Add("success", false);
            returnval.Value = returnresp;
            return returnval;
        }

        private XmlRpcResponse preflightBuyLandPrep_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_logger.LogInformation($"[MONEYMODULE] preflightBuyLandPrep_func");
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable membershiplevels = new Hashtable();
            ArrayList levels = new ArrayList();
            Hashtable level = new Hashtable();
            level.Add("id", "00000000-0000-0000-0000-000000000000");
            level.Add("description", "some level");
            levels.Add(level);
            //membershiplevels.Add("levels",levels);

            Hashtable landuse = new Hashtable();
            landuse.Add("upgrade", false);
            landuse.Add("action", "http://invaliddomaininvalid.com/");

            Hashtable currency = new Hashtable();
            currency.Add("estimatedCost", 0);

            Hashtable membership = new Hashtable();
            membershiplevels.Add("upgrade", false);
            membershiplevels.Add("action", "http://invaliddomaininvalid.com/");
            membershiplevels.Add("levels", membershiplevels);

            retparam.Add("success", true);
            retparam.Add("currency", currency);
            retparam.Add("membership", membership);
            retparam.Add("landuse", landuse);
            retparam.Add("confirm", "123456789");

            ret.Value = retparam;

            return ret;
        }

        private XmlRpcResponse landBuy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_logger.LogInformation($"[MONEYMODULE] landBuy_func");
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            // Hashtable requestData = (Hashtable) request.Params[0];

            // UUID agentId = UUID.Zero;
            // int amount = 0;

            retparam.Add("success", true);
            ret.Value = retparam;

            return ret;
        }

        // for LSL llGiveMoney() function
        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount, UUID txn, out string result)
        {
            if (m_debug) m_logger.LogInformation($"ObjectGiveMoney: LSL ObjectGiveMoney. UUID = {0}", objectID.ToString());

            result = string.Empty;
            if (!m_sellEnabled)
            {
                result = "LINDENDOLLAR_INSUFFICIENTFUNDS";
                return false;
            }

            string objName = string.Empty;
            string avatarName = string.Empty;

            SceneObjectPart sceneObj = GetLocatePrim(objectID);
            if (sceneObj == null)
            {
                result = "LINDENDOLLAR_INSUFFICIENTFUNDS";
                return false;
            }
            objName = sceneObj.Name;

            Scene scene = GetLocateScene(toID);
            if (scene != null)
            {
                UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, toID);
                if (account != null)
                {
                    avatarName = account.FirstName + " " + account.LastName;
                }
            }

            bool ret = false;
            string description = String.Format("Object {0} pays {1}", objName, avatarName);

            if (sceneObj.OwnerID == fromID)
            {
                ulong regionHandle = sceneObj.RegionHandle;
                UUID regionUUID = sceneObj.RegionID;
                if (GetLocateClient(fromID) != null)
                {
                    ret = TransferMoney(fromID, toID, amount, (int)TransactionType.ObjectPays, objectID, regionHandle, regionUUID, description);
                }
                else
                {
                    ret = ForceTransferMoney(fromID, toID, amount, (int)TransactionType.ObjectPays, objectID, regionHandle, regionUUID, description);
                }
            }

            if (!ret) result = "LINDENDOLLAR_INSUFFICIENTFUNDS";
            return ret;
        }

        //
        public int UploadCharge
        {
            get { return PriceUpload; }
        }

        //
        public int GroupCreationCharge
        {
            get { return PriceGroupCreate; }
        }

        public int GetBalance(UUID agentID)
        {
            IClientAPI client = GetLocateClient(agentID);
            return QueryBalanceFromMoneyServer(client);
        }

        public bool UploadCovered(UUID agentID, int amount)
        {
            IClientAPI client = GetLocateClient(agentID);

            if (m_enable_server || string.IsNullOrEmpty(m_moneyServURL))
            {
                int balance = QueryBalanceFromMoneyServer(client);
                if (balance >= amount) return true;
            }
            return false;
        }

        public bool AmountCovered(UUID agentID, int amount)
        {
            IClientAPI client = GetLocateClient(agentID);

            if (m_enable_server || string.IsNullOrEmpty(m_moneyServURL))
            {
                int balance = QueryBalanceFromMoneyServer(client);
                if (balance >= amount) return true;
            }
            return false;
        }

        public void ObjectBuy(IClientAPI remoteClient, UUID agentID,
        UUID sessionID, UUID groupID, UUID categoryID,
        uint localID, byte saleType, int salePrice)
        {
            if (!m_sellEnabled)
            {
                remoteClient.SendBlueBoxMessage(UUID.Zero, "", "Buying is not implemented in this version");
                return;
            }

            if (salePrice != 0)
            {
                remoteClient.SendBlueBoxMessage(UUID.Zero, "", "Buying anything for a price other than zero is not implemented");
                return;
            }

            Scene s = LocateSceneClientIn(remoteClient.AgentId);

            // Implmenting base sale data checking here so the default OpenSimulator implementation isn't useless
            // combined with other implementations.  We're actually validating that the client is sending the data
            // that it should.   In theory, the client should already know what to send here because it'll see it when it
            // gets the object data.   If the data sent by the client doesn't match the object, the viewer probably has an
            // old idea of what the object properties are.   Viewer developer Hazim informed us that the base module
            // didn't check the client sent data against the object do any.   Since the base modules are the
            // 'crowning glory' examples of good practice..

            // Validate that the object exists in the scene the user is in
            SceneObjectPart part = s.GetSceneObjectPart(localID);
            if (part == null)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. The object was not found.", false);
                return;
            }

            // Validate that the client sent the price that the object is being sold for
            if (part.SalePrice != salePrice)
            {
                remoteClient.SendAgentAlertMessage("Cannot buy at this price. Buy Failed. If you continue to get this relog.", false);
                return;
            }

            // Validate that the client sent the proper sale type the object has set
            if (part.ObjectSaleType != saleType)
            {
                remoteClient.SendAgentAlertMessage("Cannot buy this way. Buy Failed. If you continue to get this relog.", false);
                return;
            }

            IBuySellModule module = s.RequestModuleInterface<IBuySellModule>();
            if (module != null)
                module.BuyObject(remoteClient, categoryID, localID, saleType, salePrice);
        }

        public void ApplyUploadCharge(UUID agentID, int amount, string text)
        {
            ulong regionHandle = GetLocateScene(agentID).RegionInfo.RegionHandle;
            UUID regionUUID = GetLocateScene(agentID).RegionInfo.RegionID;
            PayMoneyCharge(agentID, amount, (int)TransactionType.UploadCharge, regionHandle, regionUUID, text);
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type)
        {
            ApplyCharge(agentID, amount, type, string.Empty);
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type, string text)
        {
            ulong regionHandle = GetLocateScene(agentID).RegionInfo.RegionHandle;
            UUID regionUUID = GetLocateScene(agentID).RegionInfo.RegionID;
            PayMoneyCharge(agentID, amount, (int)type, regionHandle, regionUUID, text);
        }

        public bool Transfer(UUID fromID, UUID toID, int regionHandle, int amount, MoneyTransactionType type, string text)
        {
            return TransferMoney(fromID, toID, amount, (int)type, UUID.Zero, (ulong)regionHandle, UUID.Zero, text);
        }

        public bool Transfer(UUID fromID, UUID toID, UUID objectID, int amount, MoneyTransactionType type, string text)
        {
            SceneObjectPart sceneObj = GetLocatePrim(objectID);
            if (sceneObj == null) return false;

            ulong regionHandle = sceneObj.ParentGroup.Scene.RegionInfo.RegionHandle;
            UUID regionUUID = sceneObj.ParentGroup.Scene.RegionInfo.RegionID;
            return TransferMoney(fromID, toID, amount, (int)type, objectID, (ulong)regionHandle, regionUUID, text);
        }

        // for 0.8.3 over
        public void MoveMoney(UUID fromAgentID, UUID toAgentID, int amount, string text)
        {
            ForceTransferMoney(fromAgentID, toAgentID, amount, (int)TransactionType.MoveMoney, UUID.Zero, (ulong)0, UUID.Zero, text);
        }

        // for 0.9.1 over
        public bool MoveMoney(UUID fromAgentID, UUID toAgentID, int amount, MoneyTransactionType type, string text)
        {
            bool ret = ForceTransferMoney(fromAgentID, toAgentID, amount, (int)type, UUID.Zero, (ulong)0, UUID.Zero, text);
            return ret;
        }

        #endregion IMoneyModule interface.

        #region MoneyModule event handlers

        //
        private void OnNewClient(IClientAPI client)
        {
            m_logger.LogInformation($"OnNewClient");

            client.OnEconomyDataRequest += OnEconomyDataRequest;
            client.OnLogout += ClientClosed;

            client.OnMoneyBalanceRequest += OnMoneyBalanceRequest;
            client.OnRequestPayPrice += OnRequestPayPrice;
            client.OnObjectBuy += OnObjectBuy;
        }

        public void OnMakeRootAgent(ScenePresence agent)
        {
            m_logger.LogInformation($"OnMakeRootAgent:");

            int balance = 0;
            IClientAPI client = agent.ControllingClient;

            if (m_entryalert)
            {
                // check for hg visitor here...
                string hgmsg = "Hypergrid visitors, you are able to transact in this currency.";
                client.SendAgentAlertMessage("This region transacts " + m_moneySymbol + " currency. " + hgmsg, false);
            }

            m_enable_server = LoginMoneyServer(agent, out balance);
            client.SendMoneyBalance(UUID.Zero, true, new byte[0], balance, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);

            // client.OnMoneyBalanceRequest += OnMoneyBalanceRequest;
            // client.OnRequestPayPrice += OnRequestPayPrice;
            // client.OnObjectBuy += OnObjectBuy;
        }

        // for OnClientClosed event
        private void ClientClosed(IClientAPI client)
        {
            m_logger.LogInformation($"ClientClosed:");

            if (m_enable_server && client != null)
            {
                LogoffMoneyServer(client);
            }
        }

        /// <summary>
        /// Event called Economy Data Request handler.
        /// </summary>
        /// <param name="agentId"></param>
        public void EconomyDataRequestHandler(IClientAPI user)
        {
            Scene s = (Scene)user.Scene;

            user.SendEconomyData(EnergyEfficiency, s.RegionInfo.ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                                 PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                                 PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                                 TeleportMinPrice, TeleportPriceExponent);
        }

        // for OnMakeChildAgent event
        private void MakeChildAgent(ScenePresence avatar)
        {
            if (m_debug) m_logger.LogInformation($"MakeChildAgent:");
        }

        // for OnMoneyTransfer event
        private void MoneyTransferAction(Object sender, EventManager.MoneyTransferArgs moneyEvent)
        {
            if (m_debug) m_logger.LogInformation($"MoneyTransferAction: type = {0}", moneyEvent.transactiontype);

            if (!m_sellEnabled) return;

            // Check the money transaction is necessary.
            if (moneyEvent.sender == moneyEvent.receiver)
            {
                return;
            }

            UUID receiver = moneyEvent.receiver;
            // Pay for the object.
            if (moneyEvent.transactiontype == (int)TransactionType.PayObject)
            {
                SceneObjectPart sceneObj = GetLocatePrim(moneyEvent.receiver);
                if (sceneObj != null)
                {
                    receiver = sceneObj.OwnerID;
                }
                else
                {
                    return;
                }
            }

            // Before paying for the object, save the object local ID for current transaction.
            UUID objectID = UUID.Zero;
            ulong regionHandle = 0;
            UUID regionUUID = UUID.Zero;

            if (sender is Scene)
            {
                Scene scene = (Scene)sender;
                regionHandle = scene.RegionInfo.RegionHandle;
                regionUUID = scene.RegionInfo.RegionID;

                if (moneyEvent.transactiontype == (int)TransactionType.PayObject)
                {
                    objectID = scene.GetSceneObjectPart(moneyEvent.receiver).UUID;
                }
            }

            TransferMoney(moneyEvent.sender, receiver, moneyEvent.amount, moneyEvent.transactiontype, objectID, regionHandle, regionUUID, "OnMoneyTransfer event");
            return;
        }

        // for OnValidateLandBuy event
        private void ValidateLandBuy(Object sender, EventManager.LandBuyArgs landBuyEvent)
        {
            if (m_debug) m_logger.LogInformation($"ValidateLandBuy:");

            IClientAPI senderClient = GetLocateClient(landBuyEvent.agentId);
            if (senderClient != null)
            {
                int balance = QueryBalanceFromMoneyServer(senderClient);
                if (balance >= landBuyEvent.parcelPrice)
                {
                    lock (landBuyEvent)
                    {
                        landBuyEvent.economyValidated = true;
                    }
                }
            }
            return;
        }

        // for LandBuy even
        private void processLandBuy(Object sender, EventManager.LandBuyArgs landBuyEvent)
        {
            if (m_debug) m_logger.LogInformation($"processLandBuy:");

            if (!m_sellEnabled) return;

            lock (landBuyEvent)
            {
                if (landBuyEvent.economyValidated == true && landBuyEvent.transactionID == 0)
                {
                    landBuyEvent.transactionID = Util.UnixTimeSinceEpoch();

                    ulong parcelID = (ulong)landBuyEvent.parcelLocalID;
                    UUID regionUUID = UUID.Zero;
                    if (sender is Scene) regionUUID = ((Scene)sender).RegionInfo.RegionID;

                    if (TransferMoney(landBuyEvent.agentId, landBuyEvent.parcelOwnerID,
                                      landBuyEvent.parcelPrice, (int)TransactionType.LandSale, regionUUID, parcelID, regionUUID, "Land Purchase"))
                    {
                        landBuyEvent.amountDebited = landBuyEvent.parcelPrice;
                    }
                }
            }
            return;
        }

        // for OnObjectBuy event
        public void OnObjectBuy(IClientAPI remoteClient, UUID agentID, UUID sessionID,
                                UUID groupID, UUID categoryID, uint localID, byte saleType, int salePrice)
        {
            m_logger.LogInformation($"OnObjectBuy: agent = {0}, {1}", agentID, remoteClient.AgentId);

            // Handle the parameters error.
            if (!m_sellEnabled) return;
            if (remoteClient == null || salePrice < 0) return;

            // Get the balance from money server.
            int balance = QueryBalanceFromMoneyServer(remoteClient);
            if (balance < salePrice)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. You don't have sufficient funds", false);
                return;
            }

            Scene scene = GetLocateScene(remoteClient.AgentId);
            if (scene != null)
            {
                SceneObjectPart sceneObj = scene.GetSceneObjectPart(localID);
                if (sceneObj != null)
                {
                    IBuySellModule mod = scene.RequestModuleInterface<IBuySellModule>();
                    if (mod != null)
                    {
                        UUID receiverId = sceneObj.OwnerID;
                        ulong regionHandle = sceneObj.RegionHandle;
                        UUID regionUUID = sceneObj.RegionID;
                        bool ret = false;
                        //
                        if (salePrice >= 0)
                        {
                            if (!string.IsNullOrEmpty(m_moneyServURL))
                            {
                                ret = TransferMoney(remoteClient.AgentId, receiverId, salePrice,
                                                (int)TransactionType.PayObject, sceneObj.UUID, regionHandle, regionUUID, "Object Buy");
                            }
                            else if (salePrice == 0)
                            {   // amount is 0 with No Money Server
                                ret = true;
                            }
                        }
                        if (ret)
                        {
                            mod.BuyObject(remoteClient, categoryID, localID, saleType, salePrice);
                        }
                    }
                }
                else
                {
                    remoteClient.SendAgentAlertMessage("Unable to buy now. The object was not found", false);
                    return;
                }
            }
            return;
        }

        /// <summary>
        /// Sends the the stored money balance to the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        /// <param name="SessionID"></param>
        /// <param name="TransactionID"></param>
        private void OnMoneyBalanceRequest(IClientAPI client, UUID agentID, UUID SessionID, UUID TransactionID)
        {
            m_logger.LogInformation($"OnMoneyBalanceRequest:");

            if (client.AgentId == agentID && client.SessionId == SessionID)
            {
                int balance = 0;
                //
                if (m_enable_server)
                {
                    balance = QueryBalanceFromMoneyServer(client);
                }

                client.SendMoneyBalance(TransactionID, true, new byte[0], balance, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
            }
            else
            {
                client.SendAlertMessage("Unable to send your money balance");
            }
        }

        private void OnRequestPayPrice(IClientAPI client, UUID objectID)
        {
            m_logger.LogInformation($"OnRequestPayPrice:");

            Scene scene = GetLocateScene(client.AgentId);
            if (scene == null) return;
            SceneObjectPart sceneObj = scene.GetSceneObjectPart(objectID);
            if (sceneObj == null) return;
            SceneObjectGroup group = sceneObj.ParentGroup;
            SceneObjectPart root = group.RootPart;

            client.SendPayPrice(objectID, root.PayPrice);
        }

        //
        //private void OnEconomyDataRequest(UUID agentId)
        private void OnEconomyDataRequest(IClientAPI user)
        {
            if (m_debug) m_logger.LogInformation($"OnEconomyDataRequest:");
            //IClientAPI user = GetLocateClient(agentId);

            if (user != null)
            {
                if (m_enable_server || string.IsNullOrEmpty(m_moneyServURL))
                {
                    //Scene s = GetLocateScene(user.AgentId);
                    Scene s = (Scene)user.Scene;
                    user.SendEconomyData(EnergyEfficiency, s.RegionInfo.ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                                     PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                                     PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                                     TeleportMinPrice, TeleportPriceExponent);
                }
            }
        }

        #endregion MoneyModule event handlers

        #region MoneyModule XML-RPC Handler

        // "OnMoneyTransfered" RPC from MoneyServer
        public XmlRpcResponse OnMoneyTransferedHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_logger.LogInformation($"OnMoneyTransferedHandler:");

            bool ret = false;

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") && requestParam.Contains("clientSessionID") && requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);

                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = GetLocateClient(clientUUID);
                        string sessionid = (string)requestParam["clientSessionID"];
                        string secureid = (string)requestParam["clientSecureSessionID"];
                        if (client != null && secureid == client.SecureSessionId.ToString() && (sessionid == UUID.Zero.ToString() || sessionid == client.SessionId.ToString()))
                        {
                            if (requestParam.Contains("transactionType") && requestParam.Contains("objectID") && requestParam.Contains("amount"))
                            {
                                if (m_debug) m_logger.LogInformation($"OnMoneyTransferedHandler: type = {0}", requestParam["transactionType"]);

                                // Pay for the object.
                                if ((int)requestParam["transactionType"] == (int)TransactionType.PayObject)
                                {
                                    // Send notify to the client(viewer) for Money Event Trigger.
                                    ObjectPaid handlerOnObjectPaid = OnObjectPaid;
                                    if (handlerOnObjectPaid != null)
                                    {
                                        UUID objectID = UUID.Zero;
                                        UUID.TryParse((string)requestParam["objectID"], out objectID);
                                        handlerOnObjectPaid(objectID, clientUUID, (int)requestParam["amount"]); // call Script Engine for LSL money()
                                    }
                                    ret = true;
                                }
                            }
                        }
                    }
                }
            }

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;

            if (!ret)
            {
                m_logger.LogError($"OnMoneyTransferedHandler: Transaction is failed. MoneyServer will rollback");
            }
            resp.Value = paramTable;

            return resp;
        }

        // "UpdateBalance" RPC from MoneyServer or Script
        public XmlRpcResponse BalanceUpdateHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            if (m_debug) m_logger.LogInformation($"BalanceUpdateHandler:");

            bool ret = false;

            #region Update the balance from money server.

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") && requestParam.Contains("clientSessionID") && requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);
                    //
                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = GetLocateClient(clientUUID);
                        string sessionid = (string)requestParam["clientSessionID"];
                        string secureid = (string)requestParam["clientSecureSessionID"];
                        if (client != null && secureid == client.SecureSessionId.ToString() && (sessionid == UUID.Zero.ToString() || sessionid == client.SessionId.ToString()))
                        {
                            //
                            if (requestParam.Contains("Balance"))
                            {
                                // Send notify to the client.
                                string msg = "";
                                if (requestParam.Contains("Message")) msg = (string)requestParam["Message"];
                                client.SendMoneyBalance(UUID.Random(), true, Utils.StringToBytes(msg), (int)requestParam["Balance"],
                                                                                    0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
                                // Dialog
                                if (msg != "")
                                {
                                    Scene scene = (Scene)client.Scene;
                                    IDialogModule dlg = scene.RequestModuleInterface<IDialogModule>();
                                    dlg.SendAlertToUser(client.AgentId, msg);
                                }
                                ret = true;
                            }
                        }
                    }
                }
            }

            #endregion Update the balance from money server.

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;

            if (!ret)
            {
                m_logger.LogError($"BalanceUpdateHandler: Cannot update client balance from MoneyServer");
            }
            resp.Value = paramTable;

            return resp;
        }

        // "UserAlert" RPC from Script
        public XmlRpcResponse UserAlertHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            if (m_debug) m_logger.LogInformation($"UserAlertHandler:");

            bool ret = false;

            #region confirm the request and show the notice from money server.

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") && requestParam.Contains("clientSessionID") && requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);
                    //
                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = GetLocateClient(clientUUID);
                        string sessionid = (string)requestParam["clientSessionID"];
                        string secureid = (string)requestParam["clientSecureSessionID"];
                        if (client != null && secureid == client.SecureSessionId.ToString() && (sessionid == UUID.Zero.ToString() || sessionid == client.SessionId.ToString()))
                        {
                            if (requestParam.Contains("Description"))
                            {
                                string description = (string)requestParam["Description"];
                                // Show the notice dialog with money server message.
                                GridInstantMessage gridMsg = new GridInstantMessage(null, UUID.Zero, "MonyServer", new UUID(clientUUID.ToString()),
                                                                    (byte)InstantMessageDialog.MessageFromAgent, description, false, new Vector3());
                                client.SendInstantMessage(gridMsg);
                                ret = true;
                            }
                        }
                    }
                }
            }
            //

            #endregion confirm the request and show the notice from money server.

            // Send the response to money server.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;

            resp.Value = paramTable;
            return resp;
        }

        // "GetBalance" RPC from Script
        public XmlRpcResponse GetBalanceHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            if (m_debug) m_logger.LogInformation($"GetBalanceHandler:");

            bool ret = false;
            int balance = -1;

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("clientUUID") && requestParam.Contains("clientSessionID") && requestParam.Contains("clientSecureSessionID"))
                {
                    UUID clientUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out clientUUID);
                    //
                    if (clientUUID != UUID.Zero)
                    {
                        IClientAPI client = GetLocateClient(clientUUID);
                        string sessionid = (string)requestParam["clientSessionID"];
                        string secureid = (string)requestParam["clientSecureSessionID"];
                        if (client != null && secureid == client.SecureSessionId.ToString() && (sessionid == UUID.Zero.ToString() || sessionid == client.SessionId.ToString()))
                        {
                            balance = QueryBalanceFromMoneyServer(client);
                        }
                    }
                }
            }

            // Send the response to caller.
            if (balance < 0)
            {
                m_logger.LogError($"GetBalanceHandler: GetBalance transaction is failed");
                ret = false;
            }

            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;
            paramTable["balance"] = balance;
            resp.Value = paramTable;

            return resp;
        }

        // "AddBankerMoney" RPC from Script
        public XmlRpcResponse AddBankerMoneyHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            if (m_debug) m_logger.LogInformation($"AddBankerMoneyHandler:");

            bool ret = false;

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];

                if (requestParam.Contains("clientUUID") && requestParam.Contains("clientSessionID") && requestParam.Contains("clientSecureSessionID"))
                {
                    UUID bankerUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["clientUUID"], out bankerUUID);
                    //
                    if (bankerUUID != UUID.Zero)
                    {
                        IClientAPI client = GetLocateClient(bankerUUID);
                        string sessionid = (string)requestParam["clientSessionID"];
                        string secureid = (string)requestParam["clientSecureSessionID"];
                        if (client != null && secureid == client.SecureSessionId.ToString() && (sessionid == UUID.Zero.ToString() || sessionid == client.SessionId.ToString()))
                        {
                            if (requestParam.Contains("amount"))
                            {
                                Scene scene = (Scene)client.Scene;
                                int amount = (int)requestParam["amount"];
                                ulong regionHandle = scene.RegionInfo.RegionHandle;
                                UUID regionUUID = scene.RegionInfo.RegionID;
                                ret = AddBankerMoney(bankerUUID, amount, regionHandle, regionUUID);

                                if (m_use_web_settle && m_settle_user)
                                {
                                    ret = true;
                                    IDialogModule dlg = scene.RequestModuleInterface<IDialogModule>();
                                    if (dlg != null)
                                    {
                                        dlg.SendUrlToUser(bankerUUID, "SYSTEM", UUID.Zero, UUID.Zero, false, m_settle_message, m_settle_url);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!ret) m_logger.LogError($"AddBankerMoneyHandler: Add Banker Money transaction is failed");

            // Send the response to caller.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["settle"] = false;
            paramTable["success"] = ret;

            if (m_use_web_settle && m_settle_user) paramTable["settle"] = true;
            resp.Value = paramTable;

            return resp;
        }

        // "SendMoney" RPC from Script
        public XmlRpcResponse SendMoneyHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            if (m_debug) m_logger.LogInformation($"SendMoneyHandler:");

            bool ret = false;

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if (requestParam.Contains("agentUUID") && requestParam.Contains("secretAccessCode"))
                {
                    UUID agentUUID = UUID.Zero;
                    UUID.TryParse((string)requestParam["agentUUID"], out agentUUID);

                    if (agentUUID != UUID.Zero)
                    {
                        if (requestParam.Contains("amount"))
                        {
                            int amount = (int)requestParam["amount"];
                            int type = -1;
                            if (requestParam.Contains("type")) type = (int)requestParam["type"];
                            string secretCode = (string)requestParam["secretAccessCode"];
                            string scriptIP = remoteClient.Address.ToString();

                            MD5 md5 = MD5.Create();
                            byte[] code = md5.ComputeHash(ASCIIEncoding.Default.GetBytes(secretCode + "_" + scriptIP));
                            string hash = BitConverter.ToString(code).ToLower().Replace("-", "");
                            if (m_debug) m_logger.LogInformation($"SendMoneyHandler: SecretCode: {0} + {1} = {2}", secretCode, scriptIP, hash);
                            ret = SendMoneyTo(agentUUID, amount, type, hash);
                        }
                    }
                    else
                    {
                        m_logger.LogError($"SendMoneyHandler: amount is missed");
                    }
                }
                else
                {
                    if (!requestParam.Contains("agentUUID"))
                    {
                        m_logger.LogError($"SendMoneyHandler: agentUUID is missed");
                    }
                    if (!requestParam.Contains("secretAccessCode"))
                    {
                        m_logger.LogError($"SendMoneyHandler: secretAccessCode is missed");
                    }
                }
            }
            else
            {
                m_logger.LogError($"SendMoneyHandler: Params count is under 0");
            }

            if (!ret) m_logger.LogError($"SendMoneyHandler: Send Money transaction is failed");

            // Send the response to caller.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;

            resp.Value = paramTable;

            return resp;
        }

        // "MoveMoney" RPC from Script
        public XmlRpcResponse MoveMoneyHandler(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            if (m_debug) m_logger.LogInformation($"MoveMoneyHandler:");

            bool ret = false;

            if (request.Params.Count > 0)
            {
                Hashtable requestParam = (Hashtable)request.Params[0];
                if ((requestParam.Contains("fromUUID") || requestParam.Contains("toUUID")) && requestParam.Contains("secretAccessCode"))
                {
                    UUID fromUUID = UUID.Zero;
                    UUID toUUID = UUID.Zero;    // UUID.Zero means System
                    if (requestParam.Contains("fromUUID")) UUID.TryParse((string)requestParam["fromUUID"], out fromUUID);
                    if (requestParam.Contains("toUUID")) UUID.TryParse((string)requestParam["toUUID"], out toUUID);

                    if (requestParam.Contains("amount"))
                    {
                        int amount = (int)requestParam["amount"];
                        string secretCode = (string)requestParam["secretAccessCode"];
                        string scriptIP = remoteClient.Address.ToString();

                        MD5 md5 = MD5.Create();
                        byte[] code = md5.ComputeHash(ASCIIEncoding.Default.GetBytes(secretCode + "_" + scriptIP));
                        string hash = BitConverter.ToString(code).ToLower().Replace("-", "");
                        if (m_debug) m_logger.LogInformation($"MoveMoneyHandler: SecretCode: {0} + {1} = {2}", secretCode, scriptIP, hash);
                        ret = MoveMoneyFromTo(fromUUID, toUUID, amount, hash);
                    }
                    else
                    {
                        m_logger.LogError($"MoveMoneyHandler: amount is missed");
                    }
                }
                else
                {
                    if (!requestParam.Contains("fromUUID") && !requestParam.Contains("toUUID"))
                    {
                        m_logger.LogError($"MoveMoneyHandler: fromUUID and toUUID are missed");
                    }
                    if (!requestParam.Contains("secretAccessCode"))
                    {
                        m_logger.LogError($"MoveMoneyHandler: secretAccessCode is missed");
                    }
                }
            }
            else
            {
                m_logger.LogError($"MoveMoneyHandler: Params count is under 0");
            }

            if (!ret) m_logger.LogError($"MoveMoneyHandler: Move Money transaction is failed");

            // Send the response to caller.
            XmlRpcResponse resp = new XmlRpcResponse();
            Hashtable paramTable = new Hashtable();
            paramTable["success"] = ret;

            resp.Value = paramTable;

            return resp;
        }

        #endregion MoneyModule XML-RPC Handler

        #region MoneyModule private help functions

        /// <summary>
        /// Transfer the money from one user to another. Need to notify money server to update.
        /// </summary>
        /// <param name="amount">
        /// The amount of money.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool TransferMoney(UUID sender, UUID receiver, int amount, int type, UUID objectID, ulong regionHandle, UUID regionUUID, string description)
        {
            if (m_debug) m_logger.LogInformation($"TransferMoney:");

            bool ret = false;
            IClientAPI senderClient = GetLocateClient(sender);

            // Handle the illegal transaction.
            // receiverClient could be null.
            if (senderClient == null)
            {
                m_logger.LogInformation($"TransferMoney: Client {0} not found", sender.ToString());
                return false;
            }

            if (QueryBalanceFromMoneyServer(senderClient) < amount)
            {
                m_logger.LogInformation($"TransferMoney: No insufficient balance in client [{0}]", sender.ToString());
                return false;
            }

            #region Send transaction request to money server and parse the resultes.

            if (m_enable_server)
            {
                string objName = string.Empty;
                SceneObjectPart sceneObj = GetLocatePrim(objectID);
                if (sceneObj != null) objName = sceneObj.Name;

                // Fill parameters for money transfer XML-RPC.
                Hashtable paramTable = new Hashtable();
                paramTable["senderID"] = sender.ToString();
                paramTable["receiverID"] = receiver.ToString();
                paramTable["senderSessionID"] = senderClient.SessionId.ToString();
                paramTable["senderSecureSessionID"] = senderClient.SecureSessionId.ToString();
                paramTable["transactionType"] = type;
                paramTable["objectID"] = objectID.ToString();
                paramTable["objectName"] = objName;
                paramTable["regionHandle"] = regionHandle.ToString();
                paramTable["regionUUID"] = regionUUID.ToString();
                paramTable["amount"] = amount;
                paramTable["description"] = description;

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "TransferMoney");

                // Handle the return values from Money Server.
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                }
                else m_logger.LogError($"TransferMoney: Can not money transfer request from [{0}] to [{1}]", sender.ToString(), receiver.ToString());
            }
            else if (m_debug) m_logger.LogError($"TransferMoney: Money Server is not available!!");

            #endregion Send transaction request to money server and parse the resultes.

            return ret;
        }

        /// <summary>
        /// Force transfer the money from one user to another.
        /// This function does not check sender login.
        /// Need to notify money server to update.
        /// </summary>
        /// <param name="amount">
        /// The amount of money.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool ForceTransferMoney(UUID sender, UUID receiver, int amount, int type, UUID objectID, ulong regionHandle, UUID regionUUID, string description)
        {
            if (m_debug) m_logger.LogInformation($"ForceTransferMoney:");

            bool ret = false;

            #region Force send transaction request to money server and parse the resultes.

            if (m_enable_server)
            {
                string objName = string.Empty;
                SceneObjectPart sceneObj = GetLocatePrim(objectID);
                if (sceneObj != null) objName = sceneObj.Name;

                // Fill parameters for money transfer XML-RPC.
                Hashtable paramTable = new Hashtable();
                paramTable["senderID"] = sender.ToString();
                paramTable["receiverID"] = receiver.ToString();
                paramTable["transactionType"] = type;
                paramTable["objectID"] = objectID.ToString();
                paramTable["objectName"] = objName;
                paramTable["regionHandle"] = regionHandle.ToString();
                paramTable["regionUUID"] = regionUUID.ToString();
                paramTable["amount"] = amount;
                paramTable["description"] = description;

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "ForceTransferMoney");

                // Handle the return values from Money Server.
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                }
                else m_logger.LogError($"ForceTransferMoney: Can not money force transfer request from [{0}] to [{1}]", sender.ToString(), receiver.ToString());
            }
            else if (m_debug) m_logger.LogError($"ForceTransferMoney: Money Server is not available!!");

            #endregion Force send transaction request to money server and parse the resultes.

            return ret;
        }

        /// <summary>
        /// Send the money to avatar. Need to notify money server to update.
        /// </summary>
        /// <param name="amount">
        /// The amount of money.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool SendMoneyTo(UUID avatarID, int amount, int type, string secretCode)
        {
            if (m_debug) m_logger.LogInformation($"SendMoneyTo:");

            bool ret = false;

            if (m_enable_server)
            {
                // Fill parameters for money transfer XML-RPC.
                if (type < 0) type = (int)TransactionType.ReferBonus;
                Hashtable paramTable = new Hashtable();
                paramTable["receiverID"] = avatarID.ToString();
                paramTable["transactionType"] = type;
                paramTable["amount"] = amount;
                paramTable["secretAccessCode"] = secretCode;
                paramTable["description"] = "Bonus to Avatar";

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "SendMoney");

                // Handle the return values from Money Server.
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                    else m_logger.LogError($"SendMoneyTo: Fail Message is {0}", resultTable["message"]);
                }
                else m_logger.LogError($"SendMoneyTo: Money Server is not responce");
            }
            else if (m_debug) m_logger.LogError($"SendMoneyTo: Money Server is not available!!");

            return ret;
        }

        /// <summary>
        /// Move the money from avatar to other avatar. Need to notify money server to update.
        /// </summary>
        /// <param name="amount">
        /// The amount of money.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool MoveMoneyFromTo(UUID senderID, UUID receiverID, int amount, string secretCode)
        {
            if (m_debug) m_logger.LogInformation($"MoveMoneyFromTo:");

            bool ret = false;

            if (m_enable_server)
            {
                // Fill parameters for money transfer XML-RPC.
                Hashtable paramTable = new Hashtable();
                paramTable["senderID"] = senderID.ToString();
                paramTable["receiverID"] = receiverID.ToString();
                paramTable["transactionType"] = (int)TransactionType.MoveMoney;
                paramTable["amount"] = amount;
                paramTable["secretAccessCode"] = secretCode;
                paramTable["description"] = "Move Money";

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "MoveMoney");

                // Handle the return values from Money Server.
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                    else m_logger.LogError($"MoveMoneyFromTo: Fail Message is {0}", resultTable["message"]);
                }
                else m_logger.LogError($"MoveMoneyFromTo: Money Server is not responce");
            }
            else if (m_debug) m_logger.LogError($"MoveMoneyFromTo: Money Server is not available!!");

            return ret;
        }

        /// <summary>
        /// Add the money to banker avatar. Need to notify money server to update.
        /// </summary>
        /// <param name="amount">
        /// The amount of money.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool AddBankerMoney(UUID bankerID, int amount, ulong regionHandle, UUID regionUUID)
        {
            if (m_debug) m_logger.LogInformation($"AddBankerMoney:");

            bool ret = false;
            m_settle_user = false;

            if (m_enable_server)
            {
                // Fill parameters for money transfer XML-RPC.
                Hashtable paramTable = new Hashtable();
                paramTable["bankerID"] = bankerID.ToString();
                paramTable["transactionType"] = (int)TransactionType.BuyMoney;
                paramTable["amount"] = amount;
                paramTable["regionHandle"] = regionHandle.ToString();
                paramTable["regionUUID"] = regionUUID.ToString();
                paramTable["description"] = "Add Money to Avatar";

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "AddBankerMoney");

                // Handle the return values from Money Server.
                if (resultTable != null)
                {
                    if (resultTable.Contains("success") && (bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                    else
                    {
                        if (resultTable.Contains("banker"))
                        {
                            m_settle_user = !(bool)resultTable["banker"]; // If avatar is not banker, Web Settlement is used.
                            if (m_settle_user && m_use_web_settle) m_logger.LogError($"AddBankerMoney: Avatar is not Banker. Web Settlemrnt is used.");
                        }
                        else m_logger.LogError($"AddBankerMoney: Fail Message {0}", resultTable["message"]);
                    }
                }
                else m_logger.LogError($"AddBankerMoney: Money Server is not responding");
            }
            else if (m_debug) m_logger.LogError($"AddBankerMoney: Money Server is not available!!");

            return ret;
        }

        /// <summary>
        /// Pay the money of charge.
        /// </summary>
        /// <param name="amount">
        /// The amount of money.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool PayMoneyCharge(UUID sender, int amount, int type, ulong regionHandle, UUID regionUUID, string description)
        {
            if (m_debug) m_logger.LogInformation($"PayMoneyCharge:");

            bool ret = false;
            IClientAPI senderClient = GetLocateClient(sender);

            // Handle the illegal transaction.
            // receiverClient could be null.
            if (senderClient == null)
            {
                m_logger.LogInformation($"PayMoneyCharge: Client {0} is not found", sender.ToString());
                return false;
            }

            if (QueryBalanceFromMoneyServer(senderClient) < amount)
            {
                m_logger.LogInformation($"PayMoneyCharge: No insufficient balance in client [{0}]", sender.ToString());
                return false;
            }

            #region Send transaction request to money server and parse the resultes.

            if (m_enable_server)
            {
                // Fill parameters for money transfer XML-RPC.
                Hashtable paramTable = new Hashtable();
                paramTable["senderID"] = sender.ToString();
                paramTable["senderSessionID"] = senderClient.SessionId.ToString();
                paramTable["senderSecureSessionID"] = senderClient.SecureSessionId.ToString();
                paramTable["transactionType"] = type;
                paramTable["amount"] = amount;
                paramTable["regionHandle"] = regionHandle.ToString();
                paramTable["regionUUID"] = regionUUID.ToString();
                paramTable["description"] = description;

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "PayMoneyCharge");

                // Handle the return values from Money Server.
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                }
                else m_logger.LogError($"PayMoneyCharge: Can not pay money of charge request from [{0}]", sender.ToString());
            }
            else if (m_debug) m_logger.LogError($"PayMoneyCharge: Money Server is not available!!");

            #endregion Send transaction request to money server and parse the resultes.

            return ret;
        }

        private int QueryBalanceFromMoneyServer(IClientAPI client)
        {
            if (m_debug) m_logger.LogInformation($"QueryBalanceFromMoneyServer:");

            int balance = 0;

            #region Send the request to get the balance from money server for cilent.

            if (client != null)
            {
                if (m_enable_server)
                {
                    Hashtable paramTable = new Hashtable();
                    paramTable["clientUUID"] = client.AgentId.ToString();
                    paramTable["clientSessionID"] = client.SessionId.ToString();
                    paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();

                    // Generate the request for transfer.
                    Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "GetBalance");

                    // Handle the return result
                    if (resultTable != null && resultTable.Contains("success"))
                    {
                        if ((bool)resultTable["success"] == true)
                        {
                            balance = (int)resultTable["clientBalance"];
                        }
                    }
                }
                else
                {
                    if (m_moneyServer.ContainsKey(client.AgentId))
                    {
                        balance = m_moneyServer[client.AgentId];
                    }
                }
            }

            #endregion Send the request to get the balance from money server for cilent.

            return balance;
        }

        /// <summary>
        /// Login the money server when the new client login.
        /// </summary>
        /// <param name="userID">
        /// Indicate user ID of the new client.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool LoginMoneyServer(ScenePresence avatar, out int balance)
        {
            if (m_debug) m_logger.LogInformation($"LoginMoneyServer:");

            balance = 0;
            bool ret = false;
            bool isNpc = avatar.IsNPC;

            IClientAPI client = avatar.ControllingClient;

            #region Send money server the client info for login.

            if (!string.IsNullOrEmpty(m_moneyServURL))
            {
                Scene scene = (Scene)client.Scene;
                string userName = string.Empty;

                // Get the username for the login user.
                if (client.Scene is Scene)
                {
                    if (scene != null)
                    {
                        UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, client.AgentId);
                        if (account != null)
                        {
                            userName = account.FirstName + " " + account.LastName;
                        }
                    }
                }

                //////////////////////////////////////////////////////////////
                // User Universal Identifer for Grid Avatar, HG Avatar or NPC
                string universalID = string.Empty;
                string firstName = string.Empty;
                string lastName = string.Empty;
                string serverURL = string.Empty;
                int avatarType = (int)AvatarType.LOCAL_AVATAR;
                int avatarClass = (int)AvatarType.LOCAL_AVATAR;

                AgentCircuitData agent = scene.AuthenticateHandler.GetAgentCircuitData(client.AgentId);

                if (agent != null)
                {
                    universalID = Util.ProduceUserUniversalIdentifier(agent);
                    if (!String.IsNullOrEmpty(universalID))
                    {
                        UUID uuid;
                        string tmp;
                        Util.ParseUniversalUserIdentifier(universalID, out uuid, out serverURL, out firstName, out lastName, out tmp);
                    }
                    // if serverURL is empty, avatar is a NPC
                    if (isNpc || String.IsNullOrEmpty(serverURL))
                    {
                        avatarType = (int)AvatarType.NPC_AVATAR;
                    }
                    //
                    if ((agent.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0 || String.IsNullOrEmpty(userName))
                    {
                        avatarType = (int)AvatarType.HG_AVATAR;
                    }
                }
                if (String.IsNullOrEmpty(userName))
                {
                    userName = firstName + " " + lastName;
                }

                //
                avatarClass = avatarType;
                if (avatarType == (int)AvatarType.NPC_AVATAR) return false;
                if (avatarType == (int)AvatarType.HG_AVATAR) avatarClass = m_hg_avatarClass;

                //
                // Login the Money Server.
                Hashtable paramTable = new Hashtable();
                paramTable["openSimServIP"] = scene.RegionInfo.ServerURI.Replace(scene.RegionInfo.InternalEndPoint.Port.ToString(),
                                                                                         scene.RegionInfo.HttpPort.ToString());
                paramTable["avatarType"] = avatarType.ToString();
                paramTable["avatarClass"] = avatarClass.ToString();
                paramTable["userName"] = userName;
                paramTable["universalID"] = universalID;
                paramTable["clientUUID"] = client.AgentId.ToString();
                paramTable["clientSessionID"] = client.SessionId.ToString();
                paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "ClientLogin");

                // Handle the return result
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        balance = (int)resultTable["clientBalance"];
                        m_logger.LogInformation($"LoginMoneyServer: Client [{0}] login Money Server {1}", client.AgentId.ToString(), m_moneyServURL);
                        ret = true;
                    }
                }
                else m_logger.LogError($"LoginMoneyServer: Unable to login Money Server {0} for client [{1}]", m_moneyServURL, client.AgentId.ToString());
            }
            else m_logger.LogError($"LoginMoneyServer: Money Server is not available!!");

            #endregion Send money server the client info for login.

            // Viewerへ設定を通知する．
            if (ret || string.IsNullOrEmpty(m_moneyServURL))
            {
                OnEconomyDataRequest(client);
            }

            return ret;
        }

        /// <summary>
        /// Log off from the money server.
        /// </summary>
        /// <param name="userID">
        /// Indicate user ID of the new client.
        /// </param>
        /// <returns>
        /// return true, if successfully.
        /// </returns>
        private bool LogoffMoneyServer(IClientAPI client)
        {
            if (m_debug) m_logger.LogInformation($"LogoffMoneyServer:");

            bool ret = false;

            if (!string.IsNullOrEmpty(m_moneyServURL))
            {
                // Log off from the Money Server.
                Hashtable paramTable = new Hashtable();
                paramTable["clientUUID"] = client.AgentId.ToString();
                paramTable["clientSessionID"] = client.SessionId.ToString();
                paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "ClientLogout");
                // Handle the return result
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        ret = true;
                    }
                }
            }

            return ret;
        }

        //
        private EventManager.MoneyTransferArgs GetTransactionInfo(IClientAPI client, string transactionID)
        {
            if (m_debug) m_logger.LogInformation($"GetTransactionInfo:");

            EventManager.MoneyTransferArgs args = null;

            if (m_enable_server)
            {
                Hashtable paramTable = new Hashtable();
                paramTable["clientUUID"] = client.AgentId.ToString();
                paramTable["clientSessionID"] = client.SessionId.ToString();
                paramTable["clientSecureSessionID"] = client.SecureSessionId.ToString();
                paramTable["transactionID"] = transactionID;

                // Generate the request for transfer.
                Hashtable resultTable = genericCurrencyXMLRPCRequest(paramTable, "GetTransaction");

                // Handle the return result
                if (resultTable != null && resultTable.Contains("success"))
                {
                    if ((bool)resultTable["success"] == true)
                    {
                        int amount = (int)resultTable["amount"];
                        int type = (int)resultTable["type"];
                        string desc = (string)resultTable["description"];
                        UUID sender = UUID.Zero;
                        UUID recver = UUID.Zero;
                        UUID.TryParse((string)resultTable["sender"], out sender);
                        UUID.TryParse((string)resultTable["receiver"], out recver);
                        args = new EventManager.MoneyTransferArgs(sender, recver, amount, type, desc);
                    }
                    else
                    {
                        m_logger.LogError($"GetTransactionInfo: GetTransactionInfo: Fail to Request. {0}", (string)resultTable["description"]);
                    }
                }
                else
                {
                    m_logger.LogError($"GetTransactionInfo: Invalid Response");
                }
            }
            else
            {
                m_logger.LogError($"GetTransactionInfo: Invalid Money Server URL");
            }

            return args;
        }

        /// <summary>
        /// Generic XMLRPC client abstraction
        /// </summary>
        /// <param name="reqParams">Hashtable containing parameters to the method</param>
        /// <param name="method">Method to invoke</param>
        /// <returns>Hashtable with success=>bool and other values</returns>
        private Hashtable genericCurrencyXMLRPCRequest(Hashtable reqParams, string method)
        {
            if (m_debug) m_logger.LogInformation($"genericCurrencyXMLRPCRequest:");

            if (reqParams.Count <= 0 || string.IsNullOrEmpty(method)) return null;

            if (m_checkServerCert)
            {
                if (!m_moneyServURL.StartsWith("https://"))
                {
                    m_logger.LogInformation($"genericCurrencyXMLRPCRequest: CheckServerCert is true, but protocol is not HTTPS. Please check INI file");
                    //return null;
                }
            }
            else
            {
                if (!m_moneyServURL.StartsWith("https://") && !m_moneyServURL.StartsWith("http://"))
                {
                    m_logger.LogError($"genericCurrencyXMLRPCRequest: Invalid Money Server URL: {0}", m_moneyServURL);
                    return null;
                }
            }

            //
            ArrayList arrayParams = new ArrayList();
            arrayParams.Add(reqParams);
            XmlRpcResponse moneyServResp = null;
            try
            {
                NSLXmlRpcRequest moneyModuleReq = new NSLXmlRpcRequest(method, arrayParams);
                moneyServResp = moneyModuleReq.certSend(m_moneyServURL, m_cert, m_checkServerCert, MONEYMODULE_REQUEST_TIMEOUT);
            }
            catch (Exception ex)
            {
                m_logger.LogError($"genericCurrencyXMLRPCRequest: Unable to connect to Money Server {0}", m_moneyServURL);
                m_logger.LogError($"genericCurrencyXMLRPCRequest: {0}", ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }

            if (moneyServResp == null || moneyServResp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }

            Hashtable moneyRespData = (Hashtable)moneyServResp.Value;
            return moneyRespData;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// Locates a IClientAPI for the client specified
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private IClientAPI GetLocateClient(UUID AgentID)
        {
            IClientAPI client = null;

            lock (m_sceneList)
            {
                if (m_sceneList.Count > 0)
                {
                    foreach (Scene _scene in m_sceneList.Values)
                    {
                        ScenePresence tPresence = (ScenePresence)_scene.GetScenePresence(AgentID);
                        if (tPresence != null && !tPresence.IsChildAgent)
                        {
                            IClientAPI rclient = tPresence.ControllingClient;
                            if (rclient != null)
                            {
                                client = rclient;
                                break;
                            }
                        }
                    }
                }
            }

            return client;
        }

        private Scene GetLocateScene(UUID AgentId)
        {
            Scene scene = null;
            ScenePresence tPresence = null;
            string pname;

            lock (m_sceneList)
            {
                if (m_sceneList.Count > 0)
                {
                    foreach (Scene _scene in m_sceneList.Values)
                    {
                        tPresence = (ScenePresence)_scene.GetScenePresence(AgentId);
                        if (m_debug && tPresence != null)
                        {
                            pname = tPresence.Firstname + ' ' + tPresence.Lastname;
                            m_logger.LogInformation($"GetLocateScene (iterating), agentID={0} child={2} name={1}", AgentId.ToString(), pname, tPresence.IsChildAgent);
                        }
                        if (tPresence != null && !tPresence.IsChildAgent)
                        {
                            scene = _scene;
                            break;
                        }
                    }
                }
            }

            pname = "not found";
            if (scene != null) pname = tPresence.Firstname + ' ' + tPresence.Lastname;
            if (m_debug) m_logger.LogInformation($"GetLocateScene, agentID={0} name={1}", AgentId.ToString(), pname);

            return scene;
        }

        private SceneObjectPart GetLocatePrim(UUID objectID)
        {
            SceneObjectPart sceneObj = null;

            lock (m_sceneList)
            {
                if (m_sceneList.Count > 0)
                {
                    foreach (Scene _scene in m_sceneList.Values)
                    {
                        SceneObjectPart part = (SceneObjectPart)_scene.GetSceneObjectPart(objectID);
                        if (part != null)
                        {
                            sceneObj = part;
                            break;
                        }
                    }
                }
            }

            return sceneObj;
        }

        #endregion MoneyModule private help functions
    }
}
