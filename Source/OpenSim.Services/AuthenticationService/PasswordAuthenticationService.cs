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
using OpenSim.Data;
using OpenSim.Framework;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenSim.Services.AuthenticationService
{
    //
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need
    // verifiable identification.
    //
    public class PasswordAuthenticationService :  AuthenticationServiceBase, IAuthenticationService
    {
        public PasswordAuthenticationService(
            IConfiguration config, 
            ILogger<AuthenticationServiceBase> logger,
            IAuthenticationData authenticationData,
            IUserAccountService acct)
            : base(config, logger, authenticationData, acct)
        {
            m_logger.LogDebug("[AUTH SERVICE]: Started with User Account access");
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            UUID realID;
            return Authenticate(principalID, password, lifetime, out realID);
        }

        public string Authenticate(UUID principalID, string password, int lifetime, out UUID realID)
        {
            realID = UUID.Zero;

            m_logger.LogDebug($"[AUTH SERVICE]: Authenticating for {principalID}, user account service present: {(m_UserAccountService != null)}");

            AuthenticationData? data = m_Database.Get(principalID);
            UserAccount? user = null;

            if (m_UserAccountService != null)
                user = m_UserAccountService.GetUserAccount(UUID.Zero, principalID);

            if (data == null || data.Data == null)
            {
                m_logger.LogDebug("[AUTH SERVICE]: PrincipalID {0} or its data not found", principalID);
                return String.Empty;
            }

            if (data.Data.ContainsKey("passwordHash") is false || data.Data.ContainsKey("passwordSalt") is false)
            {
                return String.Empty;
            }

            string hashed = Util.Md5Hash(password + ":" + data.Data["passwordSalt"].ToString());

//            m_log.DebugFormat("[PASS AUTH]: got {0}; hashed = {1}; stored = {2}", password, hashed, data.Data["passwordHash"].ToString());

            if (data.Data["passwordHash"].ToString() == hashed)
            {
                return GetToken(principalID, lifetime);
            }

            if (user == null)
            {
                m_logger.LogDebug($"[PASS AUTH]: No user record for {principalID}");
                return String.Empty;
            }

            int impersonateFlag = 1 << 6;

            if ((user.UserFlags & impersonateFlag) == 0)
                return String.Empty;

            m_logger.LogDebug("[PASS AUTH]: Attempting impersonation");

            List<UserAccount>? accounts = m_UserAccountService?.GetUserAccountsWhere(UUID.Zero, "UserLevel >= 200");
            if (accounts == null || accounts.Count == 0)
            {
                m_logger.LogDebug("[PASS AUTH]: No suitable gods found");
                return String.Empty;
            }

            foreach (UserAccount a in accounts)
            {
                data = m_Database.Get(a.PrincipalID);
                if (data == null || data.Data == null ||
                    !data.Data.ContainsKey("passwordHash") ||
                    !data.Data.ContainsKey("passwordSalt"))
                {
                    m_logger.LogDebug($"[PASS AUTH]: {a.FirstName} {a.LastName} has no suitable password set");
                    continue;
                }

                m_logger.LogDebug($"[PASS AUTH]: Trying {data.PrincipalID}");

                hashed = Util.Md5Hash(password + ":" + data.Data["passwordSalt"].ToString());

                if (data.Data["passwordHash"].ToString() == hashed)
                {
                    m_logger.LogDebug($"[PASS AUTH]: {a.FirstName} {a.LastName} impersonating {principalID}, proceeding with login");

                    realID = a.PrincipalID;
                    return GetToken(principalID, lifetime);
                }
                else
                {
                    m_logger.LogDebug(
                        $"[AUTH SERVICE]: Salted hash {hashed} of given password did not match salted hash "+
                        $"of {data.Data["passwordHash"]} for PrincipalID {data.PrincipalID}.  Authentication failure.");
                }
            }

            m_logger.LogDebug($"[PASS AUTH]: Impersonation of {principalID} failed");
            return String.Empty;
        }
    }
}
