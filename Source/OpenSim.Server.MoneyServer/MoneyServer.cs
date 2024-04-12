/*
 * Copyright (c) Contributors, http://opensimulator.org/, http://www.nsl.tuis.ac.jp/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *	 * Redistributions of source code must retain the above copyright
 *	   notice, this list of conditions and the following disclaimer.
 *	 * Redistributions in binary form must reproduce the above copyright
 *	   notice, this list of conditions and the following disclaimer in the
 *	   documentation and/or other materials provided with the distribution.
 *	 * Neither the name of the OpenSim Project nor the
 *	   names of its contributors may be used to endorse or promote products
 *	   derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Net.Security;
using System.Timers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework;

using NSL.Certificate.Tools;

using Timer = System.Timers.Timer;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenSim.Server.Base;
using OpenSim.Framework.Monitoring;

/// <summary>
/// OpenSim Server MoneyServer
/// </summary>
namespace OpenSim.Server.MoneyServer
{
    public class MoneyServer : IMoneyServiceCore
    {
        private readonly ILogger<MoneyServer> m_logger;
        private readonly IConfiguration m_configuration;
        private readonly IServiceProvider m_serviceProvider;

        private string connectionString = string.Empty;
        private uint m_moneyServerPort = 8008;         // 8008 is default server port

        private string m_certFilename = "";
        private string m_certPassword = "";
        private string m_cacertFilename = "";
        private string m_clcrlFilename = "";
        private bool m_checkClientCert = false;

        private int DEAD_TIME = 120;
        private int MAX_DB_CONNECTION = 10;

        private readonly NSLCertificateVerify m_certVerify = new NSLCertificateVerify(); // for Client Certificate

        private readonly Dictionary<string, string> m_sessionDic = new Dictionary<string, string>();
        private readonly Dictionary<string, string> m_secureSessionDic = new Dictionary<string, string>();
        private readonly Dictionary<string, string> m_webSessionDic = new Dictionary<string, string>();

        private IConfigurationSection m_server_config;
        private IConfigurationSection m_cert_config;

        public NSLCertificateVerify CertVerify => m_certVerify;

        private OpenSimServer m_baseServer = null;
        private MoneyXmlRpcModule m_moneyXmlRpcModule = null;
        private MoneyDBService m_moneyDBService = null;

        /// <summary>
        /// Money Server Base
        /// </summary>
        public MoneyServer(
            IServiceProvider provider,
            IConfiguration configuration,
            ILogger<MoneyServer> logger
            )

        {
            m_serviceProvider = provider;
            m_configuration = configuration;
            m_logger = logger;
        }

        /// <summary>
        /// Startup Specific
        /// </summary>
        public void Startup()
        {
            m_logger.LogInformation($"MoneyServer Startup");

            using (var scope = m_serviceProvider.CreateScope())
            {
                m_baseServer = scope.ServiceProvider.GetRequiredService<OpenSimServer>();
                m_moneyXmlRpcModule = scope.ServiceProvider.GetRequiredService<MoneyXmlRpcModule>();
                m_moneyDBService = scope.ServiceProvider.GetService<MoneyDBService>();

                // var httpServer = scope.ServiceProvider.GetRequiredService<IHttpServer>();
                // MainServer.Instance = m_baseServer.HttpServer = httpServer;

                m_logger.LogInformation($"Configuring MoneyServer And Starting Http(s) support");

                try
                {
                    m_baseServer.Startup();

                    GetDatabaseConfiguration();

                    GetMoneyServerConfiguration();

                    SetupMoneyServices();
                }
                catch (Exception)
                {
                    m_logger.LogError($"Error occured during MoneyServer setup. Please check MoneyServer.ini. Exiting");
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// Work
        /// </summary>
        public void Work()
        {
            //The timer checks the transactions table every 60 seconds
            Timer checkTimer = new Timer { Interval = 60 * 1000, Enabled = true};

            checkTimer.Elapsed += new ElapsedEventHandler(CheckTransaction);
            checkTimer.Start();

            MainConsole.Instance.DefaultPrompt = "MoneyServer";

            while (true)
            {
                MainConsole.Instance.Prompt();
            }
        }

        /// <summary>
        /// Check the transactions table, set expired transaction state to failed
        /// </summary>
        private void CheckTransaction(object sender, ElapsedEventArgs e)
        {
            long ticksToEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            int unixEpochTime = (int)((DateTime.UtcNow.Ticks - ticksToEpoch) / 10000000);
            int deadTime = unixEpochTime - DEAD_TIME;

            m_moneyDBService.SetTransExpired(deadTime);
        }


        private void GetMoneyServerConfiguration()
        {
            //xxxx
            // [MoneyServer]
            m_server_config = m_configuration.GetSection("MoneyServer");
            DEAD_TIME = m_server_config.GetValue<int>("ExpiredTime", DEAD_TIME);
            m_moneyServerPort = m_server_config.GetValue<uint>("ServerPort", m_moneyServerPort);

            //
            // [Certificate]
            m_cert_config = m_configuration.GetSection("Certificate");
            if (m_cert_config == null)
            {
                m_logger.LogInformation($"[Certificate] section is not found. Using [MoneyServer] section instead");
                m_cert_config = m_server_config;
            }

            // HTTPS Server Cert (Server Mode)
            m_certFilename = m_cert_config.GetValue<string>("ServerCertFilename", m_certFilename);
            m_certPassword = m_cert_config.GetValue<string>("ServerCertPassword", m_certPassword);
            if (m_certFilename != "")
            {
                m_logger.LogInformation($"ReadIniConfig: Execute HTTPS communication. Cert file is {m_certFilename}");
            }

            // Client Certificate
            m_checkClientCert = m_cert_config.GetValue<bool>("CheckClientCert", m_checkClientCert);
            m_cacertFilename = m_cert_config.GetValue<string>("CACertFilename", m_cacertFilename);
            m_clcrlFilename = m_cert_config.GetValue<string>("ClientCrlFilename", m_clcrlFilename);

            //
            if (m_checkClientCert && !string.IsNullOrEmpty(m_cacertFilename))
            {
                CertVerify.SetPrivateCA(m_cacertFilename);
                m_logger.LogInformation($"ReadIniConfig: Execute Authentication of Clients. CA  file is {m_cacertFilename}");
            }
            else
            {
                m_checkClientCert = false;
            }

            if (m_checkClientCert && !string.IsNullOrEmpty(m_clcrlFilename))
            {
                CertVerify.SetPrivateCRL(m_clcrlFilename);
                m_logger.LogInformation($"ReadIniConfig: Execute Authentication of Clients. CRL file is {m_clcrlFilename}");
            }
        }

        private void GetDatabaseConfiguration()
        {
            // [MySql]
            var db_config = m_configuration.GetSection("MySql");

            string sqlserver = db_config.GetValue<string>("hostname", "localhost");
            string database = db_config.GetValue<string>("database", "OpenSim");
            string username = db_config.GetValue<string>("username", "root");
            string password = db_config.GetValue<string>("password", "password");
            string pooling = db_config.GetValue<string>("pooling", "false");
            string port = db_config.GetValue<string>("port", "3306");
            string options = db_config.GetValue<string>("options", "");

            MAX_DB_CONNECTION = db_config.GetValue<int>("MaxConnection", MAX_DB_CONNECTION);

            connectionString = $"Server={sqlserver};Port={port};Database={database};User ID={username};Password={password};Pooling={pooling};{options}";
        }

        /// <summary>
        /// Setup Money Services
        /// </summary>
        protected virtual void SetupMoneyServices()
        {
            try
            {
                m_baseServer.HttpServer = m_serviceProvider.GetService<IHttpServer> ();

                if (string.IsNullOrEmpty(m_certFilename) is false)
                {
                    m_baseServer.HttpServer.Initialize(m_moneyServerPort, ipaddr: null, true, m_certFilename, m_certPassword);
                    if (m_checkClientCert)
                    {
                        m_baseServer.HttpServer.CertificateValidationCallback = (RemoteCertificateValidationCallback)CertVerify.ValidateClientCertificate;
                        m_logger.LogInformation($"Set RemoteCertificateValidationCallback");
                    }
                }
                else
                {
                    m_baseServer.HttpServer.Initialize(m_moneyServerPort);
                }

                m_logger.LogInformation("Connecting to Money Storage Server");
                m_moneyDBService.Initialise(connectionString, MAX_DB_CONNECTION);

                m_logger.LogInformation($"Initializing XML/RPC API");
                m_moneyXmlRpcModule.Initialise(m_baseServer.Version, m_moneyDBService, this);
                m_moneyXmlRpcModule.PostInitialise();

                m_logger.LogInformation("Starting HTTP Server");
                m_baseServer.HttpServer.Start();
                m_baseServer.Startup();
            }
            catch (Exception e)
            {
                m_logger.LogError(e, $"StartupSpecific: Fail to start HTTPS process");
                m_logger.LogError(e, $"StartupSpecific: Please Check Certificate File or Password. Exit");
                Environment.Exit(1);
            }
        }

        public IHttpServer HttpServer
        {
            get { return m_baseServer.HttpServer; }
        }

        /// <summary>
        /// Is Check Client Cert
        /// </summary>
        public bool IsCheckClientCert()
        {
            return m_checkClientCert;
        }

        /// <summary>
        /// Get Server Config
        /// </summary>
        public IConfigurationSection GetServerConfig()
        {
            return m_server_config;
        }

        /// <summary>
        /// Get Cert Config
        /// </summary>
        public IConfigurationSection GetCertConfig()
        {
            return m_cert_config;
        }

        /// <summary>
        /// Get Session Dic
        /// </summary>
        public Dictionary<string, string> GetSessionDic()
        {
            return m_sessionDic;
        }

        /// <summary>
        /// Get Secure Session Dic
        /// </summary>
        public Dictionary<string, string> GetSecureSessionDic()
        {
            return m_secureSessionDic;
        }

        /// <summary>
        /// Get Web Session Dic
        /// </summary>
        public Dictionary<string, string> GetWebSessionDic()
        {
            return m_webSessionDic;
        }

    }
}