using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using H1xl.Shared;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;
using Console = RoR2.Console;

namespace JustDropItems
{
    /// <summary>
    /// Networking without extra dependencies.
    /// <para>Client to host: console commands flagged ExecuteOnServer. The game forwards them through the sender's own
    /// <see cref="NetworkUser"/>, so the host always knows who sent a request and a client can't pretend to be someone else.</para>
    /// <para>Host to client: one raw message type, sent only to players who said hello, so players without the mod never get it.</para>
    /// </summary>
    internal static class Net
    {
        private const short MsgType = 0x4A44; // "JD"
        private const int Protocol = 1;
        private const string CmdHello = "justdrop_hello", CmdDrop = "justdrop_drop", CmdGive = "justdrop_give";

        private enum Msg : byte { Rules = 1, Notice = 2, Reject = 3 }

        internal enum NoticeType : byte { Dropped, Gave }

        /// <summary>AltEquipment: the equipment shown by the second equipment icon (MUL-T's other slot); only in requests.</summary>
        internal enum Target : byte { Item, Equipment, AltEquipment }

        // Client side: the host's rules, null until the host answers the hello (a host without the mod never does).
        internal static Rules HostRules;
        private static float helloSentAt = -100f;
        private static int helloTries;

        // Host side: players who have the mod.
        private static readonly HashSet<NetworkUser> modded = new HashSet<NetworkUser>();

        /// <summary>
        /// True while a chat command runs. Its sender may not have the mod, so a refusal is then also written to the chat
        /// as plain text in the host's language.
        /// </summary>
        internal static bool ChatContext;

        private sealed class Payload : MessageBase
        {
            public byte[] data;
            public override void Serialize(NetworkWriter writer) => writer.WriteBytesAndSize(data, data.Length);
            public override void Deserialize(NetworkReader reader) => data = reader.ReadBytesAndSize();
        }

        internal static void Init()
        {
            RoR2Application.onLoad += RegisterCommands;
            NetworkManagerSystem.onStartClientGlobal += client => client.RegisterHandler(MsgType, OnClientMessage);
            NetworkManagerSystem.onStopClientGlobal += () => { HostRules = null; helloTries = 0; };
            NetworkManagerSystem.onStopServerGlobal += () => modded.Clear();
            NetworkUser.onPostNetworkUserStart += user =>
            {
                if (user && user.isLocalPlayer) { helloTries = 0; SendHello(); }
            };
            NetworkUser.onNetworkUserLost += user => modded.Remove(user);
            Settings.RulesChanged += () => { if (NetworkServer.active) foreach (NetworkUser u in modded) SendRules(u); };
        }

        // ---------------------------------------------------------------- client

        private static NetworkUser LocalUser => LocalUserManager.GetFirstLocalUser()?.currentNetworkUser;

        /// <summary>Asks the host for its rules. Repeated a few times by the UI in case the first hello was too early.</summary>
        internal static void SendHello()
        {
            if (HostRules != null || !LocalUser || Console.instance == null) return;
            if (Time.unscaledTime - helloSentAt < 3f || helloTries >= 5) return;
            helloSentAt = Time.unscaledTime;
            helloTries++;
            Submit($"{CmdHello} {Protocol}");
        }

        /// <summary>True once the host answered; false means the host doesn't have the mod (or hasn't answered yet).</summary>
        internal static bool HostReady
        {
            get
            {
                if (HostRules == null) SendHello();
                return HostRules != null;
            }
        }

        /// <summary>The hello was sent several times with no answer: the host doesn't have the mod.</summary>
        internal static bool HostMissing => HostRules == null && helloTries >= 5 && Time.unscaledTime - helloSentAt > 3f;

        // The last argument is the player's BarOrder: the order copies leave the stack, as on their amount bar.
        internal static void RequestDrop(CharacterMaster owner, Target target, int index, int amount) =>
            Submit($"{CmdDrop} {NetId(owner)} {(int)target} {index} {amount} {(int)Settings.barOrder.Value}");

