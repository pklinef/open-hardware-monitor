using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.Windows.Forms;
using System.IO;
using System.Data;
using System.Management;
using System.Net.NetworkInformation;

namespace OpenHardwareMonitor.DAL
{
    public class DataManager
    {
        private static Object s_lockObject = new Object();
        private string _dbFile;
        private SQLiteConnection _sqliteConnection;
        private static DataManager s_dataManager = new DataManager();
        private static long s_macAddress = -1;
        private static DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        private DataManager()
        {
            _dbFile = "sqlite.s3db";
            string currentPath;
            string absolutePath;
            string connectionString;

            currentPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath); 
            absolutePath = System.IO.Path.Combine(currentPath, _dbFile);

            connectionString = string.Format(@"Data Source={0}", absolutePath);

            _sqliteConnection = new SQLiteConnection(connectionString);
            _sqliteConnection.Open();
        }

        #region Get Methods
        public static long CurrentComputerId
        {
            get
            {
                if (s_macAddress == -1)
                {
                    lock (s_lockObject)
                    {
                        if (s_macAddress == -1)
                        {
                            IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
                            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

                            if (nics != null && nics.Length > 0)
                            {
                                // TODO: We need to sort these
                                foreach (NetworkInterface adapter in nics)
                                {
                                    IPInterfaceProperties properties = adapter.GetIPProperties();
                                    PhysicalAddress address = adapter.GetPhysicalAddress();

                                    // This is annoying, but BitConverter expect an 8 byte array. We'll create one
                                    // and pad the first two bytes as 0
                                    byte[] soonToBeLong = new byte[8];
                                    address.GetAddressBytes().CopyTo(soonToBeLong, 2);

                                    if (BitConverter.IsLittleEndian)
                                    {
                                        Array.Reverse(soonToBeLong);
                                    }

                                    s_macAddress = BitConverter.ToInt64(soonToBeLong, 0);
                                    break;
                                }

                            }
                        }
                    }
                }

                return s_macAddress;
            }
        }

