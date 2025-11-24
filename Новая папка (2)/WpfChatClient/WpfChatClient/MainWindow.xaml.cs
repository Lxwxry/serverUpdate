using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace WpfChatClient
{
    public partial class MainWindow : Window
    {
        TcpClient client;
        NetworkStream stream;

        UdpClient udp;
        int udpPort = 6001;
        string userName;
        HashSet<string> onlineUsers = new HashSet<string>();

        public MainWindow()
        {
            InitializeComponent();
            userName = "User" + new Random().Next(1000, 9999);
            NameBox.Text = userName;
            ConnectToServer();
        }

        void ConnectToServer()
        {
            try
            {
                client = new TcpClient();
                client.Connect("127.0.0.1", 5000);
                stream = client.GetStream();

                Thread t = new Thread(ReceiveMessages);
                t.IsBackground = true;
                t.Start();

                // UDP init
                udp = new UdpClient();
                SendJoin();
                StartUdpListener();
                AddMessage("Connected to server. Your name: " + userName);
            }
            catch (Exception ex)
            {
                AddMessage("Failed to connect: " + ex.Message);
            }
        }

        void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    if (bytes == 0) break;

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                    Dispatcher.Invoke(() => AddMessage(msg));
                }
            }
            catch
            {
                Dispatcher.Invoke(() => AddMessage("TCP connection lost."));
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (InputBox.Text.Trim() == "") return;

            string text = $"{NameBox.Text}: {InputBox.Text}";
            byte[] data = Encoding.UTF8.GetBytes(text);
            try
            {
                stream.Write(data, 0, data.Length);
                AddMessage("Me: " + InputBox.Text);
                InputBox.Clear();
            }
            catch
            {
                AddMessage("Failed to send message.");
            }
        }

        void AddMessage(string msg)
        {
            ChatBox.AppendText(msg + Environment.NewLine);
            ChatBox.ScrollToEnd();
        }

        // UDP: send JOIN and LEAVE messages to server
        void SendJoin()
        {
            try
            {
                userName = string.IsNullOrWhiteSpace(NameBox.Text) ? userName : NameBox.Text.Trim();
                byte[] data = Encoding.UTF8.GetBytes("JOIN|" + userName);
                udp.Send(data, data.Length, "127.0.0.1", udpPort);
            }
            catch { }
        }

        void SendLeave()
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes("LEAVE|" + userName);
                udp.Send(data, data.Length, "127.0.0.1", udpPort);
            }
            catch { }
        }

        void StartUdpListener()
        {
            Thread t = new Thread(() =>
            {
                UdpClient listener = new UdpClient(udpPort);
                IPEndPoint ep = null;
                try
                {
                    while (true)
                    {
                        byte[] data = listener.Receive(ref ep);
                        string msg = Encoding.UTF8.GetString(data);
                        Dispatcher.Invoke(() =>
                        {
                            AddMessage("[NOTIFY] " + msg);
                            UpdateOnlineCounter(msg);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => AddMessage("UDP listener stopped: " + ex.Message));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void UpdateOnlineCounter(string udpMsg)
        {
            // Expected format: "Name ONLINE" or "Name OFFLINE"
            var parts = udpMsg.Split(' ');
            if (parts.Length < 2) return;
            string name = parts[0];
            string status = parts[1];

            if (status == "ONLINE")
                onlineUsers.Add(name);
            else if (status == "OFFLINE")
                onlineUsers.Remove(name);

            OnlineCounter.Text = onlineUsers.Count.ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                SendLeave();
            }
            catch { }
            try
            {
                client?.Close();
                udp?.Close();
            }
            catch { }
            base.OnClosed(e);
        }
    }
}
