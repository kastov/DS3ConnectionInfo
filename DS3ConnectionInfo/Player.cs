﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using Steamworks;
using System.Text.RegularExpressions;

namespace DS3ConnectionInfo
{
    public class Player
    {
        private const long baseB = 0x4768E78;


        private static Dictionary<CSteamID, Player> activePlayers = new Dictionary<CSteamID, Player>();

        private P2PSessionState_t sessionState;

        public CSteamID SteamID { get; private set; }
        public string SteamName { get; private set; }
        public ulong NetId { get; private set; }
        public string Region { get; private set; }
        public string CharSlot { get; private set; }
        public string CharName { get; private set; }
        public int TeamId { get; private set; }


        public int SoulLevel { get; private set; }
        public int CurrentHP { get; private set; }
        public int Vig { get; private set; }
        public int Att { get; private set; }
        public int End { get; private set; }
        public int Vit { get; private set; }
        public int Str { get; private set; }
        public int Dex { get; private set; }
        public int Int { get; private set; }
        public int Fth { get; private set; }
        public int Lck { get; private set; }


        public string TeamName => Team.GetTeamFromId(TeamId).Name;
        public TeamAllegiance TeamAlliegance => Team.GetTeamFromId(TeamId).Allegiance;
        public ulong SteamId64 => SteamID.m_SteamID;
        public double Ping => ETWPingMonitor.GetPing(NetId);
        public double AveragePing => ETWPingMonitor.GetAveragePing(NetId);
        public double Jitter => ETWPingMonitor.GetJitter(NetId);
        public double LatePacketRatio => ETWPingMonitor.GetLatePacketRatio(NetId);

        public SolidColorBrush SteamNameColor => new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            (CharSlot == "") ? Settings.Default.ConnectingColor : "#FFFFFFFF"));
        public SolidColorBrush TeamColor => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Team.GetTeamFromId(TeamId).Color));

        public string OverlayName => GetOverlayName();
        private string GetOverlayName()
        {
            string[] keyNames = new string[2] { "SteamName", "CharName" };
            string fmt = (CharSlot == "") ? Settings.Default.NameFormatConnecting : Settings.Default.NameFormat;
            return FormatUtils.NamedFormat(fmt, keyNames, SteamName, CharName);
        }

        public string PingColor
        {
            get
            {
                switch (Ping)
                {
                    case -1:
                        return Settings.Default.TextColor;
                    case double n when (n <= 50):
                        return Settings.Default.PingColor1;
                    case double n when (n <= 100):
                        return Settings.Default.PingColor2;
                    case double n when (n <= 200):
                        return Settings.Default.PingColor3;
                    default:
                        return Settings.Default.PingColor4;
                }
            }
        }


        public string HealthColor
        {
            get
            {
                switch (CurrentHP)
                {
                    case -1:
                        return Settings.Default.TextColor;
                    case int n when (n >= 1200):
                        return Settings.Default.PingColor1;
                    case int n when (n >= 600):
                        return Settings.Default.PingColor2;
                    case int n when (n <= 200):
                        return Settings.Default.PingColor3;
                    default:
                        return Settings.Default.PingColor4;
                }
            }
        }
        private Player(CSteamID steamID)
        {
            SteamID = steamID;
            SteamName = SteamFriends.GetFriendPersonaName(steamID);

            NetId = 0;
            sessionState = new P2PSessionState_t();

            CharSlot = "";
            TeamId = -1;
            CharName = "";
        }

        private void UpdateNetInfo(P2PSessionState_t session)
        {
            bool endpointChanged = sessionState.m_nRemoteIP != session.m_nRemoteIP || sessionState.m_nRemotePort != session.m_nRemotePort;
            sessionState = session;

            if (endpointChanged)
            {
                // If IP/port changed for whatever reason
                ETWPingMonitor.Unregister(NetId);

                Region = "...";

                byte[] ipBytes = BitConverter.GetBytes(sessionState.m_nRemoteIP).Reverse().ToArray();
                NetId = (ulong)sessionState.m_nRemotePort << 32 | BitConverter.ToUInt32(ipBytes, 0);
                ETWPingMonitor.Register(NetId);

                if (session.m_bUsingRelay == 0)
                    IpLocationAPI.GetLocationAsync(new IPAddress(ipBytes).ToString(), r => Region = r);
                else
                    Region = "[STEAM RELAY]";
            }
        }

        public static IEnumerable<Player> ActivePlayers()
        {
            return activePlayers.Values.AsEnumerable();
        }

        public static void UpdateInGameInfo()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                try
                {
                    if (DS3Interop.GetPlayerBase(slot) == 0) continue;

                    CSteamID id = DS3Interop.GetTruePlayerSteamId(slot);
                    if (!activePlayers.ContainsKey(id)) continue;

                    activePlayers[id].CharSlot = slot.ToString();
                    activePlayers[id].CharName = DS3Interop.GetPlayerName(slot);
                    activePlayers[id].TeamId = DS3Interop.GetPlayerTeam(slot);
                    activePlayers[id].SoulLevel = DS3Interop.GetPlayerSL(slot);
                    activePlayers[id].CurrentHP = DS3Interop.GetPlayerCurrentHP(slot);
                    activePlayers[id].Vig = DS3Interop.GetPlayerVig(slot);
                    activePlayers[id].Att = DS3Interop.GetPlayerAtt(slot);
                    activePlayers[id].End = DS3Interop.GetPlayerEnd(slot);
                    activePlayers[id].Vit = DS3Interop.GetPlayerVit(slot);
                    activePlayers[id].Str = DS3Interop.GetPlayerStr(slot);
                    activePlayers[id].Dex = DS3Interop.GetPlayerDex(slot);
                    activePlayers[id].Int = DS3Interop.GetPlayerInt(slot);
                    activePlayers[id].Fth = DS3Interop.GetPlayerFth(slot);
                    activePlayers[id].Lck = DS3Interop.GetPlayerLck(slot);
                }
                catch (Exception)
                {

                }
            }
        }

        public static void UpdatePlayerList()
        {
            // There's probably a better way to get all current active P2P connections,
            // but I couldn't find one. The SessionInfo pointers are not reliable as 
            // players can spoof their Steam ID there.
            int cnt = SteamFriends.GetCoplayFriendCount();
            for (int i = 0; i < cnt; i++)
            {
                CSteamID id = SteamFriends.GetCoplayFriend(i);

                P2PSessionState_t session = new P2PSessionState_t();
                if (!SteamNetworking.GetP2PSessionState(id, out session) || (session.m_bConnectionActive == 0 && session.m_bConnecting == 0))
                {
                    if (activePlayers.ContainsKey(id))
                    {
                        ETWPingMonitor.Unregister(activePlayers[id].NetId);
                        activePlayers.Remove(id);
                    }
                    continue;
                }
                if (!activePlayers.ContainsKey(id))
                    activePlayers[id] = new Player(id);

                activePlayers[id].UpdateNetInfo(session);
            }
        }
    }
}
