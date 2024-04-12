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

using System.Reflection;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;

namespace OpenSim.Server.Base
{
    public class ServicesServerBase : ServerBase
    {
        protected string m_configDirectory = ".";

        // Run flag
        //
        private bool m_Running = true;

        // Handle all the automagical stuff
        //
        public ServicesServerBase(
            IConfiguration configuration,
            ILogger<ServicesServerBase> logger,
            ServerStatsCollector statsCollector)
            : base(configuration, logger, statsCollector)
        {
            LogEnvironmentInformation();
            RegisterCommonCommands();
            RegisterCommonComponents(Config);

            // Allow derived classes to perform initialization that
            // needs to be done after the console has opened
            Initialise();
        }

        public bool Running
        {
            get { return m_Running; }
        }

        private bool DoneShutdown = false;

        public virtual int Run()
        {
            Watchdog.Enabled = true;
            MemoryWatchdog.Enabled = true;

            while (m_Running)
            {
                try
                {
                    MainConsole.Instance.Prompt();
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Command error");
                }
            }

            if (!DoneShutdown)
            {
                DoneShutdown = true;
                MainServer.Stop();

                MemoryWatchdog.Enabled = false;
                Watchdog.Enabled = false;
                WorkManager.Stop();
            }
            return 0;
        }

        protected override void ShutdownSpecific()
        {
            if(!m_Running)
                return;

            m_Running = false;
            Logger.LogInformation("Quitting");

            base.ShutdownSpecific();

            if (!DoneShutdown)
            {
                DoneShutdown = true;
                MainServer.Stop();

                MemoryWatchdog.Enabled = false;
                Watchdog.Enabled = false;
                WorkManager.Stop();

                Util.StopThreadPool();
                Environment.Exit(0);
            }
        }

        protected virtual void Initialise()
        {
        }

        /// <summary>
        /// Check if we can convert the string to a URI
        /// </summary>
        /// <param name="file">String uri to the remote resource</param>
        /// <returns>true if we can convert the string to a Uri object</returns>
        bool IsUri(string file)
        {
            Uri configUri;

            return (Uri.TryCreate(file, UriKind.Absolute, out configUri) && configUri.Scheme == Uri.UriSchemeHttp);
        }
    }
}
