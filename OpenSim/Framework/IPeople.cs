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

using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class UserData
    {
        public UUID Id;
        public string FirstName;
        public string LastName;
        public string HomeURL;
        public Dictionary<string, object> ServerURLs;
        public bool IsUnknownUser;
        public bool HasGridUserTried;
        public bool IsLocal;
        public double LastWebFail = -1;
        public string DisplayName;
        public DateTime NameChanged;

        public bool IsNameDefault
        {
            get
            {
                return string.IsNullOrWhiteSpace(DisplayName);
            }
        }

        public string LegacyName
        {
            get
            {
                if (LastName.ToLower() == "resident")
                    return FirstName;
                else return $"{FirstName} {LastName}";
            }
        }

        public string Username
        {
            get
            {
                if (LastName.ToLower() == "resident")
                    return FirstName;
                else if(LastName.StartsWith("@"))
                    return $"{FirstName}{LastName}";
                else return $"{FirstName}.{LastName}";
            }
        }

        public string LowerUsername
        {
            get
            {
                return Username.ToLower();
            }
        }

        public string ViewerDisplayName
        {
            get
            {
                if (IsNameDefault)
                    return LegacyName;
                else return DisplayName;
            }
        }
    }

    public interface IPeople
    {
        List<UserData> GetUserData(string query, int page_size, int page_number);
    }
}