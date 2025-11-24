using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    static List<TcpClient> clients = new List<TcpClient>();
    static object locker = new object();

    // UDP port for notifications
    static int udpPort = 6000;
    static UdpClient udpServer;

    static void Main()
    {
        udpServer = new UdpClient(udpPort);
        StartUdpListener();

        TcpListener listener = new TcpListener(IPAddress.Any, 5000);
        listener.Start();
        Console.WriteLine("TCP server started on port 5000. UDP notifications on port 6000.");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            lock (locker) clients.Add(client);

            Console.WriteLine("New TCP client connected: " + client.Client.RemoteEndPoint);
            Thread t = new Thread(HandleClient);
            t.Start(client);
        }
    }

    static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes == 0) break;

                string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                Console.WriteLine("Received TCP: " + msg);
                Broadcast(msg, client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Client error: " + ex.Message);
        }

        lock (locker) clients.Remove(client);
        Console.WriteLine("TCP client disconnected: " + client.Client.RemoteEndPoint);
        client.Close();
    }

    static void Broadcast(string msg, TcpClient sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);

        lock (locker)
        {
            foreach (var c in clients)
            {
                if (c == sender) continue;
                try { c.GetStream().Write(data, 0, data.Length); }
                catch { }
            }
        }
    }

    // UDP listener reacts to JOIN|name and LEAVE|name and broadcasts "{name} ONLINE"/"{name} OFFLINE" to all known clients' IPs
    static void StartUdpListener()
    {
        Thread t = new Thread(() =>
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            Console.WriteLine("UDP listener started on port " + udpPort);
            while (true)
            {
                try
                {
                    byte[] data = udpServer.Receive(ref remote);
                    string msg = Encoding.UTF8.GetString(data);
                    Console.WriteLine("UDP received from " + remote.Address + ": " + msg);

                    if (msg.StartsWith("JOIN|"))
                    {
                        string name = msg.Substring(5);
                        BroadcastUdp($"{name} ONLINE");
                    }
                    else if (msg.StartsWith("LEAVE|"))
                    {
                        string name = msg.Substring(6);
                        BroadcastUdp($"{name} OFFLINE");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("UDP listener error: " + ex.Message);
                }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    static void BroadcastUdp(string msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(msg);

        lock (locker)
        {
            // send UDP notification to each client's IP on udpPort
            foreach (var c in clients)
            {
                try
                {
                    if (c.Client.RemoteEndPoint is IPEndPoint ep)
                    {
                        udpServer.Send(data, data.Length, ep.Address.ToString(), udpPort);
                    }
                }
                catch { }
            }
        }
        Console.WriteLine("UDP broadcast: " + msg);
    }
}
