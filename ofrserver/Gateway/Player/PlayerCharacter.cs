﻿using Gateway.Login;
using log4net;
using Newtonsoft.Json;
using SOE;
using SOE.Core;
using SOE.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static Gateway.Login.ClientPcData;

namespace Gateway.Player
{
    public class PlayerCharacter : IDisposable
    {
        private static SOEServer _server = Gateway.Server;
        private static ILog _log = _server.Log;
        private static Random _random = new Random();

        public readonly SOEClient client;
        public readonly uint playerGUID;

        public float[] position;
        public float[] rotation;
        public byte characterState;
        public byte unknown;

        public float[] lastBroadcastedPosition;
        public int lastBroadcastedTime;

        public ClientPcDatas CharacterData;

        public PlayerCharacter(SOEClient soeClient, ClientPcDatas characterData)
        {
            client = soeClient;
            playerGUID = (uint)characterData.PlayerGUID;

            position = new float[3];
            for (int i = 0; i < position.Length; i++)
                position[i] = characterData.PlayerPosition[i];

            rotation = new float[3];
            for (int i = 0; i < rotation.Length; i++)
                rotation[i] = characterData.CameraRotation[i];

            characterState = 0x00;
            unknown = 0x00;

            lastBroadcastedPosition = new float[3];
            for (int i = 0; i < lastBroadcastedPosition.Length; i++)
                lastBroadcastedPosition[i] = 0;

            lastBroadcastedTime = 0;

            CharacterData = characterData;
        }

        public void Dispose()
        {
            _log.Debug($"Attempting to dispose of \"{CharacterData.FirstName} {CharacterData.LastName}\"");

            var removePlayer = new SOEWriter((ushort)BasePackets.BasePlayerUpdatePacket, true);
            removePlayer.AddHostUInt16((ushort)BasePlayerUpdatePackets.PlayerUpdatePacketRemovePlayer);

            removePlayer.AddBoolean(false);
            removePlayer.AddBoolean(false);
            removePlayer.AddHostUInt64(playerGUID); // Player GUID

            List<SOEClient> Clients = _server.ConnectionManager.Clients;
            for (int i = 0; i < Clients.Count; i++)
            {
                if (Clients[i] == null) continue;
                LoginManager.SendTunneledClientPacket(Clients[i], removePlayer.GetRaw());
            }
        }

