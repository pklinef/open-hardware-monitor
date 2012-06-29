using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.Windows.Forms;
using System.IO;
using System.Data;

namespace OpenHardwareMonitor.DAL
{
    public class DataManager
    {
        private string _dbFile;
        private SQLiteConnection _sqliteConnection;
        public DataManager()
        {
            _dbFile = "sqlite.s3db";
            string currentPath;
            string absolutePath;
            string connectionString;

            currentPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath); 
            absolutePath = System.IO.Path.Combine(currentPath, _dbFile);

            connectionString = string.Format(@"Data Source={0}", absolutePath);

            _sqliteConnection = new SQLiteConnection(connectionString);
        }
    }
}
