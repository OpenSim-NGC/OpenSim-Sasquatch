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

using MySqlConnector;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// Common code for a number of database modules
    /// </summary>
    public class MySqlFramework
    {
        protected string m_connectionString = String.Empty;
        protected MySqlTransaction m_trans = null;

        // Initialize using a connection string. Instances constructed
        // this way will open a new connection for each call.
        public void Initialize(string connectionString)
        {
            m_connectionString = connectionString;
        }

        // Initialize using a connection object. Instances constructed
        // this way will use the connection object and never create
        // new connections.
        public void Initialize(MySqlTransaction transaction)
        {
            m_trans = transaction;
        }

        //////////////////////////////////////////////////////////////
        //
        // All non queries are funneled through one connection
        // to increase performance a little
        //
        public int ExecuteNonQuery(MySqlCommand cmd)
        {
            if (m_trans == null)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    int ret = ExecuteNonQueryWithConnection(cmd, dbcon);
                    dbcon.Close();
                    return ret;
                }
            }
            else
            {
                return ExecuteNonQueryWithTransaction(cmd, m_trans);
            }
        }

        public int ExecuteNonQueryWithTransaction(MySqlCommand cmd, MySqlTransaction trans)
        {
            cmd.Transaction = trans;
            return ExecuteNonQueryWithConnection(cmd, trans.Connection);
        }

        public int ExecuteNonQueryWithConnection(MySqlCommand cmd, MySqlConnection dbcon)
        {
            try
            {
                cmd.Connection = dbcon;

                try
                {
                    int ret = cmd.ExecuteNonQuery();
                    cmd.Connection = null;
                    return ret;
                }
                catch (Exception)
                {
                    cmd.Connection = null;
                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