        public void SpawnPcFor(SOEClient target)
        {
            _log.Debug($"Attempting to spawn \"{CharacterData.FirstName} {CharacterData.LastName}\" (#{client.GetClientID()}) for Client #{target.GetClientID()}");

            var addPc = new SOEWriter((ushort)BasePackets.BasePlayerUpdatePacket, true);

            addPc.AddHostUInt16((ushort)BasePlayerUpdatePackets.PlayerUpdatePacketAddPc);
            addPc.AddHostUInt64(playerGUID); // Player GUID

            addPc.AddHostInt32(0); // DisplayInformation.Unknown1
            addPc.AddHostInt32(0); // DisplayInformation.Unknown2
            addPc.AddHostInt32(0); // DisplayInformation.Unknown3

            addPc.AddASCIIString(CharacterData.FirstName);
            addPc.AddASCIIString(CharacterData.LastName);

            addPc.AddHostInt32(CharacterData.PlayerModel);
            addPc.AddHostInt32(408679); // Unknown3
            addPc.AddHostInt32(13951728); // Unknown4
            addPc.AddHostInt32(1); // Unknown5

            for (var i = 0; i < position.Length; i++) // Position
                addPc.AddFloat(position[i]);
            addPc.AddFloat(1.0f);

            for (var i = 0; i < rotation.Length; i++) // Rotation
                addPc.AddFloat(rotation[i]);
            addPc.AddFloat(0.0f);

            List<(int, ProfileItem)> profileItems = CharacterData.ClientPcProfiles[0].Items; // 0 means adventurer
            List<ClientItemDefinition> equippedItems = new List<ClientItemDefinition>();
            foreach ((int, ProfileItem) item in profileItems)
            {
                int itemGUID = item.Item2.ItemGUID;

                var clientItem = CharacterData.ClientItems.Find(x => x.Guid == itemGUID);

                if (clientItem == null)
                    continue;

                ClientItemDefinition itemDefintion = LoginManager.ClientItemDefinitions.Find(x => x.Id == clientItem.Definition);

                if (itemDefintion != null)
                    equippedItems.Add(itemDefintion);
            }

            addPc.AddHostInt32(equippedItems.Count);
            for (var i = 0; i < equippedItems.Count; i++)
            {
                ClientItemDefinition item = equippedItems[i];
                addPc.AddASCIIString(item.ModelName);
                addPc.AddASCIIString(item.TextureAlias);
                addPc.AddASCIIString(item.TintAlias);
                addPc.AddHostInt32(item.IconData.TintId);
                addPc.AddHostInt32(0);
                addPc.AddHostInt32(i + 1);
            }

            addPc.AddASCIIString(CharacterData.PlayerHead);
            addPc.AddASCIIString(CharacterData.PlayerHair);
            addPc.AddHostInt32(CharacterData.HairColor);
            addPc.AddHostInt32(CharacterData.EyeColor);
            addPc.AddHostInt32(0); // Unknown12
            addPc.AddASCIIString(CharacterData.Skintone);
            addPc.AddASCIIString(CharacterData.FacePaint);
            addPc.AddASCIIString(CharacterData.HumanBeardsPixieWings);

            addPc.AddHostInt32(1090519040); // Unknown16
            addPc.AddBoolean(false); // Unknown17

            addPc.AddBoolean(true); // Unknown18
            addPc.AddBoolean(false); // Unknown19

            addPc.AddHostInt32(0); // Unknown20

            addPc.AddHostInt32(0); // GuildGUID Count
            // TODO: Guilds

            addPc.AddHostInt32(1); // Job/Class, Placeholder for Adventurer

            addPc.AddHostInt32(0); // PlayerTitle.GUID
            addPc.AddHostInt32(0); // PlayerTitle.Unknown2
            addPc.AddHostInt32(0); // PlayerTitle.NameId
            addPc.AddHostInt32(0); // PlayerTitle.Unknown4

            addPc.AddHostInt32(CharacterData.EffectTags.Count); // EffectTagCount
            foreach ((int, ClientEffectTag) effectTag in CharacterData.EffectTags)
            {
                addPc.AddHostInt32(effectTag.Item2.Unknown); // EffectTag.Unknown
                addPc.AddHostInt32(effectTag.Item2.Unknown2); // EffectTag.Unknown2
                addPc.AddHostInt32(effectTag.Item2.Unknown3); // EffectTag.Unknown3

                addPc.AddHostInt32(effectTag.Item2.Unknown4); // EffectTag.Unknown4
                addPc.AddHostInt32(effectTag.Item2.Unknown5); // EffectTag.Unknown5
                addPc.AddHostInt32(effectTag.Item2.Unknown6); // EffectTag.Unknown6
                addPc.AddHostInt32(effectTag.Item2.Unknown7); // EffectTag.Unknown7

                addPc.AddBoolean(effectTag.Item2.Unknown8); // EffectTag.Unknown8

                addPc.AddHostInt64(effectTag.Item2.Unknown9); // EffectTag.Unknown9
                addPc.AddHostInt32(effectTag.Item2.Unknown10); // EffectTag.Unknown10, Stored as long, epoch time?
                addPc.AddHostInt32(effectTag.Item2.Unknown11); // EffectTag.Unknown11, Stored as long, epoch time?

                addPc.AddHostInt32(effectTag.Item2.Unknown12); // EffectTag.Unknown12
                addPc.AddHostInt32(effectTag.Item2.Unknown13); // EffectTag.Unknown13
                addPc.AddHostInt64(effectTag.Item2.Unknown14); // EffectTag.Unknown14
                addPc.AddHostInt32(effectTag.Item2.Unknown15); // EffectTag.Unknown15
                addPc.AddHostInt32(effectTag.Item2.Unknown16); // EffectTag.Unknown16

                addPc.AddBoolean(effectTag.Item2.Unknown17); // EffectTag.Unknown17
                addPc.AddBoolean(effectTag.Item2.Unknown18); // EffectTag.Unknown18
                addPc.AddBoolean(effectTag.Item2.Unknown19); // EffectTag.Unknown19
            }

            addPc.AddHostInt64(0); // Unknown22
            addPc.AddHostInt32(-1); // Unknown23
            addPc.AddHostInt32(-1); // Unknown24
            addPc.AddFloat(0); // Unknown25
            addPc.AddHostInt32(0); // Unknown26
            addPc.AddFloat(0); // Unknown27
            addPc.AddHostInt32(0); // Unknown28
            addPc.AddHostInt32(0); // Unknown29
            addPc.AddHostInt32(0); // Unknown30

            LoginManager.SendTunneledClientPacket(target, addPc.GetRaw());
        }

