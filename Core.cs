using System.Net;
using System.Net.Sockets;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using HarmonyLib;
using Il2CppNcp.Game;
using Il2CppNcp;
using System.Reflection;
using UnityEngine.UI;
using Steamworks;

[assembly: MelonInfo(typeof(Multiplayer.Core), "Multiplayer", "1.0.0", "Jayden", null)]
[assembly: MelonGame("Notional Games", "Beltmatic")]

namespace Multiplayer
{
    public class Core : MelonMod
    {
        public NetworkManager network;
        private UnityEngine.Rect window = new(10, 10, 150, 80);
        private bool visible = true;
        private GameManager gameManager;
        private bool allowConnect = true;
        public GameObject mainMenu;
        private GameObject upgradeMenu;
        private GameObject deliveryMenu;
        private RectTransform ipPortInput;
        private RectTransform chatInput;
        private Transform canvasTransform;
        private string ipAddress;
        private int port;
        public string username;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
            foreach (MelonBase melon in MelonBase.RegisteredMelons)
            {
                if (melon.Info.Name != "SeedDisplay" && melon.Info.Name != "Multiplayer")
                {
                    allowConnect = false;
                    LoggerInstance.Msg($"Disallowed online access due to illegal mod: {melon.Info.Name} (v{melon.Info.Version}) being installed");
                }
            }

            SteamClient.Init(2674590);
            username = Steamworks.SteamClient.Name;
            SteamClient.Shutdown();
        }

        public void ToggleMainMenu()
        {
            if (mainMenu == null)
            {
                mainMenu = Resources.FindObjectsOfTypeAll<MainMenu>()[0].gameObject;
                for (int i = 0; i < mainMenu.transform.childCount; i++)
                {
                    GameObject child = mainMenu.transform.GetChild(i).gameObject;
                    if (child.name == "ResumeGameButton")
                    {
                        child.SetActive(true);
                        child.GetComponent<Button>().onClick.AddListener(new System.Action(() =>
                        {
                            ToggleMainMenu();
                        }));
                    } else if (child.name == "ContinueGameButton")
                    {
                        child.SetActive(false);
                    }
                }
            }
            mainMenu.SetActive(!mainMenu.activeSelf);
        }

        private void ToggleUpgradeMenu()
        {
            if (upgradeMenu == null)
            {
                upgradeMenu = Resources.FindObjectsOfTypeAll<UpgradeMenu>()[0].gameObject;
                for (int i = 0; i < upgradeMenu.transform.childCount; i++)
                {
                    GameObject child = upgradeMenu.transform.GetChild(i).gameObject;
                    if (child.name == "CloseButton")
                    {
                        child.GetComponent<Button>().onClick.AddListener(new System.Action(() =>
                        {
                            ToggleUpgradeMenu();
                        }));
                    }
                }
            }
            upgradeMenu.SetActive(!upgradeMenu.activeSelf);
        }

