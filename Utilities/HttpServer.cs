/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
	Copyright (C) 2012 Prince Samuel <prince.samuel@gmail.com>

*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using OpenHardwareMonitor.DAL;
using OpenHardwareMonitor.GUI;
using OpenHardwareMonitor.Hardware;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using Lidgren.Network;
using System.Diagnostics;
using System.Timers;
using System.Collections.Specialized;

namespace OpenHardwareMonitor.Utilities
{
    public class HttpServer
    {
        private HttpListener listener;
        private int listenerHttpPort, nodeCount;
        private Thread listenerThread;
        private Node root;
        private static NetPeer peer;
        private static System.Timers.Timer peerTimer;
        private static Hashtable peers = new Hashtable();

        public HttpServer(Node r, int p)
        {
            root = r;
            listenerHttpPort = p;
            //JSON node count. 
            nodeCount = 0;
            listener = new HttpListener();

            NetPeerConfiguration config = new NetPeerConfiguration("OpenHardwareMonitor");
            config.Port = p + 1;
            config.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            peer = new NetPeer(config);
            peer.RegisterReceivedCallback(new SendOrPostCallback(HandlePeerMessages));

            peerTimer = new System.Timers.Timer();
            peerTimer.Elapsed += new ElapsedEventHandler(TimerElapsed);
            peerTimer.Interval = 1000 * 60 * 1;
        }

        public Boolean startHTTPListener()
        {
            try
            {
                if (listener.IsListening)
                    return true;

                string prefix = "http://+:" + listenerHttpPort + "/";
                AddPortToFirewall(listenerHttpPort);
                listener.Prefixes.Clear();
                listener.Prefixes.Add(prefix);
                listener.Start();

                if (listenerThread == null)
                {
                    listenerThread = new Thread(HandleRequests);
                    listenerThread.Start();
                }
            }
            catch (Exception)
            {
                return false;
            }

            peer.Start();
            peer.DiscoverLocalPeers(peer.Port);
            peerTimer.Enabled = true;

            return true;
        }

        public static void AddPortToFirewall(int port)
        {
            // HACK to add port exception to Windows Firewall since HTTPListener uses HTTP.sys
            // for Windows Vista and up only

            // remove current OpenHardwareMonitor port in case it has changed
            string deleteRuleArgs = @"advfirewall firewall delete rule name=OpenHardwareMonitor";
            RunNetsh(deleteRuleArgs);

            // add current OpenHardwareMonitor port
            string createRuleArgs = string.Format(@"advfirewall firewall add rule name=OpenHardwareMonitor dir=in action=allow protocol=TCP localport={0}", port);
            RunNetsh(createRuleArgs);
        }

        public static void RunNetsh(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo("netsh", args);
            psi.Verb = "runas";
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = false;

            Process.Start(psi).WaitForExit();
        }

        public Boolean stopHTTPListener()
        {
            try
            {
                listenerThread.Abort();
                listener.Stop();
                listenerThread = null;
            }
            catch (System.Net.HttpListenerException)
            {
            }
            catch (System.Threading.ThreadAbortException)
            {
            }
            catch (System.NullReferenceException)
            {
            }
            catch (Exception)
            {
            }

            peerTimer.Enabled = false;
            peer.Shutdown("bye");

            return true;
        }

        public static void HandlePeerMessages(object peer)
        {
            if (peer is NetPeer)
            {
                NetPeer p = ((NetPeer)peer);
                NetIncomingMessage msg = ((NetPeer)peer).ReadMessage();
                string machineName;
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.DiscoveryRequest:
                        if (!peers.ContainsKey(msg.SenderEndpoint))
                        {
                            Console.WriteLine("DiscoveryRequest from " + msg.SenderEndpoint.Address + " port: " + msg.SenderEndpoint.Port);

                            NetOutgoingMessage requestResponse = p.CreateMessage();
                            requestResponse.Write(Environment.MachineName);

                            p.SendDiscoveryResponse(requestResponse, msg.SenderEndpoint);
                        }
                        break;
                    case NetIncomingMessageType.DiscoveryResponse:
                        machineName = msg.ReadString();

                        Boolean connectedToPeer = false;
                        foreach (NetConnection conn in p.Connections)
                        {
                            if (conn.RemoteEndpoint == msg.SenderEndpoint)
                            {
                                connectedToPeer = true;
                            }
                        }

                        if (!connectedToPeer)
                        {
                            if (!peers.ContainsKey(msg.SenderEndpoint))
                            {
                                Console.WriteLine("DiscoveryResponse from " + msg.SenderEndpoint.Address + " port: " + msg.SenderEndpoint.Port +
                                    " machine name: " + machineName);
                                peers.Add(msg.SenderEndpoint, machineName);
                            }

                            NetOutgoingMessage hailMessage = p.CreateMessage();
                            hailMessage.Write(Environment.MachineName);
                            NetConnection senderConn = p.Connect(msg.SenderEndpoint, hailMessage);
                        }

                        break;
                }
                p.Recycle(msg);
            }
        }

