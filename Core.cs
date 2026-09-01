using MelonLoader;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

[assembly: MelonInfo(typeof(Multiplayer.Core), "Multiplayer", "1.0.0", "Jayden", null)]
[assembly: MelonGame("Notional Games", "Beltmatic")]

namespace Multiplayer
{
    public class Core : MelonMod
    {
        [DllImport("steam_api64", EntryPoint = "SteamAPI_Init", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SteamAPI_Init();

        [DllImport("steam_api64", EntryPoint = "SteamAPI_RunCallbacks", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_RunCallbacks();

        [DllImport("steam_api64", EntryPoint = "SteamAPI_GetHSteamUser", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_GetHSteamUser();

        [DllImport("steam_api64", EntryPoint = "SteamAPI_SteamMatchmaking_v009", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_SteamMatchmaking();

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamMatchmaking_CreateLobby", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeCreateLobby(IntPtr pSteamMatchmaking, int eLobbyType, int cMaxMembers);

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamMatchmaking_JoinLobby", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NativeJoinLobby(IntPtr pSteamMatchmaking, ulong steamIDLobby);

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamMatchmaking_GetLobbyOwner", CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong NativeGetLobbyOwner(IntPtr pSteamMatchmaking, ulong steamIDLobby);

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamUser_GetSteamID", CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong NativeGetMySteamID();

        [DllImport("steam_api64", EntryPoint = "SteamAPI_SteamNetworking_v006", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_SteamNetworking();

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworking_IsP2PPacketAvailable", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool NativeIsP2PPacketAvailable(IntPtr pSteamNetworking, out uint pcubMsgSize, int nChannel);

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworking_ReadP2PPacket", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool NativeReadP2PPacket(IntPtr pSteamNetworking, byte[] pubDest, uint cubDest, out uint pcubMsgSize, out ulong psteamIDRemote, int nChannel);

        [DllImport("steam_api64", EntryPoint = "SteamAPI_ISteamNetworking_SendP2PPacket", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool NativeSendP2PPacket(IntPtr pSteamNetworking, ulong steamIDRemote, byte[] pubData, uint cubData, int eP2PSendType, int nChannel);


        private bool isSteamActive = false;
        private bool hasJoinedSession = false;
        private int lobbytimer = 0;
        private IntPtr steamUserHandle = IntPtr.Zero;
        private Rect window = new(10, 10, 150, 230);
        private ulong lobbyid = 0;
        GUIStyle centered;
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
        }

        public override void OnLateInitializeMelon()
        {
            try
            {
                MelonLogger.Msg("Binding directly to native steam_api64.dll...");

                if (SteamAPI_Init())
                {
                    MelonLogger.Msg("Direct native Steam pipeline established!");

                    steamUserHandle = SteamAPI_GetHSteamUser();
                    MelonLogger.Msg($"Fetched active Steam user pointer: {steamUserHandle}");

                    isSteamActive = true;
                }
                else
                {
                    MelonLogger.Error("Native Steam initialization failed.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"P/Invoke Mapping Failure: {ex.Message}");
            }
        }

        public override void OnGUI()
        {
            if (isSteamActive)
            {
                window = GUI.Window(45753, window, (GUI.WindowFunction)DrawWindow, "Multiplayer");
            }
        }

        private void ReceiveNetworkPackets()
        {
            try
            {
                IntPtr networkingPtr = SteamAPI_SteamNetworking();

                if (networkingPtr == IntPtr.Zero)
                {
                    return;
                }

                while (NativeIsP2PPacketAvailable(networkingPtr, out uint msgSize, 0))
                {
                    if (msgSize == 0) continue;

                    byte[] incomingBytes = new byte[msgSize];
                    if (NativeReadP2PPacket(networkingPtr, incomingBytes, msgSize, out uint bytesRead, out ulong senderSteamId, 0))
                    {
                        string dataString = System.Text.Encoding.UTF8.GetString(incomingBytes);
                        MelonLogger.Msg($"[Network] Packet from {senderSteamId}: {dataString}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Network Error] Memory fault or pointer misalignment: {ex.Message}");
            }
        }

        public override void OnUpdate()
        {
            if (!isSteamActive) { return; }
            SteamAPI_RunCallbacks();
            if (hasJoinedSession && steamUserHandle != IntPtr.Zero)
            {
                ReceiveNetworkPackets();
            }
        }

        public override void OnFixedUpdate()
        {
            if (lobbytimer > 0)
            {
                lobbytimer--;
                if (lobbytimer == 0)
                {
                    IntPtr matchmakingPtr = SteamAPI_SteamMatchmaking();
                    if (matchmakingPtr != IntPtr.Zero)
                    {
                        ulong mySteamId = NativeGetMySteamID();
                        LoggerInstance.Msg($"Your Personal SteamID is: {mySteamId}");
                        LoggerInstance.Msg("To share this session, have your friend use your SteamID directly");
                        lobbyid = mySteamId;
                    } else
                    {
                        lobbytimer = 100;
                    }
                }
            }
        }

        private void DrawWindow(int id)
        {
            if (centered == null)
            {
                centered = new GUIStyle();
                centered.normal.textColor = Color.white;
                centered.alignment = TextAnchor.MiddleCenter;
            }

            GUI.DragWindow(new Rect(0, 0, 150, 20));

            if (hasJoinedSession)
            {
                GUI.Box(new Rect(0, 20, 150, 30), lobbyid.ToString(), centered);
            } else
            {
                if (Button(new Rect(0, 20, 150, 30), "Host Lobby"))
                {
                    if (isSteamActive)
                    {
                        IntPtr matchmakingPtr = SteamAPI_SteamMatchmaking();
                        if (matchmakingPtr == IntPtr.Zero)
                        {
                            LoggerInstance.Error("Failed to resolve native ISteamMatchmaking pointer.");
                        }
                        else
                        {
                            LoggerInstance.Msg($"Hosting lobby using interface pointer: {matchmakingPtr}");
                            NativeCreateLobby(matchmakingPtr, 2, 4);
                            window.height = 50;
                            hasJoinedSession = true;
                            lobbytimer = 100;
                        }
                    }
                }

                GUI.Box(new Rect(0, 50, 150, 30), lobbyid.ToString(), centered);

                if (Button(new Rect(30, 80, 30, 30), "1"))
                {
                    lobbyid *= 10;
                    lobbyid += 1;
                }

                if (Button(new Rect(60, 80, 30, 30), "2"))
                {
                    lobbyid *= 10;
                    lobbyid += 2;
                }

                if (Button(new Rect(90, 80, 30, 30), "3"))
                {
                    lobbyid *= 10;
                    lobbyid += 3;
                }

                if (Button(new Rect(30, 110, 30, 30), "4"))
                {
                    lobbyid *= 10;
                    lobbyid += 4;
                }

                if (Button(new Rect(60, 110, 30, 30), "5"))
                {
                    lobbyid *= 10;
                    lobbyid += 5;
                }

                if (Button(new Rect(90, 110, 30, 30), "6"))
                {
                    lobbyid *= 10;
                    lobbyid += 6;
                }

                if (Button(new Rect(30, 140, 30, 30), "7"))
                {
                    lobbyid *= 10;
                    lobbyid += 7;
                }

                if (Button(new Rect(60, 140, 30, 30), "8"))
                {
                    lobbyid *= 10;
                    lobbyid += 8;
                }

                if (Button(new Rect(90, 140, 30, 30), "9"))
                {
                    lobbyid *= 10;
                    lobbyid += 9;
                }

                if (Button(new Rect(30, 170, 30, 30), "C"))
                {
                    lobbyid = 0;
                }

                if (Button(new Rect(60, 170, 30, 30), "0"))
                {
                    lobbyid *= 10;
                }

                if (Button(new Rect(90, 170, 30, 30), "<"))
                {
                    lobbyid /= 10;
                }

                if (Button(new Rect(0, 200, 150, 30), "Join Lobby"))
                {
                    if (isSteamActive)
                    {
                        IntPtr matchmakingPtr = SteamAPI_SteamMatchmaking();
                        if (matchmakingPtr != IntPtr.Zero)
                        {
                            LoggerInstance.Msg($"Connecting to host: {lobbyid}");
                            NativeJoinLobby(matchmakingPtr, lobbyid);
                            hasJoinedSession = true;
                            LoggerInstance.Msg("Network packet listener activated for client connection.");
                            window.height = 50;
                        }
                    }
                }
            }
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
}