        private void ToggleDeliveryMenu()
        {
            if (deliveryMenu == null)
            {
                deliveryMenu = Resources.FindObjectsOfTypeAll<DeliveryMenu>()[0].gameObject;
                for (int i = 0; i < deliveryMenu.transform.childCount; i++)
                {
                    GameObject child = deliveryMenu.transform.GetChild(i).gameObject;
                    if (child.name == "CloseButton")
                    {
                        child.GetComponent<Button>().onClick.AddListener(new System.Action(() =>
                        {
                            ToggleDeliveryMenu();
                        }));
                    }
                }
            }
            deliveryMenu.SetActive(!deliveryMenu.activeSelf);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            PopulateGameManager();
            network = new NetworkManager(gameManager);

            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.gameObject.name == "MenuButton")
                {
                    button.onClick.AddListener(new System.Action(() =>
                    {
                        ToggleMainMenu();
                    }));
                } else if (button.gameObject.name == "UpgradeButton")
                {
                    button.onClick.AddListener(new System.Action(() =>
                    {
                        ToggleUpgradeMenu();
                    }));
                } else if (button.gameObject.name == "DeliveryButton")
                {
                    button.onClick.AddListener(new System.Action(() =>
                    {
                        ToggleDeliveryMenu();
                    }));
                }
            }

            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Canvas")
                {
                    canvasTransform = go.transform;
                }
            }

            ipPortInput = InputField(canvasTransform, new Vector2(150, 30), "ipandport", "IP:PORT");
            ipPortInput.gameObject.GetComponent<InputField>().onValueChanged.AddListener(new System.Action<string>((string value) =>
            {
                if (gameManager.RealSimulation != null)
                {
                    if (int.TryParse(value, out int result))
                    {
                        port = result;
                    }
                    else
                    {
                        port = 3000;
                    }
                }
                else
                {
                    if (value.Contains(':'))
                    {
                        string[] temp = value.Split(':');
                        ipAddress = temp[0];
                        if (int.TryParse(temp[1], out int result))
                        {
                            port = result;
                        }
                        else
                        {
                            port = 3000;
                        }
                    }
                    else
                    {
                        ipAddress = "127.0.0.1";
                        port = 3000;
                    }
                }
            }));

            chatInput = InputField(canvasTransform, new Vector2(150, 30), "chatbox", "Send a message...");
            chatInput.gameObject.GetComponent<InputField>().onSubmit.AddListener(new System.Action<string>((string value) =>
            {
                network.SendChatMessage(value);
                chatInput.gameObject.GetComponent<InputField>().text = "";
            }));
        }

        public override void OnUpdate()
        {

            if (gameManager.RealSimulation != null)
            {
                gameManager.GameSpeed = 1;
                gameManager.SetGameState(GameState.Game);
            }

            foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.gameObject.name == "NewGameButton")
                {
                    button.interactable = !network.IsActiveSession();
                } else if (button.gameObject.name == "LoadGameButton")
                {
                    button.interactable = !network.IsActiveSession();
                } else if (button.gameObject.name == "AddMarkerButton")
                {
                    button.gameObject.SetActive(false);
                }
            }

            network?.Update();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                visible = !visible;
            }

            if (gameManager.RealSimulation != null)
            {

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    ToggleMainMenu();
                }

                if (keyboard[gameManager.Input.keybinds[InputCode.ShowUpgradeMenu].KeyCode].wasPressedThisFrame)
                {
                    ToggleUpgradeMenu();
                }

                if (keyboard[gameManager.Input.keybinds[InputCode.ShowDeliveryMenu].KeyCode].wasPressedThisFrame)
                {
                    ToggleDeliveryMenu();
                }
            }
        }

        private static RectTransform InputField(Transform canvasTransform, Vector2 size, string name, string placeholder)
        {
            GameObject inputFieldObj = new(name);
            inputFieldObj.transform.SetParent(canvasTransform, false);

            RectTransform rectTransform = inputFieldObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.sizeDelta = new Vector2(size.x / canvasTransform.localScale.x, size.y / canvasTransform.localScale.y);

            Image bgImage = inputFieldObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            GameObject textObj = new("Text");
            textObj.transform.SetParent(inputFieldObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            Text textComp = textObj.AddComponent<Text>();
            textComp.color = Color.white;
            textComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComp.fontSize = 14;
            textComp.alignment = TextAnchor.MiddleCenter;

            GameObject placeholderObj = new("Placeholder");
            placeholderObj.transform.SetParent(inputFieldObj.transform, false);
            RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;

            Text placeholderComp = placeholderObj.AddComponent<Text>();
            placeholderComp.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            placeholderComp.font = textComp.font;
            placeholderComp.fontSize = 14;
            placeholderComp.text = placeholder;
            placeholderComp.alignment = TextAnchor.MiddleCenter;

            InputField inputField = inputFieldObj.AddComponent<InputField>();
            inputField.textComponent = textComp;
            inputField.placeholder = placeholderComp;

            return rectTransform;
        }

        public override void OnGUI()
        {
            if (visible && !network.IsActiveSession() && allowConnect)
            {
                window = GUI.Window(36354, window, (GUI.WindowFunction)DrawWindow, "Multiplayer Window");
                if (ipPortInput != null && !ipPortInput.gameObject.activeSelf)
                {
                    ipPortInput.gameObject.SetActive(true);
                }
            } else
            {
                if (ipPortInput != null && ipPortInput.gameObject.activeSelf)
                {
                    ipPortInput.gameObject.SetActive(false);
                }
            }

            if (network.IsActiveSession() && visible)
            {
                if (!chatInput.gameObject.activeSelf)
                {
                    chatInput.gameObject.SetActive(true);
                }
            } else
            {
                if (chatInput.gameObject.activeSelf)
                {
                    chatInput.gameObject.SetActive(false);
                }
            }

            if (chatInput != null)
            {
                chatInput.anchoredPosition = new Vector2(10 / canvasTransform.localScale.x, -30 / canvasTransform.localScale.y);
            }

            if (ipPortInput != null)
            {
                ipPortInput.anchoredPosition = new Vector2(window.x / canvasTransform.localScale.x, -(window.y + 20) / canvasTransform.localScale.y);
                for (int i = 0; i < ipPortInput.childCount; i++)
                {
                    GameObject child = ipPortInput.GetChild(i).gameObject;
                    if (child.name == "Placeholder")
                    {
                        child.GetComponent<Text>().text = (gameManager.RealSimulation != null) ? "PORT" : "IP:PORT";
                    }
                }
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
            GUI.DragWindow(new UnityEngine.Rect(0, 0, 150, 20));

            if (gameManager.RealSimulation != null)
            {
                if (Button(new UnityEngine.Rect(0, 50, 150, 30), "Host"))
                {
                    network?.StartServer(port);
                }
            }
            else
            {
                if (Button(new UnityEngine.Rect(0, 50, 150, 30), "Join"))
                {
                    network?.StartClient(ipAddress, port, "Super Secret Password");
                }
            }
        }

        public override void OnDeinitializeMelon()
        {
            network?.Stop();
        }

        private static bool Button(UnityEngine.Rect rect, string label)
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

    public class InitialSyncPacket
    {
        public string XmlSaveData { get; set; }
    }

    public class BuildingPacket
    {
        public bool IsAdd { get; set; }
        public string DefName { get; set; }
        public int Dir {  get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class BeltPacket
    {
        public bool IsBuild { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int EndX { get; set; }
        public int EndY { get; set; }
        public bool AutoBuildBridges { get; set; }
    }

    public class UpgradePacket
    {
        public bool Str { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
    }

    public class ChatMessagePacket
    {
        public string Name { get; set; }
        public string Message { get; set; }
    }

    public class NetworkManager(GameManager gameManager) : INetEventListener
    {
        private NetManager _netManager;
        private NetPacketProcessor _packetProcessor;
        public int type = 0;
        private readonly GameManager _gameManagerInstance = gameManager;
        private List<NetPeer> _cachedPeers = [];

        public bool IsActiveSession()
        {
            return type == 1 || type == 2;
        }

        public void StartServer(int port)
        {
            type = 1;
            InitializePacketProcessor();
            _netManager = new NetManager(this) { AutoRecycle = true };
            _netManager.Start(port);
            Melon<Core>.Logger.Msg($"Server started on port: {port}");
        }

        public void StartClient(string ip, int port, string connectionKey)
        {
            type = 2;
            InitializePacketProcessor();
            _netManager = new NetManager(this) { AutoRecycle = true };
            _netManager.Start();
            _netManager.Connect(ip, port, connectionKey);
            Melon<Core>.Logger.Msg($"Joined server at {ip}:{port}");
        }

        private void InitializePacketProcessor()
        {
            _packetProcessor = new();
            if (type == 2)
            {
                _packetProcessor.SubscribeReusable<InitialSyncPacket, NetPeer>((packet, peer) =>
                {
                    Melon<Core>.Logger.Msg("Received initial world sync payload from host");
                    try
                    {
                        byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(packet.XmlSaveData);
                        Il2CppStructArray<byte> il2cppBytes = new(xmlBytes);
                        xmlBytes.CopyTo(il2cppBytes, 0);
                        var memoryStream = new Il2CppSystem.IO.MemoryStream(il2cppBytes);
                        var xmlReader = Il2CppSystem.Xml.XmlReader.Create(memoryStream);
                        Il2CppSystem.Xml.Linq.XElement il2cppXmlData = Il2CppSystem.Xml.Linq.XElement.Load(xmlReader);
                        _gameManagerInstance.LoadGame(il2cppXmlData, false);
                        Melon<Core>.Logger.Msg("Game Manager successfully processed initial world state");
                    }
                    catch (System.Exception ex)
                    {
                        Melon<Core>.Logger.Error($"Failed to parse or load sync data: {ex}");
                    }
                });
            }

            _packetProcessor.SubscribeReusable<BuildingPacket, NetPeer>((packet, peer) =>
            {
                if (_gameManagerInstance?.RealSimulation == null) return;

                NetworkInterceptGuard.IsNetworkIncoming = true;
                try
                {
                    Simulation sim = _gameManagerInstance.RealSimulation.Cast<Simulation>();
                    if (packet.IsAdd)
                    {
                        sim.AddBuilding(packet.DefName, new Vector2i(packet.X, packet.Y), new MapDirection(packet.Dir), null);
                    } else
                    {
                        sim.RemoveBuilding(new Vector2i(packet.X, packet.Y), null);
                    }
                } finally
                {
                    NetworkInterceptGuard.IsNetworkIncoming = false;
                }
            });

            _packetProcessor.SubscribeReusable<BeltPacket, NetPeer>((packet, peer) =>
            {
                if (_gameManagerInstance?.RealSimulation == null) return;

                NetworkInterceptGuard.IsNetworkIncoming = true;
                try
                {
                    Simulation sim = _gameManagerInstance.RealSimulation.Cast<Simulation>();
                    if (packet.IsBuild)
                    {
                        sim.BuildBelt(new Vector2i(packet.StartX, packet.StartY), new Vector2i(packet.EndX, packet.EndY), packet.AutoBuildBridges, null);
                    } else
                    {
                        sim.RemoveBelt(new Vector2i(packet.StartX, packet.StartY), packet.AutoBuildBridges, null);
                    }
                } finally
                {
                    NetworkInterceptGuard.IsNetworkIncoming = false;
                }
            });

            _packetProcessor.SubscribeReusable<UpgradePacket, NetPeer>((packet, peer) =>
            {
                if (_gameManagerInstance?.RealSimulation == null) return;

                NetworkInterceptGuard.IsNetworkIncoming = true;
                try
                {
                    LevelManager manager = _gameManagerInstance.RealSimulation.LevelManager.Cast<LevelManager>();
                    if (packet.Str)
                    {
                        manager.BuyUpgrade(packet.Name, true);
                    } else
                    {
                        manager.BuyUpgrade((BuildingType)packet.Type, true);
                    }
                } finally
                {
                    NetworkInterceptGuard.IsNetworkIncoming = false;
                }
            });

            _packetProcessor.SubscribeReusable<ChatMessagePacket, NetPeer>((packet, peer) =>
            {
                if (_gameManagerInstance?.RealSimulation == null) return;

                NetworkInterceptGuard.IsNetworkIncoming = true;
                try
                {
                    Melon<Core>.Logger.Msg($"{packet.Name}: {packet.Message}");
                }
                finally
                {
                    NetworkInterceptGuard.IsNetworkIncoming = false;
                }
            });
        }

        public void SendBuildingUpdate(bool isAdd, string defName, int x, int y, int dir)
        {
            BuildingPacket packet = new() { IsAdd = isAdd, DefName = defName, X = x, Y = y, Dir = dir };
            BroadcastPacket(packet);
        }

        public void SendBeltUpdate(bool isBuild, int startX, int startY, int endX, int endY, bool autoBuildBridges)
        {
            BeltPacket packet = new() { IsBuild = isBuild, StartX = startX, StartY = startY, EndX = endX, EndY = endY, AutoBuildBridges = autoBuildBridges };
            BroadcastPacket(packet);
        }

        public void SendUpgradeUpdate(string name)
        {
            UpgradePacket packet = new() { Str = true, Name = name };
            BroadcastPacket(packet);
        }

        public void SendUpgradeUpdate(int buildingType)
        {
            UpgradePacket packet = new() { Str = false, Type = buildingType };
            BroadcastPacket(packet);
        }

        public void SendChatMessage(string message)
        {
            ChatMessagePacket packet = new() { Name = Melon<Core>.Instance.username, Message = message };
            BroadcastPacket(packet);
        }

        private void BroadcastPacket<T>(T packet) where T : class, new()
        {
            if (_netManager == null) return;
            NetDataWriter writer = new();
            _packetProcessor.Write(writer, packet);

            _cachedPeers.Clear();
            _netManager.GetConnectedPeers(_cachedPeers);
            foreach (var peer in _cachedPeers)
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        public void Update()
        {
            _netManager?.PollEvents();
        }

        public void Stop()
        {
            _netManager?.Stop();
            type = 0;
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Melon<Core>.Logger.Msg($"Connected: {peer.Id}");

            if (type == 1)
            {
                Melon<Core>.Logger.Msg($"Gathering sync data to transmit to client: {peer.Id}");
                try
                {
                    Il2CppSystem.Xml.Linq.XElement currentWorldState = _gameManagerInstance.GatherSaveData();
                    InitialSyncPacket packet = new()
                    {
                        XmlSaveData = currentWorldState.ToString()
                    };

                    NetDataWriter writer = new NetDataWriter();
                    _packetProcessor.Write(writer, packet);
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);

                    Melon<Core>.Logger.Msg($"Sent world sync data packet to {peer.Id}");
                }
                catch (System.Exception ex)
                {
                    Melon<Core>.Logger.Error($"Failed to gather or transmit state data: {ex}");
                }
            }
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            Melon<Core>.Logger.Msg($"Disconnected: {peer.Id}. Reason: {info.Reason}");
            if (type == 2)
            {
                Melon<Core>.Instance.ToggleMainMenu();
                Melon<Core>.Instance.mainMenu.GetComponent<MainMenu>().OnButtonPress("Exit");
            }
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            _packetProcessor.ReadAllPackets(reader, peer);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Melon<Core>.Logger.Error($"Network Error from {endPoint}: {socketError}");
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnConnectionRequest(ConnectionRequest request) => request.AcceptIfKey("Super Secret Password");
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    }

    public static class NetworkInterceptGuard
    {
        public static bool IsNetworkIncoming = false;
    }

    [HarmonyPatch]
    public static class Patch_AddBuilding
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Simulation),
                nameof(Simulation.AddBuilding),
                [typeof(string), typeof(Vector2i), typeof(MapDirection), typeof(UndoList)]
            );
        }

        public static void Postfix(string defName, Vector2i pos, MapDirection dir, UndoList undoList)
        {
            if (NetworkInterceptGuard.IsNetworkIncoming) return;
            Melon<Core>.Instance.network.SendBuildingUpdate(true, defName, pos.x, pos.y, dir.Value);
        }
    }

    [HarmonyPatch]
    public static class Patch_BuyUpgrade1
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(LevelManager),
                nameof(LevelManager.BuyUpgrade),
                [typeof(string), typeof(bool)]
            );
        }

        public static void Postfix(string name, bool forceUpgrade = false)
        {
            if (NetworkInterceptGuard.IsNetworkIncoming) return;
            Melon<Core>.Instance.network.SendUpgradeUpdate(name);
        }
    }

    [HarmonyPatch]
    public static class Patch_BuyUpgrade2
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(LevelManager),
                nameof(LevelManager.BuyUpgrade),
                [typeof(BuildingType), typeof(bool)]
            );
        }

        public static void Postfix(BuildingType buildingType, bool forceUpgrade = false)
        {
            if (NetworkInterceptGuard.IsNetworkIncoming) return;
            Melon<Core>.Instance.network.SendUpgradeUpdate((int)buildingType);
        }
    }

    [HarmonyPatch(typeof(Simulation), "RemoveBuilding")]
    public static class Patch_RemoveBuilding
    {
        public static void Postfix(Vector2i pos, UndoList undoList)
        {
            if (NetworkInterceptGuard.IsNetworkIncoming) return;
            Melon<Core>.Instance.network.SendBuildingUpdate(false, "", pos.x, pos.y, 0);
        }
    }

    [HarmonyPatch(typeof(Simulation), "BuildBelt")]
    public static class Patch_BuildBelt
    {
        public static void Postfix(Vector2i startPos, Vector2i endPos, bool autoBuildBridges, UndoList undoList)
        {
            if (NetworkInterceptGuard.IsNetworkIncoming) return;
            Melon<Core>.Instance.network.SendBeltUpdate(true, startPos.x, startPos.y, endPos.x, endPos.y, autoBuildBridges);
        }
    }

    [HarmonyPatch(typeof(Simulation), "RemoveBelt")]
    public static class Patch_RemoveBelt
    {
        public static void Postfix(Vector2i pos, bool removeConnections, UndoList undoList)
        {
            if (NetworkInterceptGuard.IsNetworkIncoming) return;
            Melon<Core>.Instance.network.SendBeltUpdate(false, pos.x, pos.y, 0, 0, removeConnections);
        }
    }

    [HarmonyPatch(typeof(GameManager), "get_IsPaused")]
    public static class Patch_get_IsPaused
    {
        public static bool Prefix(GameManager __instance, ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(GameManager), "get_NeedsSaving")]
    public static class Patch_get_NeedsSaving
    {
        public static bool Prefix(GameManager __instance, ref bool __result)
        {
            if (Melon<Core>.Instance.network.type == 2)
            {
                __result = false;
                return false;
            } else
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "DoAutosave")]
    public static class Patch_DoAutosave
    {
        public static bool Prefix()
        {
            if (Melon<Core>.Instance.network.type == 2)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
