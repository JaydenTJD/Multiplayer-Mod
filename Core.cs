using System.Net;
using System.Net.Sockets;
using Il2Cpp;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(Multiplayer.Core), "Multiplayer", "1.0.0", "Jayden", null)]
[assembly: MelonGame("Notional Games", "Beltmatic")]

namespace Multiplayer
{
    public class Core : MelonMod
    {
        public NetworkManager network;
        private Rect window = new(10, 10, 150, 50);
        private bool visible = true;
        private GameManager gameManager;
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
            network = new NetworkManager();
        }

        public override void OnUpdate()
        {
            network?.Update();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        public override void OnGUI()
        {
            if (visible)
            {
                window = GUI.Window(36354, window, (GUI.WindowFunction)DrawWindow, "Multiplayer Window");
            }
        }

        private void PopulateGameManager()
        {
            GameObject dummy = new();
            UnityEngine.Object.DontDestroyOnLoad(dummy);
            Scene ddolScene = dummy.scene;
            UnityEngine.Object.Destroy(dummy);
            GameObject gameManagerGO = null;
            var rootObjects = ddolScene.GetRootGameObjects();
            foreach (var GO in rootObjects)
            {
                if (GO != null && GO.name == "GameManager")
                {
                    gameManagerGO = GO;
                    break;
                }
            }
            gameManager = gameManagerGO.GetComponent<GameManager>();
        }

        private void DrawWindow(int id)
        {
            if (gameManager == null)
            {
                PopulateGameManager();
            }

            GUI.DragWindow(new Rect(0, 0, 150, 20));

            if (network.isActiveSession)
            {
                if (Button(new Rect(0, 20, 150, 30), "Send"))
                {
                    network?.SendString("Test Message 1");
                }
            }
            else
            {
                if (gameManager.demoSimulation == null)
                {
                    if (Button(new Rect(0, 20, 150, 30), "Host"))
                    {
                        network?.StartServer(9050);
                    }
                }
                else
                {
                    if (Button(new Rect(0, 20, 150, 30), "Join"))
                    {
                        network?.StartClient("100.98.91.61", 9050, "Super Secret Password");
                    }
                }
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
        private List<NetPeer> _cachedPeerList = [];
        public bool isActiveSession = false;

        public void StartServer(int port)
        {
            _netManager = new NetManager(this) { AutoRecycle = true };
            _netManager.Start(port);
            Melon<Core>.Logger.Msg($"Server started on port: {port}");
            isActiveSession = true;
        }

        public void StartClient(string ip, int port, string connectionKey)
        {
            _netManager = new NetManager(this) { AutoRecycle = true };
            _netManager.Start();
            _netManager.Connect(ip, port, connectionKey);
            isActiveSession = true;
        }

        public void Update()
        {
            _netManager?.PollEvents();
        }

        public void SendString(string message)
        {
            if (_netManager == null) return;
            NetDataWriter writer = new();
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
            isActiveSession = false;
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