        public static long GetComponentId(string name, string type)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name must not be null or empty", "name");
            }

            if (String.IsNullOrEmpty(type))
            {
                throw new ArgumentException("Type must not be null or empty", "type");
            }

            long componentId = -1;
            lock (s_lockObject)
            {
                const string c_getCompoenntIdQuery = "SELECT ComponentId FROM Component WHERE Name = @name and Type = @type";
            
                SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection);
                sqlQueryCommand.CommandText = c_getCompoenntIdQuery;
                sqlQueryCommand.Parameters.Add(new SQLiteParameter("@name", name));
                sqlQueryCommand.Parameters.Add(new SQLiteParameter("@type", type));

                using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        componentId = Convert.ToInt64(reader["ComponentID"]);
                    }
                }

                if (componentId == -1)
                {
                    using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        const string c_insertComponent = "INSERT INTO Component (Name,Type) values (@name,@type)";

                        sqlInsertCommand.CommandText = c_insertComponent;
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@name", name));
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@type", type));

                        sqlInsertCommand.ExecuteNonQuery();
                        componentId = s_dataManager._sqliteConnection.LastInsertRowId;
                    }
                }
            }

            return componentId;
        }

        public static long GetComputerComponentId(long componentId, long parentComponentId)
        {
            if (componentId <= 0)
            {
                throw new ArgumentException("ComponentId must not be positive", "componentId");
            }

            if (parentComponentId < 0)
            {
                throw new ArgumentException("ParentComponentId must be 0 or positive", "parentComponentId");
            }

            long computerComponentId = -1;

            lock (s_lockObject)
            {
                using (SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    const string c_getCompoenntIdQuery = "SELECT ComputerComponentID FROM ComputerComponent WHERE ComputerID = @computerId and ComponentID = @componentId and ParentComputerComponentID = @parentComponentId";

                    sqlQueryCommand.CommandText = c_getCompoenntIdQuery;
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@computerId", CurrentComputerId));
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@componentId", componentId));
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@parentComponentId", parentComponentId));

                    using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                //if (reader.
                                computerComponentId = Convert.ToInt64(reader["ComponentID"]);
                            }
                        }
                    }
                }

                if (computerComponentId == -1)
                {
                    const string c_insertComponent = "INSERT INTO ComputerComponent (ComputerID,ComponentID,ParentComputerComponentID) values (@computerId,@componentId,@parentComponentId)";
                    
                    using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        sqlInsertCommand.CommandText = c_insertComponent;
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@computerId", CurrentComputerId));
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@componentId", componentId));
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@parentComponentId", parentComponentId));

                        sqlInsertCommand.ExecuteNonQuery();
                        computerComponentId = s_dataManager._sqliteConnection.LastInsertRowId;
                    }
                }
            }

            return computerComponentId;
        }

        public static long GetSensorTypeId(string name, string units)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name must not be null or empty", "name");
            }

            if (String.IsNullOrEmpty(units))
            {
                throw new ArgumentException("Units must not be null or empty", "units");
            }

            long sensorTypeId = -1;
            lock (s_lockObject)
            {
                using (SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    const string c_getSensorTypeIdQuery = "SELECT SensorTypeID FROM SensorType WHERE Name = @name and Units = @units";

                    sqlQueryCommand.CommandText = c_getSensorTypeIdQuery;
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@name", name));
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@units", units));

                    using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            sensorTypeId = Convert.ToInt64(reader["SensorTypeID"]);
                        }
                    }
                }

                if (sensorTypeId == -1)
                {
                    const string c_insertSensorType = "INSERT INTO SensorType (Name,Units) values (@name,@units)";

                    using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        sqlInsertCommand.CommandText = c_insertSensorType;
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@name", name));
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@units", units));
                        
                        sqlInsertCommand.ExecuteNonQuery();
                        sensorTypeId = s_dataManager._sqliteConnection.LastInsertRowId;
                    }
                }
            }

            return sensorTypeId;
        }

        public static long GetComputerId(int macAddress)
        {
            if (macAddress <= 0)
            {
                throw new ArgumentException("MAC Address must not be null or empty", "macAddress");
            }

            long computerId = -1;
            lock (s_lockObject)
            {
                using (SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    const string c_getComputerIdQuery = "SELECT ComputerID FROM Computer WHERE MACAddress = @address";
            
                    sqlQueryCommand.CommandText = c_getComputerIdQuery;
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@address", macAddress));

                    using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            computerId = Convert.ToInt64(reader["SensorTypeID"]);
                        }
                    }
                }

                if (computerId == -1)
                {
                    const string c_insertComputer = "INSERT INTO Computer (MACAddress,IPAddress) values (@mac,@ip)";

                    using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        sqlInsertCommand.CommandText = c_insertComputer;
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@mac", macAddress));
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@comipputerId", 1));
                        sqlInsertCommand.ExecuteNonQuery();
                        computerId = s_dataManager._sqliteConnection.LastInsertRowId;
                    }
                }
            }

            return computerId;
        }
        #endregion

        #region Insert Methods
        public static void InsertUser(string userName, string name)
        {
            const string c_userExistsQuery = "SELECT Username FROM User WHERE Username = @userName";

            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentException("Username must not be null or empty", "userName");
            }

            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name must not be null or empty", "name");
            }

            lock (s_lockObject)
            {
                using (SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlQueryCommand.CommandText = c_userExistsQuery;
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@userName", userName));
                    using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            throw new ArgumentException("Username must not already exist", "userName");
                        }
                    }
                }

                const string c_insertUser = "INSERT INTO User (Username,Name,LastAccessTime) values (@userName,@name,@lastAccessTime)";

                using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlInsertCommand.CommandText = c_insertUser;
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@userName", userName));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@name", name));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@lastAccessTime", ((int)(DateTime.UtcNow - s_unixEpoch).TotalSeconds)));

                    sqlInsertCommand.ExecuteNonQuery();
                }
            }
        }

        public static void InsertSensorData(long computerComponentId, long sensorTypeID, double value)
        {
            lock (s_lockObject)
            {
                const string c_insertSensorData = "INSERT INTO SensorData (ComputerComponentID,Date,SensorTypeID,Value) values (@componentId,@date,@sensorTypeId,@Value)";

                using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlInsertCommand.CommandText = c_insertSensorData;
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@componentId", computerComponentId));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@date", ((int)(DateTime.UtcNow - s_unixEpoch).TotalSeconds)));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@sensorTypeId", sensorTypeID));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@Value", value));

                    sqlInsertCommand.ExecuteNonQuery();
                }
            }
        }



        public static void AddHardwareToDB(Hardware.IHardware hardware)
        {
            lock (s_lockObject)
            {
                SQLiteDataReader reader;
                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {

                    command.CommandText = "SELECT * FROM ComputerComponent WHERE ComputerComponentID = @computerComponentId";
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", hardware.Identifier.ToString()));
                    reader = command.ExecuteReader();

                    if (reader.HasRows)
                        return;
                }
                
                Int64 componentId = -1;

                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "SELECT ComponentID FROM Component WHERE Name = '@componentName' AND Type = '@componentType'";
                    command.Parameters.Add(new SQLiteParameter("@componentName", hardware.Name));
                    command.Parameters.Add(new SQLiteParameter("@componentType", hardware.HardwareType.ToString()));
                    reader = command.ExecuteReader();

                    
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                            componentId = Convert.ToInt64(reader["ComponentID"]);

                    }
                }



                if (componentId == -1)
                {
                    using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        command.CommandText = "INSERT INTO Component (Name, Type) values (@componentName, @componentType)";
                        command.Parameters.Add(new SQLiteParameter("@componentName", hardware.Name));
                        command.Parameters.Add(new SQLiteParameter("@componentType", hardware.HardwareType.ToString()));
                        command.ExecuteNonQuery();

                        command.CommandText = "SELECT ComponentID FROM Component WHERE Name = @componentName AND Type = @componentType";
                        command.Parameters.Add(new SQLiteParameter("@componentName", hardware.Name));
                        command.Parameters.Add(new SQLiteParameter("@componentType", hardware.HardwareType.ToString()));
                        reader = command.ExecuteReader();

                        while (reader.Read())
                            componentId = Convert.ToInt64(reader["ComponentID"]);

                    }
                }

                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "INSERT INTO ComputerComponent (ComputerComponentID, ComputerID, ComponentID, ParentComputerComponentID) values (@computerComponentId, @computerId, @componentId, @parentComputerComponentId)";
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", hardware.Identifier.ToString()));
                    command.Parameters.Add(new SQLiteParameter("@computerId", 1));  //NOTE: hard coded!!!
                    command.Parameters.Add(new SQLiteParameter("@componentId", componentId));
                    command.Parameters.Add(new SQLiteParameter("@parentComputerComponentId", (hardware.Parent == null?"":hardware.Parent.Identifier.ToString())));
                    command.ExecuteNonQuery();
                }
            }
        }
        #endregion
    }
}
