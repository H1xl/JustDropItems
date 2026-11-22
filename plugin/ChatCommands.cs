using System;
using System.Collections.Generic;
using System.Globalization;
using H1xl.Shared;
using RoR2;
using UnityEngine.Networking;

namespace JustDropItems
{
    /// <summary>
    /// /drop and /give typed in chat (off by default). They work for players without the mod too, since the host reads the chat.
    /// <code>/drop {item} [amount]          /give {item} {player} [amount]</code>
    /// Names are typed without spaces and may be partial; "equip" means the equipment.
    /// </summary>
    internal static class ChatCommands
    {
        /// <summary>Hook on the chat's "say" command: the message is shown as usual, then read as a command.</summary>
        internal static void OnSay(Action<ConCommandArgs> orig, ConCommandArgs args)
        {
            orig(args);
            if (!NetworkServer.active || !Settings.chatCommands.Value || !args.sender || args.userArgs.Count < 1) return;
            string text = args.userArgs[0]?.Trim();
            if (string.IsNullOrEmpty(text) || text[0] != '/') return;
            string[] words = text.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return;
            string cmd = words[0].ToLowerInvariant();
            try
            {
                Net.ChatContext = true;
                if (cmd == "drop" || cmd == "d") Drop(args.sender, words);
                else if (cmd == "give" || cmd == "g") Give(args.sender, words);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                Net.ChatContext = false;
            }
        }

        private static void Drop(NetworkUser sender, string[] w)
        {
            if (w.Length < 2) { Help(sender); return; }
            int amount = w.Length >= 3 && int.TryParse(w[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) ? a : 1;
            CharacterMaster me = sender.master;
            if (!me || !me.inventory) return;
            if (IsEquipmentWord(w[1]))
            {
                Exchange.Drop(sender, me, Net.Target.Equipment, (int)me.inventory.currentEquipmentIndex, 1);
                return;
            }
            ItemIndex item = FindItem(me.inventory, w[1]);
            if (item == ItemIndex.None) { Net.SendReject(sender, Tokens.Reject.NothingThere); return; }
            Exchange.Drop(sender, me, Net.Target.Item, (int)item, amount);
        }

        private static void Give(NetworkUser sender, string[] w)
        {
            if (w.Length < 3) { Help(sender); return; }
            int amount = w.Length >= 4 && int.TryParse(w[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) ? a : 1;
            CharacterMaster me = sender.master;
            if (!me || !me.inventory) return;
            CharacterMaster to = FindPlayer(w[2]);
            if (!to) { Net.SendReject(sender, Tokens.Reject.NoTarget); return; }
            if (IsEquipmentWord(w[1]))
            {
                Exchange.Give(sender, me, Net.Target.Equipment, (int)me.inventory.currentEquipmentIndex, 1, to);
                return;
            }
            ItemIndex item = FindItem(me.inventory, w[1]);
            if (item == ItemIndex.None) { Net.SendReject(sender, Tokens.Reject.NothingThere); return; }
            Exchange.Give(sender, me, Net.Target.Item, (int)item, amount, to);
        }

        private static void Help(NetworkUser sender) =>
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = "<color=#AAE6F0>/drop {item} [amount]   /give {item} {player} [amount]   (\"equip\" = equipment)</color>",
            });

        private static bool IsEquipmentWord(string s)
        {
            s = s.ToLowerInvariant();
            return s == "e" || s == "equip" || s == "equipment";
        }

        /// <summary>
        /// An item in the inventory whose name (the host's language, English or internal) matches: an exact match first,
        /// then the start of a name, then any part; the most recently picked up item wins a tie.
        /// </summary>
        private static ItemIndex FindItem(Inventory inv, string query)
        {
            string q = Normalize(query);
            if (q.Length == 0) return ItemIndex.None;
            List<ItemIndex> order = inv.itemAcquisitionOrder;
            for (int pass = 0; pass < 3; pass++)
                for (int i = order.Count - 1; i >= 0; i--)
                {
                    ItemDef def = ItemCatalog.GetItemDef(order[i]);
                    if (!def || inv.GetItemCountEffective(def.itemIndex) <= 0) continue;
                    foreach (string name in new[] { Language.GetString(def.nameToken), Language.GetString(def.nameToken, "en"), def.name })
                    {
                        string n = Normalize(name);
                        if (pass == 0 ? n == q : pass == 1 ? n.StartsWith(q) : n.Contains(q)) return def.itemIndex;
                    }
                }
            return ItemIndex.None;
        }

        private static CharacterMaster FindPlayer(string query)
        {
            string q = Normalize(query);
            if (q.Length == 0) return null;
            foreach (bool exact in new[] { true, false })
                foreach (PlayerCharacterMasterController pc in PlayerCharacterMasterController.instances)
                {
                    if (!pc || !pc.master) continue;
                    string n = Normalize(pc.GetDisplayName());
                    if (exact ? n == q : n.Contains(q)) return pc.master;
                }
            return null;
        }

        private static string Normalize(string s) => Settings.Normalize(s);
    }
}
