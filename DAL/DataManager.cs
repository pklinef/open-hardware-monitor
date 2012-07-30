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
using System.Threading;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor.DAL
{    

    public class DataManagerData
    {
        public DateTime TimeStamp { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public double Measure { get; set; }
    }

    public class DataManager
    {
        private const double ThresholdMultiplier = 1.0;
        private static Object s_lockObject = new Object();
        private string _dbFile;
        private SQLiteConnection _sqliteConnection;
        private static DataManager s_dataManager = new DataManager();
        private static Boolean s_transactionStarted = false;
        private static long s_macAddress = -1;

        // Items for aggregation. We'll aggregate to the hour and day
        private static DateTime s_lastHourAggregation = DateTime.MinValue;
        private static DateTime s_lastDayAggregation = DateTime.MinValue;

        public static List<ComponentSensorTypesContainer> AggregatedData { get; set; }
        private static HttpClient s_httpClient = new HttpClient();

        private DataManager()
        {
            _dbFile = "ohm.db";
            string currentPath;
            string absolutePath;

            currentPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath); 
            absolutePath = System.IO.Path.Combine(currentPath, _dbFile);

            SQLiteConnectionStringBuilder connBuilder = new SQLiteConnectionStringBuilder();
            connBuilder.DataSource = absolutePath;
            connBuilder.Version = 3;
            // enable write ahead logging
            connBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
            connBuilder.LegacyFormat = false;

            _sqliteConnection = new SQLiteConnection(connBuilder.ToString());
            _sqliteConnection.Open();
        }

        #region Initialize

        public static void Initialize()
        {
            BeginTransaction();
            CreateTables();
            InitializeSensorTypeTable();
            GetComputerId(GetMacAddress);
            EndTransaction();
        }

        private static void CreateTables()
        {
            lock (s_lockObject)
            {
                List<String> createStatements = new List<String>()
                {
                    @"CREATE TABLE IF NOT EXISTS [Component] (
                        [ComponentID] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
                        [Name] VARCHAR(100)  NOT NULL,
                        [Type] VARCHAR(25)  NOT NULL)",

                    @"CREATE TABLE IF NOT EXISTS [Computer] (
                        [ComputerID] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
                        [MACAddress] INTEGER  NOT NULL,
                        [Username] VARCHAR(25)  NULL,
                        [IPAddress] INTEGER  NOT NULL,
                        [MachineName] VARCHAR(50)  NULL,
                        [LastAccessTime] TIMESTAMP  NULL)",

                    @"CREATE TABLE IF NOT EXISTS [ComputerComponent] (
                        [ComputerComponentID] TEXT  NOT NULL,
                        [ComputerID] INTEGER  NOT NULL,
                        [ComponentID] INTEGER  NOT NULL,
                        [ParentComputerComponentID] TEXT  NULL,
                        PRIMARY KEY (ComputerComponentID, ComputerID))",

                    @"CREATE TABLE IF NOT EXISTS [SensorType] (
                        [SensorTypeID] TEXT  NOT NULL PRIMARY KEY,
                        [Name] VARCHAR(25)  NOT NULL,
                        [Units] VARCHAR(10)  NULL)",

                    @"CREATE TABLE IF NOT EXISTS [ComponentSensor] (
                        [ComponentSensorID] INTEGER  NOT NULL PRIMARY KEY AUTOINCREMENT,
                        [ComputerComponentID] TEXT NOT NULL,
                        [ComputerID] INTEGER NOT NULL,
                        [SensorID] TEXT NOT NULL,
                        [SensorName] TEXT NOT NULL,
                        [SensorTypeID] INTEGER NOT NULL)",

                    @"CREATE TABLE IF NOT EXISTS [SensorData] (
                        [ComponentSensorID] INTEGER  NOT NULL,
                        [Date] TIMESTAMP  NULL,
                        [Value] REAL  NOT NULL,
                        PRIMARY KEY (ComponentSensorID, [Date]))",

                    @"CREATE TABLE IF NOT EXISTS [User] (
                        [Username] VARCHAR(25)  PRIMARY KEY NULL,
                        [Name] VARCHAR(100)  NOT NULL,
                        [LastAccessTime] TIMESTAMP  NULL)",

                    @"CREATE TABLE IF NOT EXISTS HistoricalAggregation
                        (ComponentSensorID INTEGER,
                        [Date] TIMESTAMP,
                        DateRange INTEGER,
                        [Count] INTEGER,
                        [Sum] REAL,
                        SumOfSquares REAL,
                        [Min] REAL,
                        [Max] REAL,
                        PRIMARY KEY (ComponentSensorID,
                        [Date],DateRange))",

                     @"CREATE TABLE IF NOT EXISTS [Watermarks] (
                        [WatermarkId] TEXT NOT NULL PRIMARY KEY,
                        [Date] TIMESTAMP  NULL)",

                    @"CREATE TABLE IF NOT EXISTS ServerAggregation
                        (ComponentID INTEGER NOT NULL,
                        SensorTypeID INTEGER NO NULL,
                        [Count] INTEGER,
                        [Sum] REAL,
                        SumOfSquares REAL,
                        [Min] REAL,
                        [Max] REAL,
                        PRIMARY KEY (ComponentID, SensorTypeID))",

                    @"CREATE TABLE IF NOT EXISTS [Registry] (
                        [RegistryKey] VARCHAR(200)  PRIMARY KEY NULL,
                        [RegistryValue] VARCHAR(200))",
                };

                foreach (String statement in createStatements)
                {
                    using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        command.CommandText = statement;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private static string GetDataInRegistry(string registryKey)
        {
            string retVal = null;

            const string c_selecttHistoricalData = "SELECT RegistryValue FROM Registry WHERE RegistryKey = @registryKey";

            // If the row already exists, we need to add the data together
            using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
            {
                sqlSelectCommand.CommandText = c_selecttHistoricalData;
                sqlSelectCommand.Parameters.Add(new SQLiteParameter("@registryKey", registryKey));
                
                using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        retVal = Convert.ToString(reader["RegistryValue"]);
                    }
                }
            }        

            return retVal;
        }

        private static void SetDataInRegistry(string registryKey, string registryValue)
        {
            const string c_selecttHistoricalData = "INSERT OR REPLACE INTO Registry (RegistryKey, RegistryValue) VALUES(@registryKey, @registryValue)";

            // If the row already exists, we need to add the data together
            using (SQLiteCommand sqlCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
            {
                sqlCommand.CommandText = c_selecttHistoricalData;
                sqlCommand.Parameters.Add(new SQLiteParameter("@registryKey", registryKey));
                sqlCommand.Parameters.Add(new SQLiteParameter("@registryValue", registryValue));
                sqlCommand.ExecuteNonQuery();
            }

        }

        public static void SetEmailSettings(string SMTPServer, string EmailAddress)
        {
            SetDataInRegistry("SMTPServer", SMTPServer);
            SetDataInRegistry("EmailAddress", EmailAddress);
        }

        public static void GetEmailSettings(out string SMTPServer, out string EmailAddress)
        {
            SMTPServer = GetDataInRegistry("SMTPServer");
            EmailAddress = GetDataInRegistry("EmailAddress");
        }

        private static void InitializeSensorTypeTable()
        {
            lock (s_lockObject)
            {
                SQLiteDataReader reader;
                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "SELECT * FROM SensorType";
                    reader = command.ExecuteReader();

                    if (reader.HasRows)
                        return;
                }

                Dictionary<SensorType, String> sensorTypes = new Dictionary<SensorType, string>()
                {
                    {SensorType.Voltage, "V"},
                    {SensorType.Clock, "MHz"},
                    {SensorType.Temperature, "°C"},
                    {SensorType.Load, "%"},
                    {SensorType.Fan, "RPM"},
                    {SensorType.Flow, "L/h"},
                    {SensorType.Control, "%"},
                    {SensorType.Level, "%"},
                    {SensorType.Factor, "1"},
                    {SensorType.Power, "W"},
                    {SensorType.Data, "GB"},
                };

                const string c_insertSensorType = "INSERT INTO SensorType (SensorTypeID,Name,Units) values (@sensorTypeId,@name,@units)";
                foreach (KeyValuePair<SensorType, String> pair in sensorTypes)
                {
                    using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        command.CommandText = c_insertSensorType;
                        command.Parameters.Add(new SQLiteParameter("@sensorTypeId", pair.Key));
                        command.Parameters.Add(new SQLiteParameter("@name", pair.Key.ToString()));
                        command.Parameters.Add(new SQLiteParameter("@units", pair.Value));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        #endregion

        #region Get Methods

        /// <summary>
        /// Gets the data from the server to return to the client. Null is allowed for the name only
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="sensorType"></param>
        /// <param name="average"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="stddev"></param>
        /// <returns>True if data is found, false otherwise</returns>
        public static bool GetData(string name, string type, SensorType sensorType, out double average, out double min, out double max, out double stddev)
        {
            average = 0;
            min = Double.MaxValue;
            max = Double.MinValue;
            stddev = 0;
            const string c_selectSensorDataWithName = 
                @"SELECT Count,Sum,SumOfSquares,Min,Max 
                 FROM ServerAggregation sa 
                 JOIN Component ct ON sa.ComponentId = ct.ComponentId 
                 WHERE ct.Name = @name 
                   AND ct.Type = @type 
                   AND sa.SensorTypeId = @sensorTypeId";

            const string c_selectSensorDataWithoutName = 
                @"SELECT Count,Sum,SumOfSquares,Min,Max 
                 FROM ServerAggregation sa 
                 JOIN Component ct ON sa.ComponentId = ct.ComponentId 
                 WHERE ct.Type = @type 
                   AND sa.SensorTypeId = @sensorTypeId";

            long count = 0;
            double sum = 0;
            double sumOfSquares = 0;

            using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
            {
                if (!String.IsNullOrEmpty(name))
                {
                    sqlSelectCommand.CommandText = c_selectSensorDataWithName;
                    sqlSelectCommand.Parameters.Add(new SQLiteParameter("@name", name));
                }
                else
                {
                    sqlSelectCommand.CommandText = c_selectSensorDataWithoutName;
                }
                sqlSelectCommand.Parameters.Add(new SQLiteParameter("@type", type));
                sqlSelectCommand.Parameters.Add(new SQLiteParameter("@sensorTypeId", (long)sensorType));

                using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        double tempMin = Math.Min(min, Convert.ToDouble(reader["Min"]));
                        min = Math.Min(min, tempMin);

                        double tempMax = Math.Max(max, Convert.ToDouble(reader["Max"]));
                        max = Math.Max(max, tempMax);
                        
                        count += Convert.ToInt64(reader["Count"]);
                        sum += Convert.ToDouble(reader["Sum"]);
                        sumOfSquares += Convert.ToDouble(reader["SumOfSquares"]);
                    }

                    if (count > 0)
                    {
                        average = (double)sum / count;
                        stddev = Math.Sqrt((sumOfSquares / count) - (average * average));
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool GetAggregateData(long componentSensorId, out double min, out double max, out double avg, out double stddev)
        {
            min = 0.0;
            max = 0.0;
            avg = 0.0;
            stddev = 0.0;

            if (AggregatedData == null)
                return false;

            lock (s_lockObject)
            {

                const string c_query = "SELECT DISTINCT cp.Type, st.Name FROM ((Component cp JOIN ComputerComponent cc ON cp.ComponentID = cc.ComponentID) JOIN ComponentSensor cs ON cc.ComputerComponentID = cs.ComputerComponentID) JOIN SensorType st ON cs.SensorTypeID = st.SensorTypeID where ComponentSensorID=@componentSensorId;";

                SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection);
                sqlQueryCommand.CommandText = c_query;
                sqlQueryCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));

                ComponentSensorTypesContainer cstc = new ComponentSensorTypesContainer();
                List<ComponentSensorTypesContainer> list = new List<ComponentSensorTypesContainer>();
                using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cstc.ComponentType = (string)reader["Type"];
                        cstc.SensorName = (SensorType)Enum.Parse(typeof(SensorType), (string)reader["Name"]);
                    }
                }

                if (cstc.ComponentType == null || cstc.ComponentType == "")
                    return false;

                foreach (var item in AggregatedData)
                {
                    if (item.ComponentType == cstc.ComponentType && item.SensorName == cstc.SensorName)
                    {
                        min = item.Min;
                        max = item.Max;
                        avg = item.Avg;
                        stddev = item.StdDev;

                        return true;
                    }
                }
            }

            return false;
        }

        private static List<ComponentSensorTypesContainer> GetComponentSensorList()
        {
            List<ComponentSensorTypesContainer> list = new List<ComponentSensorTypesContainer>();
            lock (s_lockObject)
            {

                const string c_query = "SELECT DISTINCT cp.Type, st.Name FROM ((Component cp JOIN ComputerComponent cc ON cp.ComponentID = cc.ComponentID) JOIN ComponentSensor cs ON cc.ComputerComponentID = cs.ComputerComponentID) JOIN SensorType st ON cs.SensorTypeID = st.SensorTypeID;";

                SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection);
                sqlQueryCommand.CommandText = c_query;

                using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ComponentSensorTypesContainer
                        {
                            ComponentType = (string)reader["Type"],
                            SensorName = (SensorType) Enum.Parse(typeof(SensorType), (string)reader["Name"]) 
                        });
                    }
                }
            }
            return list;
        }

        public static List<DataManagerData> GetDataForSensor(long componentSensorId, DateTime startTime, TimeSpan timeRange, DateRangeType minResolution, DateRangeType maxResolution, out double average, out double min, out double max, out double stddev)
        {
            if (maxResolution > minResolution)
            {
                throw new ArgumentException("max resolution must be finer (less) than the min resolution!");
            }

            if (maxResolution > DateRangeType.hour)
            {
                throw new ArgumentException("We don't aggregate hours into days dynamically yet!");
            }

            max = Double.MinValue;
            min = Double.MaxValue;
            average = 0;
            stddev = 0;

            double sum = 0;
            double sumOfSquares = 0;
            long count = 0;

            List<DataManagerData> retVal = new List<DataManagerData>();

            lock (s_lockObject)
            {
                DateTime timeCutoff = RoundDownToHour(DateTime.UtcNow.AddHours(-1));
                
                // Let's see if we need to grab the historical data
                if (startTime < timeCutoff)
                {
                    const string c_selecttHistoricalData = "SELECT Count,Sum,DateRange,Date,SumOfSquares,Min,Max FROM HistoricalAggregation WHERE ComponentSensorID = @componentSensorId AND Date >= @minDate AND Date < @maxDate AND DateRange >= @maxResolution AND DateRange <= @minResolution ORDER BY Date ASC, DateRange DESC";

                    // If the row already exists, we need to add the data together
                    using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        sqlSelectCommand.CommandText = c_selecttHistoricalData;
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@minDate", startTime));

                        // Only pull coarse grain info up to the cutoff. We have fine grained info above the cutoff
                        DateTime maxDate = (startTime + timeRange < timeCutoff) ? startTime + timeRange : timeCutoff;
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@maxDate", maxDate));
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@maxResolution", maxResolution));
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@minResolution", minResolution));

                        using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DataManagerData data = new DataManagerData();
                                DateRangeType currentRangeType = (DateRangeType)Convert.ToInt32(reader["DateRange"]);
                                data.TimeSpan = GetTimeSpanFromDateRangeType(currentRangeType);
                                data.TimeStamp = DateTime.SpecifyKind(Convert.ToDateTime(reader["Date"]), DateTimeKind.Local).ToUniversalTime();

                                // We'll always assume Count != 0 for this project
                                data.Measure = Convert.ToDouble(reader["Sum"]) / (double)(Convert.ToInt64(reader["Count"]));
                                retVal.Add(data);

                                min = Math.Min(min, Convert.ToDouble(reader["Min"]));
                                max = Math.Max(max, Convert.ToDouble(reader["Max"]));
                                sumOfSquares += Convert.ToDouble(reader["SumOfSquares"]);
                                sum += Convert.ToDouble(reader["Sum"]);
                                count += Convert.ToInt64(reader["Count"]);
                            }
                        }
                    }
                }

                // Let's see if we need to grab from the fine grain data
                if (startTime + timeRange > timeCutoff)
                {
                    const string c_selectSensorData = "SELECT Date,Value FROM SensorData WHERE Date < @maxTime AND Date >= @minTime AND ComponentSensorId = @componentSensorId ORDER BY Date ASC";

                    using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                    {
                        sqlSelectCommand.CommandText = c_selectSensorData;
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@minTime", startTime));
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@maxTime", startTime + timeRange));
                        sqlSelectCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                        
                        using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                        {
                            DateTime lastTime = DateTime.MinValue;
                            double tempSum = 0;
                            double tempCount = 0;
                            
                            // We need to aggregate the seconds together if the max resolution is coarser than a second
                            while (reader.Read())
                            {
                                DataManagerData data = new DataManagerData();
                                data.TimeStamp = Convert.ToDateTime(reader["Date"]);
                                data.Measure = Convert.ToDouble(reader["Value"]);
                                
                                DateTime currentTime = RoundDownToDateRangeType(data.TimeStamp, maxResolution);
                                if (currentTime > lastTime || maxResolution <= DateRangeType.second)
                                {
                                    if (lastTime > DateTime.MinValue)
                                    {
                                        DataManagerData newData = new DataManagerData();
                                        newData.TimeStamp = lastTime;
                                        newData.TimeSpan = GetTimeSpanFromDateRangeType(maxResolution);
                                        newData.Measure = (tempSum / (double)tempCount);
                                        retVal.Add(newData);
                                        min = Math.Min(min, newData.Measure);
                                        max = Math.Max(max, newData.Measure);
                                        sumOfSquares += newData.Measure * newData.Measure;
                                        sum += newData.Measure;
                                        count++;
                                        tempSum = 0;
                                        tempCount = 0;
                                    }

                                    lastTime = currentTime;                                    
                                }

                                tempSum += data.Measure;
                                tempCount++;
                            }

                            if (lastTime > DateTime.MinValue)
                            {
                                DataManagerData newData = new DataManagerData();
                                newData.TimeStamp = lastTime;
                                newData.TimeSpan = GetTimeSpanFromDateRangeType(maxResolution);
                                newData.Measure = (tempSum / (double)tempCount);
                                retVal.Add(newData);
                                min = Math.Min(min, newData.Measure);
                                max = Math.Max(max, newData.Measure);
                                sumOfSquares += newData.Measure * newData.Measure;
                                sum += newData.Measure;
                                count++;
                                tempSum = 0;
                                tempCount = 0;
                            }
                        }
                    }
                }
            }

            if (count > 0)
            {
                average = sum / count;
                stddev = Math.Sqrt((sumOfSquares / count) - (average * average));
            }            

            return retVal;
        }

        public class AlertContainer
        {
            public string ComponentName { get; set; }
            public string SensorName { get; set; }
            public double AverageOverLastMinute { get; set; }
            public SensorType SensorType { get; set; }
        }

        public static List<AlertContainer> GetAlerts()
        {
            List<AlertContainer> retVal = new List<AlertContainer>();
            lock (LastSamples)
            {
                List<long> warningsToRemove = new List<long>();
                foreach (long componentSensorId in FlaggedWarnings)
                {
                    SensorReadingContainer readingContainer;
                    if (LastSamples.TryGetValue(componentSensorId, out readingContainer))
                    {
                        if (readingContainer.WarnUntilTime < DateTime.UtcNow ||
                            readingContainer.Readings.Count == 0)
                        {
                            warningsToRemove.Add(componentSensorId);
                            continue;
                        }

                        AlertContainer container = new AlertContainer();
                        container.AverageOverLastMinute = readingContainer.Sum / readingContainer.Readings.Count;

                        const string c_selecttHistoricalData = 
                            @"SELECT ct.Name,cs.SensorName, cs.SensorTypeId
                             FROM ComponentSensor cs
                             JOIN ComputerComponent cc
                             ON cs.ComputerComponentId = cc.ComputerComponentId
                             JOIN Component ct
                             ON ct.ComponentId = cc.ComponentId 
                             WHERE cs.ComponentSensorId = @componentSensorId";

                        // If the row already exists, we need to add the data together
                        using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlSelectCommand.CommandText = c_selecttHistoricalData;
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                            
                            using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    container.ComponentName = Convert.ToString(reader["Name"]);
                                    container.SensorName = Convert.ToString(reader["SensorName"]);
                                    long sensorTypeId = Convert.ToInt64(reader["SensorTypeID"]);
                                    container.SensorType = (SensorType)sensorTypeId;

                                    retVal.Add(container);                                    
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }

                        string smtpServer;
                        string emailAddress;
                        GetEmailSettings(out smtpServer, out emailAddress);

                        if (!readingContainer.EmailSent && !String.IsNullOrEmpty(smtpServer) && !String.IsNullOrEmpty(emailAddress))
                        {
                            try
                            {
                                System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
                                message.To.Add(emailAddress);
                                message.Subject = "Alert: Your computer is running hotter than normal!";
                                message.From = new System.Net.Mail.MailAddress(emailAddress);
                                message.Body = "Alert: Your computer is running hotter than normal!\r\nName: " + container.ComponentName + "\r\nSensorName: " + container.SensorName + "\r\nAverage over last minute: " + container.AverageOverLastMinute;
                                System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient(smtpServer);
                                smtp.Send(message);
                                readingContainer.EmailSent = true;
                            }
                            catch (Exception)
                            {
                                // We'll just eat these
                            }
                        }
                    }
                }

                foreach (long componentSensorId in warningsToRemove)
                {
                    FlaggedWarnings.Remove(componentSensorId);
                }
            }

            return retVal;
        }
        
        public static long GetMacAddress
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

        public static long GetComponentSensorId(String fullSensorPath)
        {
            if (String.IsNullOrEmpty(fullSensorPath))
            {
                throw new ArgumentException("Sensor path must not be null or empty", "fullSensorPath");
            }

            long componentSensorId = -1;
            lock (s_lockObject)
            {
                const string c_getCompoenntIdQuery = "SELECT ComponentSensorId, ComputerComponentID || SensorID as SensorPath from ComponentSensor WHERE SensorPath = @sensorPath";

                SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection);
                sqlQueryCommand.CommandText = c_getCompoenntIdQuery;
                sqlQueryCommand.Parameters.Add(new SQLiteParameter("@sensorPath", fullSensorPath));

                using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        componentSensorId = Convert.ToInt64(reader["ComponentSensorId"]);
                    }
                }
            }
            return componentSensorId;
        }

        public static long GetComponentId(string name, string type)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name must not be null or empty", "type");
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
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));
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
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));
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

        public static long GetComputerId(long macAddress)
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
                            computerId = Convert.ToInt64(reader["ComputerID"]);
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
                        sqlInsertCommand.Parameters.Add(new SQLiteParameter("@ip", 1));  // TODO get actual IP
                        sqlInsertCommand.ExecuteNonQuery();
                        computerId = s_dataManager._sqliteConnection.LastInsertRowId;
                    }
                }
            }

            return computerId;
        }

        public static DateTime GetLastAccessTime()
        {
            const string c_getLastAccessTime = "SELECT LastAccessTime FROM Computer WHERE ComputerID = @computerId;";

            lock (s_lockObject)
            {
                using (SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlQueryCommand.CommandText = c_getLastAccessTime;
                    sqlQueryCommand.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));

                    using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["LastAccessTime"].ToString() == "")
                                return DateTime.UtcNow;
                            return DateTime.Parse(reader["LastAccessTime"].ToString());
                        }
                    }
                }
            }
            return DateTime.UtcNow;
        }

        #endregion

        #region Insert Methods

        /// <summary>
        /// Inserts data into the server database. Nothing is allowed to be null
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="sensorType"></param>
        /// <param name="average"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="stddev"></param>
        /// <returns>true if inserted, false otherwise</returns>
        public static bool InsertData(List<AggregateContainer> dataToInsert)
        {
            lock (s_lockObject)
            {
                using (SQLiteTransaction dbTrans = s_dataManager._sqliteConnection.BeginTransaction())
                {
                    try
                    {
                        foreach (AggregateContainer container in dataToInsert)
                        {
                            // Type cannot be null
                            if (container.Type == null)
                            {
                                continue;
                            }

                            long componentId = GetComponentId(container.Name, container.Type);

                            bool alreadyExists = false;

                            using (SQLiteCommand sqlQueryCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                            {
                                const string c_dataExistsQuery = "SELECT ComponentID FROM ServerAggregation WHERE ComponentID = @componentID AND SensorTypeID = @sensorTypeID";
                                sqlQueryCommand.CommandText = c_dataExistsQuery;
                                sqlQueryCommand.Parameters.Add(new SQLiteParameter("@componentID", componentId));
                                sqlQueryCommand.Parameters.Add(new SQLiteParameter("@sensorTypeID", container.SensorType));
                                using (SQLiteDataReader reader = sqlQueryCommand.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        alreadyExists = true;
                                    }
                                }
                            }

                            using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                            {
                                const string c_sqlCommandAlreadyExists =
                                    @"UPDATE ServerAggregation 
                                    SET [Count] = [Count] + @count, 
                                    [Sum] = [Sum] + @sum, 
                                    SumOfSquares = SumOfSquares + @sumOfSquares, 
                                    [Min] = CASE WHEN ([Min] <= @min) THEN [Min] ELSE @min END, 
                                    [Max] = CASE WHEN ([Max] >= @max) THEN [Max] ELSE @max END 
                                    WHERE ComponentID = @componentID AND SensorTypeID = @sensorTypeID;";
                                const string c_sqlCommandInsert =
                                    @"INSERT INTO ServerAggregation (ComponentID,SensorTypeID,Count,Sum,SumOfSquares,Min,Max) values (@componentID,@sensorTypeID,@count,@sum,@sumOfSquares,@min,@max);";

                                sqlInsertCommand.CommandText = (alreadyExists) ? c_sqlCommandAlreadyExists : c_sqlCommandInsert;
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@componentID", componentId));
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@sensorTypeID", container.SensorType));
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@count", container.Count));
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@sum", container.Sum));
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@sumOfSquares", container.SumOfSquares));
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@min", container.Min));
                                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@max", container.Max));

                                sqlInsertCommand.ExecuteNonQuery();
                            }
                        }

                        dbTrans.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        // Just eat it for now, we'll retry later
                        dbTrans.Rollback();
                    }
                }
            }
            return false;
        }

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
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@lastAccessTime", DateTime.UtcNow));

                    sqlInsertCommand.ExecuteNonQuery();
                }
            }
        }

        private static void InsertHistoricalData(long componentSensorId, DateTime measureTime, DateRangeType dateRangeType, long count, double sum, double sumOfSquares, double min, double max)
        {
            const string c_selecttHistoricalData = "SELECT Count,Sum,SumOfSquares,Min,Max FROM HistoricalAggregation WHERE ComponentSensorID = @componentSensorId AND Date = @date AND DateRange = @dateRange";

            bool alreadyExists = false;

            // If the row already exists, we need to add the data together
            using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
            {
                sqlSelectCommand.CommandText = c_selecttHistoricalData;
                sqlSelectCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                sqlSelectCommand.Parameters.Add(new SQLiteParameter("@date", measureTime));
                sqlSelectCommand.Parameters.Add(new SQLiteParameter("@dateRange", dateRangeType));

                using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        alreadyExists = true;
                        count += Convert.ToInt64(reader["Count"]);
                        sum += Convert.ToDouble(reader["Sum"]);
                        sumOfSquares += Convert.ToDouble(reader["SumOfSquares"]);
                        double tempMin = Convert.ToDouble(reader["Min"]);
                        min = Math.Min(min, tempMin);
                        double tempMax = Convert.ToDouble(reader["Max"]);
                        max = Math.Max(max, tempMax);
                    }
                }
            }

            // If a row does already exist, it's equivalent and easier to delete and reinsert rather than update since
            // we're updating most of the row. This can almost never happen
            if (alreadyExists)
            {
                const string c_deleteSensorData = "DELETE FROM HistoricalAggregation WHERE ComponentSensorID = @componentSensorId AND Date = @date AND DateRange = @dateRange";
                using (SQLiteCommand sqlDeleteCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlDeleteCommand.CommandText = c_deleteSensorData;

                    sqlDeleteCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                    sqlDeleteCommand.Parameters.Add(new SQLiteParameter("@date", measureTime));
                    sqlDeleteCommand.Parameters.Add(new SQLiteParameter("@dateRange", dateRangeType));


                    sqlDeleteCommand.ExecuteNonQuery();
                }
            }

            const string c_insertHistoricalData = "INSERT INTO HistoricalAggregation (ComponentSensorID,Date,DateRange,Count,Sum,SumOfSquares,Min,Max) values (@componentSensorId,@date,@dateRange,@count,@sum,@sumOfSquares,@min,@max)";

            using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
            {
                sqlInsertCommand.CommandText = c_insertHistoricalData;
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@date", measureTime));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@dateRange", dateRangeType));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@count", count));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@sum", sum));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@sumOfSquares", sumOfSquares));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@min", min));
                sqlInsertCommand.Parameters.Add(new SQLiteParameter("@max", max));

                sqlInsertCommand.ExecuteNonQuery();
            }
        }

        public static void InsertSensorData(String computerComponentId, String sensorID, int sensorTypeID, double value)
        {
            ThreadPool.QueueUserWorkItem(AggregateHistoricalData, null);
            lock (s_lockObject)
            {
                Int64 componentSensorId = -1;
                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "SELECT ComponentSensorID FROM ComponentSensor WHERE ComputerComponentID = @computerComponentId AND SensorID = @sensorId AND SensorTypeID = @sensorTypeId AND ComputerID = @computerID";
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", computerComponentId));
                    command.Parameters.Add(new SQLiteParameter("@sensorId", sensorID));  
                    command.Parameters.Add(new SQLiteParameter("@sensorTypeId", sensorTypeID));
                    command.Parameters.Add(new SQLiteParameter("@computerID", GetComputerId(GetMacAddress)));
                    SQLiteDataReader reader= command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        while (reader.Read())
                            componentSensorId = Convert.ToInt64(reader["ComponentSensorID"]);
                    }
                    else
                        return;
                }

                const string c_insertSensorData = "INSERT INTO SensorData (ComponentSensorID, Date, Value) values (@componentSensorId, @date, @Value)";
                DateTime currentTime = DateTime.UtcNow;
                using (SQLiteCommand sqlInsertCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlInsertCommand.CommandText = c_insertSensorData;
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@componentSensorId", componentSensorId));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@date", currentTime));
                    sqlInsertCommand.Parameters.Add(new SQLiteParameter("@Value", value));

                    sqlInsertCommand.ExecuteNonQuery();
                }

                if (sensorTypeID == (long)SensorType.Temperature)
                {
                    lock (LastSamples)
                    {
                        Dictionary<long, Threshold> thresholds = Thresholds;
                        Threshold threshold;
                        if (thresholds.TryGetValue(componentSensorId, out threshold))
                        {
                            SensorReadingContainer readingContainer;
                            if (!LastSamples.TryGetValue(componentSensorId, out readingContainer))
                            {
                                readingContainer = new SensorReadingContainer();
                                LastSamples[componentSensorId] = readingContainer;
                            }

                            // Remove old readings
                            while (readingContainer.Readings.First != null &&
                                readingContainer.Readings.First.Value.SampleTime < currentTime.AddSeconds(-60))
                            {
                                SensorReading currentReading = readingContainer.Readings.First.Value;
                                readingContainer.Readings.RemoveFirst();

                                if (//currentReading.Sum < threshold.LowerThreshold ||
                                    currentReading.Sum > threshold.UpperThreshold)
                                {
                                    readingContainer.CountOutsideRange--;
                                }
                                readingContainer.Sum -= currentReading.Sum;
                            }

                            SensorReading lastReading = new SensorReading();
                            lastReading.SumOfSquares = value * value;
                            lastReading.Sum = value;
                            lastReading.SampleTime = currentTime;
                            lastReading.Count = 1;

                            if (lastReading.Sum > threshold.UpperThreshold /*||
                                lastReading.Sum < threshold.LowerThreshold*/)
                            {
                                readingContainer.CountOutsideRange++;
                                if (readingContainer.CountOutsideRange > 30)
                                {
                                    readingContainer.WarnUntilTime = currentTime.AddMinutes(5);
                                    if (!FlaggedWarnings.Contains(componentSensorId))
                                    {
                                        readingContainer.EmailSent = false;
                                        FlaggedWarnings.Add(componentSensorId);

                                        // This will trigger an email being sent if necessary
                                        GetAlerts();            
                                    }
                                }
                            }
                            readingContainer.Sum += lastReading.Sum;
                            readingContainer.Readings.AddLast(lastReading);
                        }
                    }
                }
            }
        }

        public static void AddHardware(Hardware.IHardware hardware)
        {
            lock (s_lockObject)
            {
                //lets check if this computer-component combo has already been added
                SQLiteDataReader reader;
                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {

                    command.CommandText = "SELECT * FROM ComputerComponent WHERE ComputerComponentID = @computerComponentId AND ComputerID = @computerID";
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", hardware.Identifier.ToString()));
                    command.Parameters.Add(new SQLiteParameter("@computerID", GetComputerId(GetMacAddress)));
                    reader = command.ExecuteReader();

                    if (reader.HasRows)
                        return;
                }

                Int64 componentId = GetComponentId(hardware);

                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "INSERT INTO ComputerComponent (ComputerComponentID, ComputerID, ComponentID, ParentComputerComponentID) values (@computerComponentId, @computerId, @componentId, @parentComputerComponentId)";
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", hardware.Identifier.ToString()));
                    command.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));
                    command.Parameters.Add(new SQLiteParameter("@componentId", componentId));
                    command.Parameters.Add(new SQLiteParameter("@parentComputerComponentId", (hardware.Parent == null?"":hardware.Parent.Identifier.ToString())));
                    command.ExecuteNonQuery();
                }
            }
        }

        private static Int64 GetComponentId(Hardware.IHardware hardware)
        {
            SQLiteDataReader reader;
                
            //has this hardware component been added before?
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
                    {
                        componentId = Convert.ToInt64(reader["ComponentID"]);
                    }
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
                    {
                        componentId = Convert.ToInt64(reader["ComponentID"]);
                    }
                }
            }
            return componentId;
        }

        #endregion

        #region Update Methods

        public static void UpdateLastAccessTime()
        {
            const string c_updateLastAccessTime = "UPDATE Computer SET LastAccessTime = @lastAccessTime WHERE ComputerID = @computerId;";

            lock (s_lockObject)
            {
                using (SQLiteCommand sqlUpdateCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    sqlUpdateCommand.CommandText = c_updateLastAccessTime;
                    sqlUpdateCommand.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));
                    sqlUpdateCommand.Parameters.Add(new SQLiteParameter("@lastAccessTime", DateTime.UtcNow));

                    sqlUpdateCommand.ExecuteNonQuery();
                }
            }
        }

        #endregion

        public static void RegisterSensor(string computerComponentId, string sensorId, string sensorName, int sensorTypeId)
        {
            lock (s_lockObject)
            {
                //lets check if this sensor has already been registered
                SQLiteDataReader reader;
                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "SELECT ComponentSensorID FROM ComponentSensor WHERE ComputerID = @computerID AND ComputerComponentID = @computerComponentID AND SensorID = @sensorID AND SensorTypeID = @sensorTypeID";
                    command.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", computerComponentId));
                    command.Parameters.Add(new SQLiteParameter("@sensorId", sensorId));
                    command.Parameters.Add(new SQLiteParameter("@sensorTypeId", sensorTypeId));
                    reader = command.ExecuteReader();

                    if (reader.HasRows)
                        return;
                }

                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                {
                    command.CommandText = "INSERT INTO ComponentSensor (ComputerID,ComputerComponentID, SensorID, SensorName, SensorTypeID) values (@computerID,@computerComponentId, @sensorId, @sensorName, @sensorTypeId)";
                    command.Parameters.Add(new SQLiteParameter("@computerId", GetComputerId(GetMacAddress)));
                    command.Parameters.Add(new SQLiteParameter("@computerComponentId", computerComponentId));
                    command.Parameters.Add(new SQLiteParameter("@sensorId", sensorId));
                    command.Parameters.Add(new SQLiteParameter("@sensorName", sensorName));
                    command.Parameters.Add(new SQLiteParameter("@sensorTypeId", sensorTypeId));
                    command.ExecuteNonQuery();
                }
            }
        }

        #region Helper Methods
        private static DateTime RoundDownToHour(DateTime dt)
        {
            dt = dt.ToUniversalTime();
            DateTime retVal = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);
            return retVal;
        }

        private static DateTime RoundDownToDay(DateTime dt)
        {
            dt = dt.ToUniversalTime();
            return dt.Date;
        }

        private static DateTime RoundDownToMinute(DateTime dt)
        {
            dt = dt.ToUniversalTime();
            DateTime retVal = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc);
            return retVal;
        }

        private static DateTime RoundDownToSecond(DateTime dt)
        {
            dt = dt.ToUniversalTime();
            DateTime retVal = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, DateTimeKind.Utc);
            return retVal;
        }

        private static DateTime RoundDownToDateRangeType(DateTime dt, DateRangeType drt)
        {
            switch (drt)
            {
                case DateRangeType.day:
                    return RoundDownToDay(dt);
                case DateRangeType.hour:
                    return RoundDownToHour(dt);
                case DateRangeType.minute:
                    return RoundDownToMinute(dt);
                case DateRangeType.second:
                default:
                    return RoundDownToSecond(dt);
            }
        }

        private static TimeSpan GetTimeSpanFromDateRangeType(DateRangeType drt)
        {
            switch (drt)
            {
                case DateRangeType.second:
                    return TimeSpan.FromSeconds(1);
                case DateRangeType.minute:
                    return TimeSpan.FromMinutes(1);
                case DateRangeType.hour:
                    return TimeSpan.FromHours(1);
                case DateRangeType.day:
                default:
                    return TimeSpan.FromDays(1);
            }
        }

        public enum DateRangeType
        {
            // Let's give ourselves some room
            second = 1,
            minute = 3,
            hour = 6,
            day = 10,
        }

        public class ComponentSensorTypesContainer
        {
            public string Name;
            public string ComponentType;
            public SensorType SensorName;
            public double Max = 0;
            public double Min = 0;
            public double Avg = 0;
            public double StdDev = 0;
        }

        public class AggregateContainer
        {
            public string Name;
            public string Type;
            public SensorType SensorType;
            public DateTime CurrentDay = DateTime.MinValue;
            public double Sum = 0;
            public double Max = 0;
            public double Min = 0;
            public double SumOfSquares = 0;
            public long Count = 0;            
        }

        public static void AggregateHistoricalData(object unused)
        {
            DateTime currentTime = DateTime.UtcNow;

            // We'll check the hour aggregation, since the day aggregation can only be triggered when the hour one is also triggered
            if (s_lastHourAggregation < (currentTime.AddHours(-1)))
            {
                lock (s_lockObject)
                {
                    if (s_lastHourAggregation < (currentTime.AddHours(-1)))
                    {
                        DateTime hourFloorTime = RoundDownToHour(currentTime);

                        const string c_selectSensorData = "SELECT ComponentSensorID,Date,Value FROM SensorData WHERE Date < @minTime ORDER BY ComponentSensorID ASC, Date ASC";

                        using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlSelectCommand.CommandText = c_selectSensorData;
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@minTime", hourFloorTime.AddHours(-1)));

                            using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                            {
                                long currentComponentSensorId = -1;
                                DateTime currentHourCutoff = DateTime.MinValue;

                                DateTime currentHour = DateTime.MinValue;
                                double sum = 0;
                                double max = 0;
                                double min = 0;
                                double sumOfSquares = 0;
                                long count = 0;

                                while (reader.Read())
                                {
                                    long componentSensorId = Convert.ToInt64(reader["ComponentSensorID"]);
                                    DateTime measureTime = Convert.ToDateTime(reader["Date"]);
                                    double measure = Convert.ToDouble(reader["Value"]);

                                    if (currentComponentSensorId != componentSensorId ||
                                        measureTime > currentHourCutoff)
                                    {
                                        // We don't want to save anything back if we're setting up
                                        if (currentComponentSensorId != -1)
                                        {
                                            InsertHistoricalData(currentComponentSensorId,
                                                currentHour,
                                                DateRangeType.hour,
                                                count,
                                                sum,
                                                sumOfSquares,
                                                min,
                                                max);
                                        }

                                        // Reset our aggregation
                                        currentComponentSensorId = componentSensorId;
                                        currentHour = RoundDownToHour(measureTime);
                                        currentHourCutoff = currentHour.AddHours(1);
                                        max = measure;
                                        min = measure;
                                        count = 0;
                                        sum = 0;
                                        sumOfSquares = 0;
                                    }

                                    max = Math.Max(max, measure);
                                    min = Math.Min(min, measure);
                                    count++;
                                    sum += measure;
                                    sumOfSquares += (measure * measure);
                                }

                                // Save the last set of measures. We don't want to save anything back if we're setting up
                                if (currentComponentSensorId != -1)
                                {
                                    InsertHistoricalData(currentComponentSensorId,
                                        currentHour,
                                        DateRangeType.hour,
                                        count,
                                        sum,
                                        sumOfSquares,
                                        min,
                                        max);
                                }
                            }
                        }

                        const string c_deleteSensorData = "DELETE FROM SensorData WHERE Date < @minTime";
                        using (SQLiteCommand sqlDeleteCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlDeleteCommand.CommandText = c_deleteSensorData;

                            // Always make sure we have one full hour of full resolution
                            sqlDeleteCommand.Parameters.Add(new SQLiteParameter("@minTime", hourFloorTime.AddHours(-1)));

                            sqlDeleteCommand.ExecuteNonQuery();
                        }

                        s_lastHourAggregation = hourFloorTime;
                    }

                    if (s_lastDayAggregation < (currentTime.AddDays(-1)))
                    {
                        DateTime dayFloorTime = RoundDownToDay(currentTime);

                        const string c_selecttHistoricalData = "SELECT ComponentSensorID,Date,Count,Sum,SumOfSquares,Min,Max FROM HistoricalAggregation WHERE Date < @date AND DateRange = @dateRange ORDER BY ComponentSensorID ASC, Date ASC";

                        // If the row already exists, we need to add the data together
                        using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlSelectCommand.CommandText = c_selecttHistoricalData;
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@date", dayFloorTime.AddDays(-1)));
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@dateRange", DateRangeType.hour));

                            using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                            {
                                long currentComponentSensorId = -1;
                                DateTime currentDayCutoff = DateTime.MinValue;

                                DateTime currentDay = DateTime.MinValue;
                                double sum = 0;
                                double max = 0;
                                double min = 0;
                                double sumOfSquares = 0;
                                long count = 0;

                                while (reader.Read())
                                {
                                    long componentSensorId = Convert.ToInt64(reader["ComponentSensorID"]);
                                    DateTime measureTime = Convert.ToDateTime(reader["Date"]);

                                    if (currentComponentSensorId != componentSensorId ||
                                        measureTime > currentDayCutoff)
                                    {
                                        // We don't want to save anything back if we're setting up
                                        if (currentComponentSensorId != -1)
                                        {
                                            InsertHistoricalData(currentComponentSensorId,
                                                currentDay,
                                                DateRangeType.day,
                                                count,
                                                sum,
                                                sumOfSquares,
                                                min,
                                                max);
                                        }

                                        // Reset our aggregation
                                        currentComponentSensorId = componentSensorId;
                                        currentDay = RoundDownToDay(measureTime);
                                        currentDayCutoff = currentDay.AddHours(1);
                                        max = Convert.ToDouble(reader["Max"]);
                                        min = Convert.ToDouble(reader["Min"]);
                                        count = 0;
                                        sum = 0;
                                        sumOfSquares = 0;
                                    }

                                    count += Convert.ToInt64(reader["Count"]);
                                    sum += Convert.ToDouble(reader["Sum"]);
                                    sumOfSquares += Convert.ToDouble(reader["SumOfSquares"]);
                                    double tempMin = Convert.ToDouble(reader["Min"]);
                                    min = Math.Min(min, tempMin);
                                    double tempMax = Convert.ToDouble(reader["Max"]);
                                    max = Math.Max(max, tempMax);
                                }

                                // Save the last set of measures. We don't want to save anything back if we're setting up
                                if (currentComponentSensorId != -1)
                                {
                                    InsertHistoricalData(currentComponentSensorId,
                                        currentDay,
                                        DateRangeType.day,
                                        count,
                                        sum,
                                        sumOfSquares,
                                        min,
                                        max);
                                }                                
                            }
                        }

                        List<AggregateContainer> dataToSendToServer = new List<AggregateContainer>();
                        DateTime lastWatermark = DateTime.MinValue;

                        // NOTE: SQL does not support "TOP 1", so we'll just read one
                        const string c_selecttWatermark = "SELECT Date FROM Watermarks WHERE WatermarkId = @watermarkId ORDER BY Date DESC";

                        // If the row already exists, we need to add the data together
                        using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlSelectCommand.CommandText = c_selecttWatermark;
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@watermarkId", "ServerWatermark"));

                            using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    lastWatermark = Convert.ToDateTime(reader["Date"]);

                                    // Go ahead and add one day so we don't resend data
                                    lastWatermark = lastWatermark.AddDays(1);
                                }
                            }
                        }

                        const string c_selecttHistoricalData1 =
                            @"SELECT ha.ComponentSensorId,Name,Type,SensorTypeId,Date,Count,Sum,SumOfSquares,Min,Max 
                             FROM HistoricalAggregation ha 
                             JOIN ComponentSensor cs 
                             ON cs.ComponentSensorId = ha.ComponentSensorId 
                             JOIN ComputerComponent cc
                             ON cs.ComputerComponentId = cc.ComputerComponentId
                             JOIN Component ct
                             ON ct.ComponentId = cc.ComponentId 
                             WHERE Date <= @maxDate 
                               AND DateRange = @dateRange 
                             ORDER BY Date ASC";

                        Dictionary<long, SensorReading> readings = new Dictionary<long, SensorReading>();

                        // If the row already exists, we need to add the data together
                        using (SQLiteCommand sqlSelectCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlSelectCommand.CommandText = c_selecttHistoricalData1;
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@maxDate", dayFloorTime.AddDays(-1)));
                            sqlSelectCommand.Parameters.Add(new SQLiteParameter("@dateRange", DateRangeType.day));

                            using (SQLiteDataReader reader = sqlSelectCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    AggregateContainer container = new AggregateContainer();

                                    long componentSensorId = Convert.ToInt64(reader["ComponentSensorId"]);
                                    long sensorType = Convert.ToInt64(reader["SensorTypeId"]);
                                    container.SensorType = (SensorType)sensorType;
                                    container.Name = Convert.ToString(reader["Name"]);
                                    container.Type = Convert.ToString(reader["Type"]);
                                    container.CurrentDay = Convert.ToDateTime(reader["Date"]);
                                    container.Count = Convert.ToInt64(reader["Count"]);
                                    container.Sum = Convert.ToDouble(reader["Sum"]);
                                    container.SumOfSquares = Convert.ToDouble(reader["SumOfSquares"]);
                                    container.Min = Convert.ToDouble(reader["Min"]);
                                    container.Max = Convert.ToDouble(reader["Max"]);

                                    SensorReading sensorReading;
                                    if (!readings.TryGetValue(componentSensorId, out sensorReading))
                                    {
                                        sensorReading = new SensorReading();
                                        readings[componentSensorId] = sensorReading;
                                    }
                                    
                                    sensorReading.Count += container.Count;
                                    sensorReading.Sum += container.Sum;
                                    sensorReading.SumOfSquares += container.SumOfSquares;                                        
                                    
                                    if (container.CurrentDay > lastWatermark)
                                    {
                                        dataToSendToServer.Add(container);
                                    }
                                }                                
                            }
                        }

                        Dictionary<long, Threshold> thresholds = new Dictionary<long, Threshold>();

                        foreach (long key in readings.Keys)
                        {
                            SensorReading reading = readings[key];
                            double average = (double)reading.Sum / reading.Count;
                            double stddev = Math.Sqrt((reading.SumOfSquares / reading.Count) - (average * average));
                            Threshold threshold = new Threshold();
                            threshold.LowerThreshold = ThresholdMultiplier*(average - stddev);
                            threshold.UpperThreshold = ThresholdMultiplier*(average + stddev);
                            thresholds[key] = threshold;
                        }

                        // Atomically replace
                        Thresholds = thresholds;

                        bool sendToServerSuccess = s_httpClient.SendToServer(dataToSendToServer);

                        if (sendToServerSuccess)
                        {
                            List<DataManager.ComponentSensorTypesContainer> componentSensorList = GetComponentSensorList();
                            AggregatedData = s_httpClient.GetAggregatedStats(componentSensorList);
                            if (lastWatermark > DateTime.MinValue)
                            {
                                const string c_insertNewWatermark = "UPDATE Watermarks SET Date = @date WHERE WatermarkId = @watermarkId";
                                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                                {
                                    command.CommandText = c_insertNewWatermark;
                                    command.Parameters.Add(new SQLiteParameter("@watermarkId", "ServerWatermark"));
                                    command.Parameters.Add(new SQLiteParameter("@date", dayFloorTime.AddDays(-1)));
                                    command.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                const string c_insertNewWatermark = "INSERT INTO Watermarks (WatermarkId,Date) values (@watermarkId,@date)";
                                using (SQLiteCommand command = new SQLiteCommand(s_dataManager._sqliteConnection))
                                {
                                    command.CommandText = c_insertNewWatermark;
                                    command.Parameters.Add(new SQLiteParameter("@watermarkId", "ServerWatermark"));
                                    command.Parameters.Add(new SQLiteParameter("@date", dayFloorTime.AddDays(-1)));
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        const string c_deleteSensorData = "DELETE FROM HistoricalAggregation WHERE Date < @date AND DateRange = @dateRange";
                        using (SQLiteCommand sqlDeleteCommand = new SQLiteCommand(s_dataManager._sqliteConnection))
                        {
                            sqlDeleteCommand.CommandText = c_deleteSensorData;

                            // Always make sure we have one full day of hour resolution
                            sqlDeleteCommand.Parameters.Add(new SQLiteParameter("@date", dayFloorTime.AddDays(-1)));
                            sqlDeleteCommand.Parameters.Add(new SQLiteParameter("@dateRange", DateRangeType.hour));

                            sqlDeleteCommand.ExecuteNonQuery();
                        }

                        s_lastDayAggregation = dayFloorTime;


                    }
                }
            }
        }


        private class SensorReading
        {
            public double Sum { get; set; }
            public double SumOfSquares { get; set; }
            public long Count { get; set; }
            public DateTime SampleTime { get; set; }
        }

        private class SensorReadingContainer
        {
            public SensorReadingContainer()
            {
                this.Readings = new LinkedList<SensorReading>();
                this.WarnUntilTime = DateTime.MinValue;
            }

            public LinkedList<SensorReading> Readings { get; private set;}
            public int CountOutsideRange { get; set; }
            public DateTime WarnUntilTime { get; set; }
            public double Sum { get; set; }
            public bool EmailSent { get; set; }
        }

        private class Threshold
        {
            public double UpperThreshold { get; set; }
            public double LowerThreshold { get; set; }
        }

        private static Dictionary<long, Threshold> Thresholds = new Dictionary<long,Threshold>();
        private static Dictionary<long, SensorReadingContainer> LastSamples = new Dictionary<long, SensorReadingContainer>();
        private static HashSet<long> FlaggedWarnings = new HashSet<long>();
        #endregion

        #region Transactions

        public static void BeginTransaction()
        {
            Monitor.Enter(s_lockObject);
            if (!s_transactionStarted)
            {
                SQLiteCommand command = new SQLiteCommand("begin", s_dataManager._sqliteConnection);
                command.ExecuteNonQuery();
                s_transactionStarted = true;
            }
        }

        public static void EndTransaction()
        {
            if (s_transactionStarted)
            {
                SQLiteCommand command = new SQLiteCommand("end", s_dataManager._sqliteConnection);
                command.ExecuteNonQuery();
                s_transactionStarted = false;
            }
            Monitor.Exit(s_lockObject);
        }

        #endregion

    }
}