        static void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            peer.DiscoverLocalPeers(peer.Port);
        }

        public void HandleRequests()
        {
            while (listener.IsListening)
            {
                var context = listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
                context.AsyncWaitHandle.WaitOne();
            }
        }

        public void ListenerCallback(IAsyncResult result)
        {
            var lastAccessTime = DataManager.GetLastAccessTime();
            DataManager.UpdateLastAccessTime();

            HttpListener listener = (HttpListener)result.AsyncState;
            if (listener == null || !listener.IsListening)
                return;
            // Call EndGetContext to complete the asynchronous operation.
            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;

            var requestedFile = request.Url.AbsolutePath.Substring(1);

            try
            {
                if (request.QueryString.Count > 0)
                {
                    List<string> keys = new List<string>(request.QueryString.AllKeys);
                    if (keys.Contains("peer"))
                    {
                        proxyRequest(context, request.QueryString["peer"]);
                        return;
                    }
                }

                if (requestedFile == "data.json")
                {
                    sendJSON(context);
                    return;
                }

                if (requestedFile.Contains("sensors"))
                {
                    if (requestedFile.Contains("-"))
                    {
                        SendSensorDataJSON(context);
                        return;
                    }
                    SendTreeJSON(context);
                    return;
                }

                if (requestedFile == "peers.json")
                {
                    SendPeersJSON(context);
                    return;
                }

                if (requestedFile == "lat.json")
                {
                    SendLATJSON(context, lastAccessTime);
                    return;
                }

                if (requestedFile.Contains("sensor.csv"))
                {
                    sendSensorCSV(context);
                    return;
                }

                if (requestedFile.Contains("images_icon"))
                {
                    serveResourceImage(context, requestedFile.Replace("images_icon/", ""));
                    return;
                }

                //default file to be served
                if (string.IsNullOrEmpty(requestedFile))
                    requestedFile = "index.html";

                string[] splits = requestedFile.Split('.');
                string ext = splits[splits.Length - 1];
                serveResourceFile(context, "Web." + requestedFile.Replace('/', '.'), ext);
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void proxyRequest(HttpListenerContext context, String peer)
        {            
            String address = String.Format("http://{0}{1}",peer,context.Request.Url.AbsolutePath);

            try
            {
                WebClient client = new WebClient();
                NameValueCollection proxyQueryString = context.Request.QueryString;
                proxyQueryString.Remove("peer");
                if (proxyQueryString.Count > 0)
                {
                    client.QueryString = proxyQueryString;
                }
                byte[] data = client.DownloadData(address);

                Stream outputStream = context.Response.OutputStream;
                outputStream.Write(data, 0, data.Length);
                outputStream.Close();
                return;
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.ToString());
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            context.Response.OutputStream.Close();
            context.Response.StatusCode = 404;
            context.Response.Close();
        }

        private void serveResourceFile(HttpListenerContext context ,string name, string ext) {

          //hack! resource names do not support the hyphen
          name = "OpenHardwareMonitor.Resources." + name.Replace("custom-theme", "custom_theme");
    
          string[] names = 
            Assembly.GetExecutingAssembly().GetManifestResourceNames();
          for (int i = 0; i < names.Length; i++) {
            if (names[i].Replace('\\', '.') == name) {
              using (Stream stream = Assembly.GetExecutingAssembly().
                GetManifestResourceStream(names[i])) {
                    context.Response.ContentType = getcontentType("." + ext);
                    context.Response.ContentLength64 = stream.Length;
                    byte[] buffer = new byte[512 * 1024];
                    int len;
                    while ((len = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        context.Response.OutputStream.Write(buffer, 0, len);
                    }
                    context.Response.OutputStream.Close();
                }
              return;
            }
          }
          context.Response.OutputStream.Close();
          context.Response.StatusCode = 404;
          context.Response.Close();
        }

        private void serveResourceImage(HttpListenerContext context ,string name) {
          name = "OpenHardwareMonitor.Resources." + name;
    
          string[] names = 
            Assembly.GetExecutingAssembly().GetManifestResourceNames();
          for (int i = 0; i < names.Length; i++) {
            if (names[i].Replace('\\', '.') == name) {
              using (Stream stream = Assembly.GetExecutingAssembly().
                GetManifestResourceStream(names[i])) {
    
                Image image = Image.FromStream(stream);
                context.Response.ContentType = "image/png";
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Png);
                    ms.WriteTo(context.Response.OutputStream);
                }
                context.Response.OutputStream.Close();
                image.Dispose();
                return;
              }
            }
          } 
          context.Response.OutputStream.Close();
          context.Response.StatusCode = 404;
          context.Response.Close();
        }

        private void sendSensorCSV(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;

            DateTime start = parseDate(request.QueryString["start"], DateTime.UtcNow.AddMinutes(-10));
            DateTime end = parseDate(request.QueryString["end"], DateTime.UtcNow);

            string req = request.RawUrl;
            string sensorPath = req.Substring(0, req.IndexOf("/sensor.csv"));
            long componentSensorId = DataManager.GetComponentSensorId(sensorPath);

            if (componentSensorId != -1)
            {
                double avg;
                double min;
                double max;
                double stddev;
                List<DataManagerData> values = DataManager.GetDataForSensor(componentSensorId, start, end - start, DataManager.DateRangeType.day, DataManager.DateRangeType.second, out avg, out min, out max, out stddev);

                StringBuilder csv = new StringBuilder();
                csv.AppendLine("Date,Value");
                foreach (DataManagerData data in values)
                {
                    //http://dygraphs.com/date-formats.html
                    csv.AppendLine(data.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss") + "," + data.Measure);
                }

                var responseContent = csv.ToString();
                byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "text/csv";

                Stream outputStream = context.Response.OutputStream;
                outputStream.Write(buffer, 0, buffer.Length);
                outputStream.Close();
                return;
            }

            context.Response.OutputStream.Close();
            context.Response.StatusCode = 404;
            context.Response.Close();
        }

        private DateTime parseDate(String dateText, DateTime defaultDate)
        {
            DateTime result = defaultDate;
            if (dateText != null)
            {
                if (dateText.Length == 19)
                {
                    result = DateTime.ParseExact(dateText, "yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture).ToUniversalTime();

                }
                else if (dateText.Length == 10)
                {
                    result = DateTime.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.CurrentCulture).ToUniversalTime();
                }
            }
            return result;
        }

        private void sendJSON(HttpListenerContext context)
        {

            string JSON = "{\"id\": 0, \"Text\": \"Sensor\", \"Children\": [";
            nodeCount = 1;
            JSON += generateJSON(root);
            JSON += "]";
            JSON += ", \"Min\": \"Min\"";
            JSON += ", \"Value\": \"Value\"";
            JSON += ", \"Max\": \"Max\"";
            JSON += ", \"ImageURL\": \"\"";
            JSON += "}";

            var responseContent = JSON;
            byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "application/json";

            Stream outputStream = context.Response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            outputStream.Close();

        }

        private void SendPeersJSON(HttpListenerContext context)
        {
            string JSON = "[";
            foreach (NetConnection conn in peer.Connections)
            {
                JSON += "{";
                JSON += "\"name\":\"" + peers[conn.RemoteEndpoint] + "\", ";
                JSON += "\"address\":\"" + conn.RemoteEndpoint.Address + ":" + (conn.RemoteEndpoint.Port - 1) + "\"";
                JSON += "}, ";
            }
            if (JSON.EndsWith(", "))
            {
                JSON = JSON.Remove(JSON.LastIndexOf(","));
            }
            JSON += "]";

            var responseContent = JSON;
            byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "application/json";

            Stream outputStream = context.Response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            outputStream.Close();
        }

        private string generateJSON(Node n)
        {
            string JSON = "{\"id\": " + nodeCount + ", \"Text\": \"" + n.Text + "\", \"Children\": [";
            nodeCount++;

            foreach (Node child in n.Nodes)
                JSON += generateJSON(child) + ", ";
            if (JSON.EndsWith(", "))
                JSON = JSON.Remove(JSON.LastIndexOf(","));
            JSON += "]";

            if (n is SensorNode)
            {
                JSON += ", \"Min\": \"" + ((SensorNode)n).Min + "\"";
                JSON += ", \"Value\": \"" + ((SensorNode)n).Value + "\"";
                JSON += ", \"Max\": \"" + ((SensorNode)n).Max + "\"";
                JSON += ", \"ImageURL\": \"images/transparent.png\"";
            }
            else if (n is HardwareNode)
            {
                JSON += ", \"Min\": \"\"";
                JSON += ", \"Value\": \"\"";
                JSON += ", \"Max\": \"\"";
                JSON += ", \"ImageURL\": \"images_icon/" + getHardwareImageFile((HardwareNode)n) + "\"";
            }
            else if (n is TypeNode)
            {
                JSON += ", \"Min\": \"\"";
                JSON += ", \"Value\": \"\"";
                JSON += ", \"Max\": \"\"";
                JSON += ", \"ImageURL\": \"images_icon/" + getTypeImageFile((TypeNode)n) + "\"";
            }
            else
            {
                JSON += ", \"Min\": \"\"";
                JSON += ", \"Value\": \"\"";
                JSON += ", \"Max\": \"\"";
                JSON += ", \"ImageURL\": \"images_icon/computer.png\"";
            }

            JSON += "}";
            return JSON;
        }

        private void SendLATJSON(HttpListenerContext context, DateTime lastAccessTime)
        {

            string JSON = "{\"lastAccessTime\": \"" + lastAccessTime + "\"}";
            var responseContent = JSON;
            byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "application/json";

            Stream outputStream = context.Response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            outputStream.Close();

        }

        private void SendSensorDataJSON(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;

            DateTime start = parseDate(request.QueryString["start"], DateTime.UtcNow.AddMinutes(-10));
            DateTime end = parseDate(request.QueryString["end"], DateTime.UtcNow);

            string req = request.RawUrl;
            req = req.Remove(req.IndexOf('?'));
            string sensorId = req.Substring(9);// "sensors/" is 8 characters long
            string sensorPath = req.Substring(8).Replace('-', '/'); 
            long componentSensorId = DataManager.GetComponentSensorId(sensorPath);

            if (componentSensorId != -1)
            {
                double avg;
                double min;
                double max;
                double stddev;
                List<DataManagerData> values = DataManager.GetDataForSensor(componentSensorId, start, end - start, DataManager.DateRangeType.day, DataManager.DateRangeType.second, out avg, out min, out max, out stddev);

                var epoch = new DateTime (1970, 1, 1);
                string JSON = "{\"id\": \"" + sensorId + "\", \"data\": [";
                foreach (DataManagerData data in values)
                {
                    JSON += "[\"" + data.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss") + " GMT\", " + data.Measure + "],";
                }
                JSON = JSON.Remove(JSON.LastIndexOf(","));
                JSON += ("]}");

                var responseContent = JSON.ToString();
                byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "application/json";

                Stream outputStream = context.Response.OutputStream;
                outputStream.Write(buffer, 0, buffer.Length);
                outputStream.Close();
                return;
            }

            context.Response.OutputStream.Close();
            context.Response.StatusCode = 404;
            context.Response.Close();
        }

        private void SendTreeJSON(HttpListenerContext context)
        {

            string JSON = "[";
            nodeCount = 1;
            JSON += GenerateTreeJSON(root, 0);
            if (JSON.EndsWith(", "))
                JSON = JSON.Remove(JSON.LastIndexOf(","));
            JSON += "]";

            var responseContent = JSON;
            byte[] buffer = Encoding.UTF8.GetBytes(responseContent);

            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "application/json";

            Stream outputStream = context.Response.OutputStream;
            outputStream.Write(buffer, 0, buffer.Length);
            outputStream.Close();

        }

        private string GenerateTreeJSON(Node n, int parentID)
        {
            string JSON = "{\"nid\": " + nodeCount + ", \"text\": \"" + n.Text + "\", \"parent\": " + parentID;
            int currentId = nodeCount;
            nodeCount++;
            if (n is SensorNode)
            {
                //remove the first / and replace all others with -
                String sensorId = ((SensorNode)n).Sensor.Identifier.ToString().Substring(1).Replace("/", "-");
                JSON += ", \"id\": \"" + sensorId + "\"";
                JSON += ", \"type\": \"" + ((SensorNode)n).Sensor.SensorType.ToString() + "\"";
                JSON += ", \"imageURL\": \"images/transparent.png\"";
            }
            else if (n is HardwareNode)
            {
                JSON += ", \"id\": \"" + currentId + "\"";
                JSON += ", \"type\": \"\"";
                JSON += ", \"imageURL\": \"images_icon/" + getHardwareImageFile((HardwareNode)n) + "\"";
            }
            else if (n is TypeNode)
            {
                JSON += ", \"id\": \"" + currentId + "\"";
                JSON += ", \"type\": \"\"";
                JSON += ", \"imageURL\": \"images_icon/" + getTypeImageFile((TypeNode)n) + "\"";
            }
            else
            {
                JSON += ", \"id\": \"" + currentId + "\"";
                JSON += ", \"type\": \"\"";
                JSON += ", \"imageURL\": \"images_icon/computer.png\"";
            }

            JSON += "}, ";

            foreach (Node child in n.Nodes)
                JSON += GenerateTreeJSON(child, currentId);
            return JSON;
        }


        private static void returnFile(HttpListenerContext context, string filePath)
        {
            context.Response.ContentType = getcontentType(Path.GetExtension(filePath));
            const int bufferSize = 1024 * 512; //512KB
            var buffer = new byte[bufferSize];
            using (var fs = File.OpenRead(filePath))
            {
                
                context.Response.ContentLength64 = fs.Length;
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    context.Response.OutputStream.Write(buffer, 0, read);
            }

            context.Response.OutputStream.Close();
        }

        private static string getcontentType(string extension)
        {
            switch (extension)
            {
                case ".avi": return "video/x-msvideo";
                case ".css": return "text/css";
                case ".doc": return "application/msword";
                case ".gif": return "image/gif";
                case ".htm":
                case ".html": return "text/html";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".js": return "application/x-javascript";
                case ".mp3": return "audio/mpeg";
                case ".png": return "image/png";
                case ".pdf": return "application/pdf";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".zip": return "application/zip";
                case ".txt": return "text/plain";
                default: return "application/octet-stream";
            }
        }

        private static string getHardwareImageFile(HardwareNode hn)
        {

            switch (hn.Hardware.HardwareType)
            {
                case HardwareType.CPU:
                    return "cpu.png";
                case HardwareType.GpuNvidia:
                    return "nvidia.png";
                case HardwareType.GpuAti:
                    return "ati.png";
                case HardwareType.HDD:
                    return "hdd.png";
                case HardwareType.Heatmaster:
                    return "bigng.png";
                case HardwareType.Mainboard:
                    return "mainboard.png";
                case HardwareType.SuperIO:
                    return "chip.png";
                case HardwareType.TBalancer:
                    return "bigng.png";
                default:
                    return "cpu.png";
            }

        }

        private static string getTypeImageFile(TypeNode tn)
        {

            switch (tn.SensorType)
            {
                case SensorType.Voltage:
                    return "voltage.png";
                case SensorType.Clock:
                    return "clock.png";
                case SensorType.Load:
                    return "load.png";
                case SensorType.Temperature:
                    return "temperature.png";
                case SensorType.Fan:
                    return "fan.png";
                case SensorType.Flow:
                    return "flow.png";
                case SensorType.Control:
                    return "control.png";
                case SensorType.Level:
                    return "level.png";
                case SensorType.Power:
                    return "power.png";
                default:
                    return "power.png";
            }

        }

        public int ListenerPort
        {
            get { return listenerHttpPort; }
            set { listenerHttpPort = value; }
        }

        ~HttpServer()
        {
            stopHTTPListener();
            listener.Abort();
        }

        public void Quit()
        {
            stopHTTPListener();
            listener.Abort();
        }
    }
}