        public void SendPlayerUpdatePacketUpdatePosition(SOEClient soeClient)
        {
            var soeWriter = new SOEWriter((ushort)BasePackets.PlayerUpdatePacketUpdatePosition, true);
            soeWriter.AddHostUInt64(playerGUID);

            for (var i = 0; i < 3; i++)
                soeWriter.AddFloat(position[i]);

            for (var i = 0; i < 3; i++)
                soeWriter.AddFloat(rotation[i]);

            soeWriter.AddByte(characterState);
            soeWriter.AddByte(unknown);

            LoginManager.SendTunneledClientPacket(soeClient, soeWriter.GetRaw());
            /*
            var soeWriter2 = new SOEWriter((ushort)BasePackets.BaseChatPacket, true);
            soeWriter2.AddHostUInt16((ushort)BaseChatPackets.PacketChat);
            soeWriter2.AddHostUInt16(0);
            soeWriter2.AddHostUInt64(playerGUID); // Player GUID

            soeWriter2.AddBytes(LoginManager.StringToByteArray("48362C00DA71657D000000000000000000000000"));
            soeWriter2.AddASCIIString(CharacterData.FirstName);
            soeWriter2.AddASCIIString(CharacterData.LastName);
            soeWriter2.AddBytes(LoginManager.StringToByteArray("0000000000000000000000000000000000000000"));
            soeWriter2.AddASCIIString(CharacterData.LastName);
            soeWriter2.AddBytes(LoginManager.StringToByteArray("4FC10D436EECBB413078B7430000803F000000000000000002000000"));

            LoginManager.SendTunneledClientPacket(soeClient, soeWriter2.GetRaw());
            */
        }
        private double Magnitude(float[] pos0, float[] pos1)
        {
            return Math.Sqrt(
                Math.Pow(pos1[0] - pos0[0], 2) +
                Math.Pow(pos1[1] - pos0[1], 2) +
                Math.Pow(pos1[2] - pos0[2], 2)
            );
        }

        public void SendPacketChat(SOEClient sender, ushort messageType, ulong guid1, ulong guid2, string message, string targetFirst, string targetLast)
        {
            var packetChat = new SOEWriter((ushort)BasePackets.BaseChatPacket, true);
            packetChat.AddHostUInt16((ushort)BaseChatPackets.PacketChat);

            packetChat.AddHostUInt16(messageType);
            packetChat.AddHostUInt64(playerGUID); // Sender's Character GUID
            packetChat.AddHostUInt64(guid2);

            for (int i = 0; i < 3; i++)
                packetChat.AddHostInt32(0);
            packetChat.AddASCIIString(CharacterData.FirstName);
            packetChat.AddASCIIString(CharacterData.LastName);

            for (int i = 0; i < 3; i++)
                packetChat.AddHostInt32(0);
            packetChat.AddASCIIString(targetFirst);
            packetChat.AddASCIIString(targetLast);

            packetChat.AddASCIIString(message);

            for (var i = 0; i < position.Length; i++) // Position
                packetChat.AddFloat(position[i]);
            packetChat.AddFloat(1.0f);


            packetChat.AddHostUInt64(0);
            packetChat.AddHostUInt32(2);
            if (messageType == 8)
                packetChat.AddHostUInt32(0);

            List<SOEClient> Clients = _server.ConnectionManager.Clients;

            PlayerCharacter targetCharacter = LoginManager.PlayerCharacters.Find(x => x.CharacterData.FirstName == targetFirst && x.CharacterData.LastName == targetLast);
            if (messageType == 1)
            {
                if (targetCharacter != null) return; // player disconnected, don't leak
                Clients = new List<SOEClient>();
                Clients.Add(sender);
                Clients.Add(targetCharacter.client);
            }
            for (int i = 0; i < Clients.Count; i++)
            {
                if (Clients[i] == null) continue;
                SOEClient otherClient = Clients[i];
                PlayerCharacter otherCharacter = LoginManager.PlayerCharacters.Find(x => x.client == otherClient);
                if (otherCharacter == null) continue;
                if (Magnitude(position, otherCharacter.position) <= 50.0)
                    LoginManager.SendTunneledClientPacket(otherClient, packetChat.GetRaw());
            }
        }
    }
}
