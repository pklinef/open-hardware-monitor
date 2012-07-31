using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Net.Sockets;

namespace OpenHardwareMonitor.DAL
{
    public class HttpClient
    {
        internal HashSet<String> GetLocalIPs()
        {
            IPHostEntry host;
            HashSet<String> localIPs = new HashSet<string>();
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIPs.Add(ip.ToString());
                }
            }
            return localIPs;
        }

        internal bool SendToServer(List<DataManager.AggregateContainer> dataToSendToServer)
        {
            try
            {
                if (HttpClient.ServerURL == null || HttpClient.ServerURL == "")
                    return false;

                var IP = ServerURL.Replace("http://", "").Replace("/", "");
                var port = IP.Split(':')[1];
                IP = IP.Split(':')[0];

                //short-circuiting if this machine is the server
                //note: not checking for port
                //assuming only one instance of OHM running on a machine
                HashSet<String> ips = GetLocalIPs();
                if (ips.Contains(IP))
                {
                    return DataManager.InsertData(dataToSendToServer);
                }

                string json = JsonConvert.SerializeObject(dataToSendToServer, Formatting.Indented);

                string RemoteUrl = HttpClient.ServerURL + "aggregator";

                var res  = SendRequest(json, RemoteUrl);
                if (res == "OK")
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return false;
        }

        public static string ServerURL { get; set; }

        internal List<DataManager.ComponentSensorTypesContainer> GetAggregatedStats(List<DataManager.ComponentSensorTypesContainer> componentSensorList)
        {
            if (HttpClient.ServerURL == null || HttpClient.ServerURL == "")
                return null;

            try
            {
                var IP = ServerURL.Replace("http://", "").Replace("/", "");
                var port = IP.Split(':')[1];
                IP = IP.Split(':')[0];

                //short-circuiting if this machine is the server
                //note: not checking for port
                //assuming only one instance of OHM running on a machine
                HashSet<String> ips = GetLocalIPs();
                if (ips.Contains(IP))
                {

                    foreach (var item in componentSensorList)
                    {
                        DataManager.GetData(item.Name, item.ComponentType, item.SensorName, out item.Avg, out item.Min, out item.Max, out item.StdDev);
                    }
                    return componentSensorList;
                }

                string json = JsonConvert.SerializeObject(componentSensorList, Formatting.Indented);
                string RemoteUrl = HttpClient.ServerURL + "aggregatedData";

                json = SendRequest(json, RemoteUrl);
                List<DataManager.ComponentSensorTypesContainer> data = JsonConvert.DeserializeObject<List<DataManager.ComponentSensorTypesContainer>>(json);

                return data;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static string SendRequest(string json, string RemoteUrl)
        {
            var httpReq = (HttpWebRequest)WebRequest.Create(RemoteUrl);
            httpReq.Method = "POST";
            httpReq.ContentType = httpReq.Accept = "application/json";

            using (var stream = httpReq.GetRequestStream())
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(json);
            }

            string res;

            using (var response = httpReq.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                res = reader.ReadToEnd();
            }
            return res;
        }
    }
}
