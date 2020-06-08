using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace MultiServer
{
    class Program
    {
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // connection with protocol TCP
        private static readonly List<Socket> clientSockets = new List<Socket>(); // clients list
        private const int BUFFER_SIZE = 50000;
        private const int PORT = 8080; 
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        private static int online = 0;


        static void Main()
        {
            Console.Title = "Server";
            SetupServer();
            Console.ReadLine(); // When we press enter close everything
            CloseAllSockets();
        }

        /// <summary>
        /// Starting socket server
        /// </summary>
        private static void SetupServer()
        {
            Console.WriteLine("Setting up server...");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(4);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete \n" +
                "Ipv4 of the host is: " + GetLocalIPAddress());
        }

        /// <summary>
        /// Close all connected client (we do not need to shutdown the server socket as its connections
        /// are already closed with the clients).
        /// </summary>
        /// 
        private static void CloseAllSockets()
        {
            foreach (Socket socket in clientSockets)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            serverSocket.Close();
        }

        /// <summary>
        /// sending a string to all connecting clients
        /// </summary>
        /// <param name="s"></param>
        private static void sentToAll(string s)
        {
            foreach (Socket socket in clientSockets)
            {
                byte[] data = Encoding.ASCII.GetBytes(s);
                socket.Send(data);
            }
        }

        /// <summary>
        /// accepting a connection of  socket.
        /// </summary>
        /// <param name="AR"></param>
        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }
            online++; // number of people connected
            clientSockets.Add(socket);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client number {0} connected, waiting for request...", online);
         //   sentToAll(ONLY_MESSAGE+"Client number " + online + " connected ");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        /// <summary>
        /// Reciving data from the client
        /// </summary>
        /// <param name="AR"></param>
        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                clientSockets.Remove(current);
                return;
            }


            byte[] recBuf = new byte[received]; //recive data
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf); //casting the bytes to a string
            Console.WriteLine("Received Text: " + text);

            if (text.ToLower().Contains("get time")) // Client requested time
            {
                Console.WriteLine("Text is a get time request");
                byte[] data = Encoding.ASCII.GetBytes(DateTime.Now.ToLongTimeString());
                sentToAll(data.ToString());
                Console.WriteLine("Time sent to client is: " + DateTime.Now.ToLongTimeString());
            }
            else if (text.ToLower()=="exit") // Client wants to exit gracefully
            {
                // Always Shutdown before closing
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                clientSockets.Remove(current);
                Console.WriteLine("Client disconnected");
                return;
            }
            else
            {
                sentToAll(text);
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        /// <summary>
        ///  returns the IPv4 of the computer host. 
        /// </summary>
        /// <returns></returns>
        public static string GetLocalIPAddress() // this function returns the local IPv4 address of the host.
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}