        internal static void RequestGive(CharacterMaster owner, Target target, int index, int amount, CharacterMaster recipient) =>
            Submit($"{CmdGive} {NetId(owner)} {(int)target} {index} {amount} {NetId(recipient)} {(int)Settings.barOrder.Value}");

        private static uint NetId(CharacterMaster m) => m && m.networkIdentity ? m.networkIdentity.netId.Value : 0u;

        private static void Submit(string cmd)
        {
            NetworkUser user = LocalUser;
            if (!user || Console.instance == null) return;
            if (Log.Verbose) Log.Info("Request: " + cmd);
            Console.instance.SubmitCmd(user, cmd);
        }

        private static void OnClientMessage(NetworkMessage netMsg)
        {
            try
            {
                var payload = netMsg.ReadMessage<Payload>();
                var r = new NetworkReader(payload.data);
                switch ((Msg)r.ReadByte())
                {
                    case Msg.Rules:
                        HostRules = Rules.Read(r);
                        if (Log.Verbose) Log.Info("Host rules received.");
                        break;
                    case Msg.Notice:
                        Chat.AddMessage(Notices.FormatNotice((NoticeType)r.ReadByte(), r.ReadString(), r.ReadString(), r.ReadString(),
                            (Target)r.ReadByte(), (int)r.ReadPackedUInt32() - 1, (int)r.ReadPackedUInt32(), (int)r.ReadPackedUInt32(),
                            (int)r.ReadPackedUInt32()));
                        break;
                    case Msg.Reject:
                        Chat.AddMessage(Notices.FormatReject((Tokens.Reject)r.ReadByte(), (Target)r.ReadByte(), (int)r.ReadPackedUInt32() - 1,
                            r.ReadString()));
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error("Bad message from the host: " + e);
            }
        }

        // ---------------------------------------------------------------- host

        // Console commands are registered by hand: the game only scans its own assembly for [ConCommand].
        private static void RegisterCommands()
        {
            try
            {
                FieldInfo catalogField = GameMembers.Console_concommandCatalog.Find();
                Type conCommandType = GameMembers.ConCommand_flags.Type;
                FieldInfo flags = GameMembers.ConCommand_flags.Find(), action = GameMembers.ConCommand_action.Find(),
                    helpText = GameMembers.ConCommand_helpText.Find();
                if (catalogField == null || conCommandType == null || flags == null || action == null || helpText == null
                    || Console.instance == null)
                {
                    Log.Error("Can't register console commands (game updated?): dropping and giving won't work.");
                    return;
                }
                var catalog = (IDictionary)catalogField.GetValue(Console.instance);
                void Add(string name, Console.ConCommandDelegate run, string help)
                {
                    object cmd = Activator.CreateInstance(conCommandType, true);
                    flags.SetValue(cmd, ConVarFlags.ExecuteOnServer);
                    action.SetValue(cmd, run);
                    helpText.SetValue(cmd, help);
                    catalog[name] = cmd;
                }
                Add(CmdHello, OnHello, "Just Drop Items: a player with the mod joined.");
                Add(CmdDrop, OnDrop, "Just Drop Items: drop {owner} {item|equipment} {index} {amount}.");
                Add(CmdGive, OnGive, "Just Drop Items: give {owner} {item|equipment} {index} {amount} {recipient}.");
            }
            catch (Exception e)
            {
                Log.Error("Can't register console commands: " + e);
            }
        }

        private static void OnHello(ConCommandArgs args)
        {
            if (!NetworkServer.active || !args.sender) return;
            modded.Add(args.sender);
            SendRules(args.sender);
            if (Log.Verbose) Log.Info($"{Exchange.NameOf(args.sender)} has the mod.");
        }

        private static void OnDrop(ConCommandArgs args)
        {
            if (!NetworkServer.active || !args.sender || args.userArgs.Count < 4) return;
            modded.Add(args.sender);
            if (!TryParse(args.userArgs, out CharacterMaster owner, out Target target, out int index, out int amount)) return;
            Exchange.Drop(args.sender, owner, target, index, amount, Order(args.userArgs, 4));
        }

        private static void OnGive(ConCommandArgs args)
        {
            if (!NetworkServer.active || !args.sender || args.userArgs.Count < 5) return;
            modded.Add(args.sender);
            if (!TryParse(args.userArgs, out CharacterMaster owner, out Target target, out int index, out int amount)) return;
            uint.TryParse(args.userArgs[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint recipientId);
            Exchange.Give(args.sender, owner, target, index, amount, FindMaster(recipientId), Order(args.userArgs, 5));
        }

        /// <summary>The optional order argument; anything missing or out of range means the default order.</summary>
        private static KindOrder Order(List<string> a, int i) =>
            a.Count > i && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int o) && o >= 0 && o <= (int)KindOrder.TemporaryEtherealPermanent
                ? (KindOrder)o : KindOrder.PermanentEtherealTemporary;

        private static bool TryParse(List<string> a, out CharacterMaster owner, out Target target, out int index, out int amount)
        {
            owner = null;
            target = Target.Item;
            index = -1;
            amount = 0;
            if (!uint.TryParse(a[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ownerId) ||
                !int.TryParse(a[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int t) || t < 0 || t > 2 ||
                !int.TryParse(a[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) ||
                !int.TryParse(a[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out amount))
                return false;
            target = (Target)t;
            owner = FindMaster(ownerId);
            return true;
        }

        private static CharacterMaster FindMaster(uint netId)
        {
            if (netId == 0) return null;
            GameObject go = NetworkServer.FindLocalObject(new NetworkInstanceId(netId));
            return go ? go.GetComponent<CharacterMaster>() : null;
        }

        private static void SendRules(NetworkUser user)
        {
            var w = new NetworkWriter();
            w.Write((byte)Msg.Rules);
            Exchange.CurrentRules.Write(w);
            Send(user, w);
        }

        internal static void SendReject(NetworkUser user, Tokens.Reject reason, Target target = Target.Item, int index = -1, string name = "")
        {
            if (Log.Verbose) Log.Info($"Refused {Exchange.NameOf(user)}: {reason}");
            if (!user) return;
            if (!modded.Contains(user))
            {
                if (ChatContext)
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Notices.FormatReject(reason, target, index, name) });
                return;
            }
            var w = new NetworkWriter();
            w.Write((byte)Msg.Reject);
            w.Write((byte)reason);
            w.Write((byte)target);
            w.WritePackedUInt32((uint)(index + 1));
            w.Write(name ?? string.Empty);
            Send(user, w);
        }

        /// <summary>Chat line for every player with the mod, formatted by each of them in their own language.</summary>
        internal static void BroadcastNotice(NoticeType type, string actor, string owner, string recipient, Target target, int index,
                                             int permanent, int ethereal, int temporary)
        {
            var w = new NetworkWriter();
            w.Write((byte)Msg.Notice);
            w.Write((byte)type);
            w.Write(actor ?? string.Empty);
            w.Write(owner ?? string.Empty);
            w.Write(recipient ?? string.Empty);
            w.Write((byte)target);
            w.WritePackedUInt32((uint)(index + 1));
            w.WritePackedUInt32((uint)permanent);
            w.WritePackedUInt32((uint)ethereal);
            w.WritePackedUInt32((uint)temporary);
            foreach (NetworkUser u in modded) Send(u, w);
        }

        private static void Send(NetworkUser user, NetworkWriter w)
        {
            if (!user || user.connectionToClient == null) return;
            try
            {
                user.connectionToClient.Send(MsgType, new Payload { data = w.ToArray() });
            }
            catch (Exception e)
            {
                Log.Warn($"Can't send to {Exchange.NameOf(user)}: {e.Message}");
            }
        }
    }
}
