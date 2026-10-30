// Piccolo server HTTP per il controllo remoto (Bitfocus Companion / Stream Deck).
// Usa TcpListener invece di HttpListener: ascolta in rete senza permessi di amministratore.
//
//   GET /api/start               avvia
//   GET /api/pause               pausa
//   GET /api/toggle              avvia/pausa
//   GET /api/reset               reset
//   GET /api/add?min=5           aggiunge minuti (negativo per togliere)
//   GET /api/preset?name=Nome    carica un preset per nome
//   GET /api/message/show|hide|toggle
//   GET /api/status              stato in JSON
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LiveCount
{
    public class RemoteApi : IDisposable
    {
        // Riceve percorso e parametri, restituisce il JSON di risposta (o null se il comando non esiste).
        readonly Func<string, Dictionary<string, string>, string> _handler;
        TcpListener _listener;
        Thread _thread;
        volatile bool _stop;

        public int Port { get; private set; }
        public string Error { get; private set; }
        public bool Running { get { return _listener != null; } }

        public RemoteApi(Func<string, Dictionary<string, string>, string> handler)
        {
            _handler = handler;
        }

        public bool Start(int port)
        {
            Stop();
            Port = port;
            Error = null;
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
            }
            catch (Exception ex)
            {
                _listener = null;
                Error = "Porta " + port + " non disponibile: " + ex.Message;
                return false;
            }
            _stop = false;
            _thread = new Thread(Loop) { IsBackground = true, Name = "Remote API" };
            _thread.Start();
            return true;
        }

        public void Stop()
        {
            _stop = true;
            if (_listener != null) { try { _listener.Stop(); } catch { } _listener = null; }
            _thread = null;
        }

        void Loop()
        {
            var listener = _listener;
            while (!_stop && listener != null)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(delegate { Serve(client); });
            }
        }

        void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 3000;
                    client.SendTimeout = 3000;
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.ASCII);
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine)) return;
                    // Le intestazioni non servono: si leggono solo per svuotare la richiesta.
                    string line;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }

                    string[] parts = requestLine.Split(' ');
                    string target = parts.Length > 1 ? parts[1] : "/";
                    string path = target, query = "";
                    int q = target.IndexOf('?');
                    if (q >= 0) { path = target.Substring(0, q); query = target.Substring(q + 1); }
                    path = Uri.UnescapeDataString(path).TrimEnd('/').ToLowerInvariant();

                    var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in query.Split('&'))
                    {
                        if (pair.Length == 0) continue;
                        int eq = pair.IndexOf('=');
                        string k = eq >= 0 ? pair.Substring(0, eq) : pair;
                        string v = eq >= 0 ? pair.Substring(eq + 1) : "";
                        args[Unescape(k)] = Unescape(v);
                    }

                    string json = null;
                    if (path.StartsWith("/api")) json = _handler(path.Substring(4).TrimStart('/'), args);
                    if (json == null) Write(stream, 404, "{\"ok\":false,\"error\":\"comando sconosciuto\"}");
                    else Write(stream, 200, json);
                }
                catch { }
            }
        }

        static string Unescape(string s)
        {
            return Uri.UnescapeDataString(s.Replace('+', ' '));
        }

        static void Write(Stream stream, int status, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            string head = "HTTP/1.1 " + status + (status == 200 ? " OK" : " Not Found") + "\r\n" +
                          "Content-Type: application/json; charset=utf-8\r\n" +
                          "Access-Control-Allow-Origin: *\r\n" +
                          "Cache-Control: no-store\r\n" +
                          "Connection: close\r\n" +
                          "Content-Length: " + body.Length + "\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(head);
            stream.Write(h, 0, h.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        // Indirizzi IPv4 di rete locale di questo PC, per configurare Companion da un altro computer.
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (var a in ni.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
                            list.Add(a.Address.ToString());
                }
            }
            catch { }
            return list;
        }

        public static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public void Dispose() { Stop(); }
    }
}
