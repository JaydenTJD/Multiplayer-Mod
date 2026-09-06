using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Multiplayer.Core), "Multiplayer", "1.0.0", "Jayden", null)]
[assembly: MelonGame("Notional Games", "Beltmatic")]

namespace Multiplayer
{
    public class Core : MelonMod
    {
        public NetworkManager network;
        private Rect window = new(10, 10, 150, 110);
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
            network = new NetworkManager();
        }

        public override void OnUpdate()
        {
            network?.Update();
        }

        public override void OnGUI()
        {
            window = GUI.Window(45763, window, (GUI.WindowFunction)DrawWindow, "Levelup Window");
        }

        private void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, 150, 20));
            if (Button(new Rect(0, 20, 150, 30), "Host"))
            {
                network?.StartServer(9050);
            }
            if (Button(new Rect(0, 50, 150, 30), "Join"))
            {
                network?.StartClient("100.98.91.61", 9050, "Super Secret Password");
            }
            if (Button(new Rect(0, 80, 150, 30), "Send"))
            {
                network?.SendString("Test Message 1");
            }
        }

        public override void OnDeinitializeMelon()
        {
            network?.Stop();
        }

        private static bool Button(Rect rect, string label)
        {
            bool clicked = false;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (rect.Contains(Event.current.mousePosition))
                {
                    clicked = true;
                    Event.current.Use();
                }
            }
            GUI.Button(rect, label);
            return clicked;
        }
    }

    public class NetworkManager : INetEventListener
    {
        private NetManager _netManager;
        private NetPacketProcessor _packetProcessor;
        private List<NetPeer> _cachedPeerList = new List<NetPeer>();

        public void StartServer(int port)
        {
            _netManager = new NetManager(this) { AutoRecycle = true };
            _netManager.Start(port);
            Melon<Core>.Logger.Msg($"Server started on port: {port}");
        }

        public void StartClient(string ip, int port, string connectionKey)
        {
            _netManager = new NetManager(this) { AutoRecycle = true };
            _netManager.Start();
            _netManager.Connect(ip, port, connectionKey);
        }

        public void Update()
        {
            _netManager?.PollEvents();
        }

        public void SendString(string message)
        {
            if (_netManager != null) return;

            NetDataWriter writer = new NetDataWriter();
            writer.Put(message);
            _cachedPeerList.Clear();
            _netManager.GetConnectedPeers(_cachedPeerList);
            foreach (NetPeer peer in _cachedPeerList)
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        public void Stop()
        {
            _netManager?.Stop();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Melon<Core>.Logger.Msg($"Connected: {peer.Address}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info) 
        {
            Melon<Core>.Logger.Msg($"Disconnected: {peer.Address}. Reason: {info.Reason}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            string message = reader.GetString();
            Melon<Core>.Logger.Msg($"Received from {peer.Address}: {message}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Melon<Core>.Logger.Error($"Network Error from {endPoint}: {socketError}");
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnConnectionRequest(ConnectionRequest request) => request.AcceptIfKey("Super Secret Password");
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    }
}