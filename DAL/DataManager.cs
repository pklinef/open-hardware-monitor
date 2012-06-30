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
        private static SQLiteCommand s_sqlCommand = new SQLiteCommand(s_dataManager._sqliteConnection);
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
            const string c_getCompoenntIdQuery1 = "SELECT ComponentId FROM Component WHERE Name = '";
            const string c_getCompoenntIdQuery2 = "' and Type = '";
            const string c_getCompoenntIdQuery3 = "'";

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
                s_sqlCommand.CommandText = c_getCompoenntIdQuery1 + name + c_getCompoenntIdQuery2 + type + c_getCompoenntIdQuery3;
                using (SQLiteDataReader reader = s_sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        componentId = Convert.ToInt64(reader["ComponentID"]);
                    }
                }

                if (componentId == -1)
                {
                    const string c_insertComponent1 = "INSERT INTO Component (Name,Type) values ('";
                    const string c_insertComponent2 = "','";
                    const string c_insertComponent3 = "')";

                    s_sqlCommand.CommandText = c_insertComponent1 + name + c_insertComponent2 + type + c_insertComponent3;
                    s_sqlCommand.ExecuteNonQuery();
                    componentId = s_dataManager._sqliteConnection.LastInsertRowId;
                }
            }

            return componentId;
        }

        public static long GetComputerComponentId(long componentId, long parentComponentId)
        {
            const string c_getCompoenntIdQuery1 = "SELECT ComputerComponentID FROM ComputerComponent WHERE ComputerID = '";
            const string c_getCompoenntIdQuery2 = "' and ComponentID = '";
            const string c_getCompoenntIdQuery3 = "' and ParentComputerComponentID = '";
            const string c_getCompoenntIdQuery4 = "'";

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
                s_sqlCommand.CommandText = c_getCompoenntIdQuery1 +
                    CurrentComputerId + 
                    c_getCompoenntIdQuery2 + 
                    componentId + 
                    c_getCompoenntIdQuery3 +
                    parentComponentId +
                    c_getCompoenntIdQuery4;
                using (SQLiteDataReader reader = s_sqlCommand.ExecuteReader())
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

                if (computerComponentId == -1)
                {
                    const string c_insertComponent1 = "INSERT INTO ComputerComponent (ComputerID,ComponentID,ParentComputerComponentID) values ('";
                    const string c_insertComponent2 = "','";
                    const string c_insertComponent3 = "')";

                    s_sqlCommand.CommandText = c_insertComponent1 + 
                        CurrentComputerId + 
                        c_insertComponent2 + 
                        componentId +
                        c_insertComponent2 + 
                        parentComponentId +
                        c_insertComponent3;
                    s_sqlCommand.ExecuteNonQuery();
                    computerComponentId = s_dataManager._sqliteConnection.LastInsertRowId;
                }
            }

            return computerComponentId;
        }

        public static long GetSensorTypeId(string name, string units)
        {
            const string c_getSensorTypeIdQuery1 = "SELECT SensorTypeID FROM SensorType WHERE Name = '";
            const string c_getSensorTypeIdQuery2 = "' and Units = '";
            const string c_getSensorTypeIdQuery3 = "'";

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
                s_sqlCommand.CommandText = c_getSensorTypeIdQuery1 + name + c_getSensorTypeIdQuery2 + units + c_getSensorTypeIdQuery3;
                using (SQLiteDataReader reader = s_sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sensorTypeId = Convert.ToInt64(reader["SensorTypeID"]);
                    }
                }

                if (sensorTypeId == -1)
                {
                    const string c_insertSensorType1 = "INSERT INTO SensorType (Name,Units) values ('";
                    const string c_insertSensorType2 = "','";
                    const string c_insertSensorType3 = "')";

                    s_sqlCommand.CommandText = c_insertSensorType1 + name + c_insertSensorType2 + units + c_insertSensorType3;
                    s_sqlCommand.ExecuteNonQuery();
                    sensorTypeId = s_dataManager._sqliteConnection.LastInsertRowId;
                }
            }

            return sensorTypeId;
        }

        public static long GetComputerId(int macAddress)
        {
            const string c_getComputerIdQuery1 = "SELECT ComputerID FROM Computer WHERE MACAddress = '";
            const string c_getComputerIdQuery2 = "'";

            if (macAddress <= 0)
            {
                throw new ArgumentException("MAC Address must not be null or empty", "macAddress");
            }

            long computerId = -1;
            lock (s_lockObject)
            {
                s_sqlCommand.CommandText = c_getComputerIdQuery1 + macAddress + c_getComputerIdQuery2;
                using (SQLiteDataReader reader = s_sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        computerId = Convert.ToInt64(reader["SensorTypeID"]);
                    }
                }

                if (computerId == -1)
                {
                    const string c_insertComputer1 = "INSERT INTO Computer (MACAddress,IPAddress) values ('";
                    const string c_insertComputer2 = "','";
                    const string c_insertComputer3 = "')";

                    s_sqlCommand.CommandText = c_insertComputer1 + macAddress + c_insertComputer2 + "1" + c_insertComputer3;
                    s_sqlCommand.ExecuteNonQuery();
                    computerId = s_dataManager._sqliteConnection.LastInsertRowId;
                }
            }

            return computerId;
        }
        #endregion

        #region Insert Methods
        public static void InsertUser(string userName, string name)
        {
            const string c_userExistsQuery1 = "SELECT Username FROM User WHERE Username = '";
            const string c_userExistsQuery2 = "'";

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
                s_sqlCommand.CommandText = c_userExistsQuery1 + userName + c_userExistsQuery2;
                using (SQLiteDataReader reader = s_sqlCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        throw new ArgumentException("Username must not already exist", "userName");
                    }
                }

                const string c_insertComponent1 = "INSERT INTO User (Username,Name,LastAccessTime) values ('";
                const string c_insertComponent2 = "','";
                const string c_insertComponent3 = "')";

                s_sqlCommand.CommandText = c_insertComponent1 + 
                    userName + 
                    c_insertComponent2 + 
                    name + 
                    c_insertComponent2 + 
                    "0" + 
                    c_insertComponent3;

                s_sqlCommand.ExecuteNonQuery();                
            }
        }

        public static void InsertSensorData(long computerComponentId, long sensorTypeID, double value)
        {
            lock (s_lockObject)
            {
                const string c_insertComponent1 = "INSERT INTO SensorData (ComputerComponentID,Date,SensorTypeID,Value) values ('";
                const string c_insertComponent2 = "','";
                const string c_insertComponent3 = "')";

                s_sqlCommand.CommandText = c_insertComponent1 +
                    computerComponentId +
                    c_insertComponent2 +
                    ((int)(DateTime.UtcNow - s_unixEpoch).TotalSeconds) +
                    c_insertComponent2 +
                    sensorTypeID +
                    c_insertComponent2 +
                    value +
                    c_insertComponent3;

                s_sqlCommand.ExecuteNonQuery();
            }
        }
        #endregion
    }
}
