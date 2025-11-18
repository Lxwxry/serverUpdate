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

    static void Main()
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 5000);
        listener.Start();
        Console.WriteLine("Server started...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            lock (locker) clients.Add(client);

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
                Broadcast(msg, client);
            }
        }
        catch {}

        lock (locker) clients.Remove(client);
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
                catch {}
            }
        }
    }
}