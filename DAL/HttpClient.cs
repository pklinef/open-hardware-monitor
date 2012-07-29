using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace OpenHardwareMonitor.DAL
{
    class HttpClient
    {
        internal bool SendToServer(List<DataManager.AggregateContainer> dataToSendToServer)
        {
            string json = JsonConvert.SerializeObject(dataToSendToServer, Formatting.Indented);

            const string RemoteUrl = "http://192.168.1.116:8090/aggregator";

            var httpReq = (HttpWebRequest)WebRequest.Create(RemoteUrl);
            httpReq.Method = "POST";
            httpReq.ContentType = httpReq.Accept = "application/json";

            using (var stream = httpReq.GetRequestStream())
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(json);
            }

            using (var response = httpReq.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                reader.ReadToEnd();
            }
            return false;
        }
    }
}
