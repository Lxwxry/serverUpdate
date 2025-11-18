using System;
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

        public MainWindow()
        {
            InitializeComponent();
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
            }
            catch
            {
                ChatBox.AppendText("Failed to connect\n");
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
                    Dispatcher.Invoke(() => ChatBox.AppendText(msg + "\n"));
                }
            }
            catch {}
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (InputBox.Text.Trim() == "") return;

            string msg = InputBox.Text;
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
            ChatBox.AppendText("Me: " + msg + "\n");
            InputBox.Clear();
        }
    }
}