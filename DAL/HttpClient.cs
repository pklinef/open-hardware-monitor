using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace OpenHardwareMonitor.DAL
{
    public class HttpClient
    {
        internal bool SendToServer(List<DataManager.AggregateContainer> dataToSendToServer)
        {
            try
            {
                string json = JsonConvert.SerializeObject(dataToSendToServer, Formatting.Indented);

                string RemoteUrl = HttpClient.ServerURL;

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
                    var res = reader.ReadToEnd();
                    if (res == "OK")
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
    }
}
