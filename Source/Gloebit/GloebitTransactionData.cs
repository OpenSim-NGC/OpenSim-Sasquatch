/*
 * GloebitTransactionData.cs is part of OpenSim-MoneyModule-Gloebit
 * Copyright (C) 2015 Gloebit LLC
 *
 * OpenSim-MoneyModule-Gloebit is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenSim-MoneyModule-Gloebit is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with OpenSim-MoneyModule-Gloebit.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Data.SqlTypes;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenSim.Data.MySQL;
using OpenSim.Server.Base;

using MySqlConnector;


namespace Gloebit.GloebitMoneyModule
{
    class GloebitTransactionData {
        private static ILogger m_logger;

        private static IGloebitTransactionData m_impl;

        public static void Initialise(string storageProvider, string connectionString) {
            m_logger ??= OpenSimServer.Instance.ServiceProvider.GetRequiredService<ILogger<GloebitTransactionData>>();

            switch (storageProvider) {
                case "OpenSim.Data.MySQL.dll":
                    m_impl = new MySQLImpl(connectionString);
                    break;
                default:
                    break;
            }
        }

        public static IGloebitTransactionData Instance {
            get { return m_impl; }
        }

        public interface IGloebitTransactionData {
            GloebitTransaction[] Get(string field, string key);

            GloebitTransaction[] Get(string[] fields, string[] keys);

            bool Store(GloebitTransaction txn);
        }

        private class MySQLImpl : MySQLGenericTableHandler<GloebitTransaction>, IGloebitTransactionData {
            public MySQLImpl(string connectionString)
                : base(connectionString, "GloebitTransactions", "GloebitTransactionsMySQL")
            {
            }
            
            public override bool Store(GloebitTransaction txn)
            {
                //            m_logger.LogDebug("[MYSQL GENERIC TABLE HANDLER]: Store(T row) invoked");

                try
                {
                
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        string query = "";
                        List<String> names = new List<String>();
                        List<String> values = new List<String>();

                        foreach (FieldInfo fi in m_Fields.Values)
                        {
                            names.Add(fi.Name);
                            values.Add("?" + fi.Name);

                            // Temporarily return more information about what field is unexpectedly null for
                            // http://opensimulator.org/mantis/view.php?id=5403.  This might be due to a bug in the
                            // InventoryTransferModule or we may be required to substitute a DBNull here.
                            /*if (fi.GetValue(asset) == null)
                                throw new NullReferenceException(
                                                                string.Format(
                                                                            "[MYSQL GENERIC TABLE HANDLER]: Trying to store field {0} for {1} which is unexpectedly null",
                                                                            fi.Name, asset));*/

                            cmd.Parameters.AddWithValue(fi.Name, fi.GetValue(txn));
                        }

                        /*if (m_DataField != null)
                        {
                            Dictionary<string, string> data =
                            (Dictionary<string, string>)m_DataField.GetValue(row);

                            foreach (KeyValuePair<string, string> kvp in data)
                            {  
                                names.Add(kvp.Key);
                                values.Add("?" + kvp.Key);
                                cmd.Parameters.AddWithValue("?" + kvp.Key, kvp.Value);
                            }
                        }*/

                        query = String.Format("replace into {0} (`", m_Realm) + String.Join("`,`", names.ToArray()) + "`) values (" + String.Join(",", values.ToArray()) + ")";

                        cmd.CommandText = query;

                        if (ExecuteNonQuery(cmd) > 0)
                            return true;

                        return false;
                    }
                }
                catch(Exception e)
                {
                    m_logger.LogDebug("[MYSQL GENERIC TABLE HANDLER]: Failed to store data: {0}", e);
                    return false;
                }
            }
        }

    }
}