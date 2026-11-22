using System;
using System.Collections.Generic;
using System.Reflection;
using H1xl.Shared;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace JustDropItems
{
    /// <summary>
    /// Per player and item, how many copies carry a mark. A mark of the base game lives only on a pickup lying on the ground
    /// and is lost when someone picks it up; the host keeps it here instead, so dropped and given copies take their marks first
    /// and keep them. Marks beyond the stack size (copies used up by printers, scrappers or cauldrons) are dropped first.
    /// </summary>
    internal sealed class MarkLedger
    {
        private readonly Dictionary<CharacterMaster, MarkCounts<ItemIndex>> items = new Dictionary<CharacterMaster, MarkCounts<ItemIndex>>();
        private readonly Dictionary<CharacterMaster, HashSet<EquipmentIndex>> equipment = new Dictionary<CharacterMaster, HashSet<EquipmentIndex>>();

        internal void Reset()
        {
            items.Clear();
            equipment.Clear();
        }

        internal void Add(CharacterMaster master, ItemIndex item, int count)
        {
            if (!master || count <= 0 || item == ItemIndex.None) return;
            if (!items.TryGetValue(master, out var marks)) items[master] = marks = new MarkCounts<ItemIndex>();
            marks.Add(item, count);
        }

        // Every copy of the item, as StackModel.Total.
        private static readonly Func<Inventory, ItemIndex, int> stackOf = (inv, item) =>
            inv.GetItemCountPermanent(item) + inv.GetItemCountRented(item) + inv.GetItemCountTemp(item) + inv.GetItemCountChanneled(item);

        /// <summary>
        /// Lowers the marks to the stack size after the stack shrank (copies used up by printers, scrappers, cauldrons or replaced
        /// by a mutually exclusive item), so old marks never stick to fresh copies picked up later.
        /// </summary>
        internal void Clamp(CharacterMaster master, Inventory inv)
        {
            if (!master || !inv || !items.TryGetValue(master, out var marks)) return;
            marks.Clamp(inv, stackOf);
        }

        /// <summary>Takes up to <paramref name="amount"/> marks for copies leaving a stack of <paramref name="stackBefore"/> copies.</summary>
        internal int Take(CharacterMaster master, ItemIndex item, int stackBefore, int amount)
        {
            if (!master || !items.TryGetValue(master, out var marks)) return 0;
            return marks.Take(item, stackBefore, amount);
        }

        internal void Mark(CharacterMaster master, EquipmentIndex index)
        {
            if (!master || index == EquipmentIndex.None) return;
            if (!equipment.TryGetValue(master, out var set)) equipment[master] = set = new HashSet<EquipmentIndex>();
            set.Add(index);
        }

        internal bool Take(CharacterMaster master, EquipmentIndex index) =>
            master && equipment.TryGetValue(master, out var set) && set.Remove(index);
    }

    /// <summary>Marks the mod's own pickups (on the host only: clients spawn the vanilla prefab).</summary>
    internal sealed class DroppedPickup : MonoBehaviour { }

    /// <summary>
    /// The two marks the mod keeps for every copy:
    /// <para>Rerolled: the Recycler's (and the Drifter's Recycle) "already rerolled" mark, so an item can be rerolled only once.</para>
    /// <para>Printed: the copy came out of a printer (or another "duplicated" source). The Drifter's Tinker gives Scrap and a
    /// temporary copy for an ordinary item, but two temporary copies and no Scrap for a printed one. Without this mark a printed
    /// item that was picked up and dropped again would give Scrap, and Scrap → printer → drop → Tinker would be an endless loop
    /// of temporary copies. Dropped copies carry the game's "duplicated" flag only if they are printed.</para>
    /// <para>The game's "duplicated" flag also stops Substandard Duplicator from adding a temporary copy on pickup. A copy the
    /// mod dropped must never trigger it (or drop and pick up again would be an endless loop too), so while such a pickup is
    /// being picked up the host turns the flag on for that moment.</para>
    /// </summary>
    internal static class Recycling
    {
        private static readonly MarkLedger rerolled = new MarkLedger(), printed = new MarkLedger();

        private static readonly FieldInfo consumedField = GameMembers.GenericPickupController_consumed.Find();

        internal static void Reset()
        {
            rerolled.Reset();
            printed.Reset();
        }

        /// <summary><c>Inventory.onInventoryChangedGlobal</c>: marks never outnumber the copies left (host only).</summary>
        internal static void OnInventoryChanged(Inventory inv)
        {
            if (!NetworkServer.active || !inv || !inv.TryGetComponent(out CharacterMaster master)) return;
            rerolled.Clamp(master, inv);
            printed.Clamp(master, inv);
        }

        /// <summary>Hook on <c>GenericPickupController.AttemptGrant</c>: records the marks of a picked-up pickup.</summary>
        internal static void OnAttemptGrant(Action<GenericPickupController, CharacterBody> orig, GenericPickupController self, CharacterBody body)
        {
            if (!NetworkServer.active || !self)
            {
                orig(self, body);
                return;
            }
            bool wasRecycled = self.Recycled && Settings.recycler.Value == RecyclerMode.OncePerItem;
            bool wasPrinted = self.Duplicated;
            bool ours = self.GetComponent<DroppedPickup>();
            UniquePickup pickup = self.pickup;

            if (ours && !wasPrinted) self.NetworkDuplicated = true; // no Substandard Duplicator copy for the mod's own drops
            try
            {
                orig(self, body);
            }
            finally
            {
                if (ours && !wasPrinted && self && !Consumed(self)) self.NetworkDuplicated = false;
            }

            if (body && body.master && self && !Consumed(self) && self.pickup.pickupIndex != pickup.pickupIndex)
            {
                OnEquipmentSwapped(self, body.master, pickup, wasRecycled);
                return;
            }
            if ((!wasRecycled && !wasPrinted) || !body || !body.master || !Consumed(self)) return;
            try
            {
                PickupDef def = PickupCatalog.GetPickupDef(pickup.pickupIndex);
                if (def == null) return;
                if (def.itemIndex != ItemIndex.None)
                {
                    if (wasRecycled) rerolled.Add(body.master, def.itemIndex, 1);
                    if (wasPrinted) printed.Add(body.master, def.itemIndex, 1);
                }
                else if (def.equipmentIndex != EquipmentIndex.None && wasRecycled) rerolled.Mark(body.master, def.equipmentIndex);
                if (Log.Verbose)
                    Log.Info($"Marks: {def.internalName} -> {Exchange.NameOf(body.master)}" +
                             (wasRecycled ? " (rerolled)" : "") + (wasPrinted ? " (printed)" : ""));
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Equipment picked up by swapping: the picked equipment went into the slot and the same pickup now shows the old one,
        /// still with the picked one's flags. The picked equipment keeps its mark; the pickup gets the old equipment's mark.
        /// </summary>
        private static void OnEquipmentSwapped(GenericPickupController self, CharacterMaster master, UniquePickup picked, bool wasRecycled)
        {
            try
            {
                EquipmentIndex taken = PickupCatalog.GetPickupDef(picked.pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
                EquipmentIndex old = PickupCatalog.GetPickupDef(self.pickup.pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
                if (taken == EquipmentIndex.None || old == EquipmentIndex.None) return;
                if (Settings.recycler.Value != RecyclerMode.OncePerItem) return;
                bool oldMarked = rerolled.Take(master, old);
                if (wasRecycled) rerolled.Mark(master, taken);
                self.NetworkRecycled = oldMarked;
                if (Log.Verbose)
                    Log.Info($"Marks: swapped equipment for {Exchange.NameOf(master)}" + (wasRecycled ? " (picked one rerolled)" : "") +
                             (oldMarked ? " (dropped one rerolled)" : ""));
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private static bool Consumed(GenericPickupController c) => consumedField == null || !c || (bool)consumedField.GetValue(c);

        /// <summary>For other mods (AutoLootDistribution): an item given to a player straight from a pickup's data.</summary>
        internal static void OnGranted(CharacterMaster master, ItemIndex item, bool wasRecycled, bool wasPrinted)
        {
            if (!NetworkServer.active || !master) return;
            if (wasRecycled && Settings.recycler.Value == RecyclerMode.OncePerItem) rerolled.Add(master, item, 1);
            if (wasPrinted) printed.Add(master, item, 1);
        }

        // ---------------------------------------------------------------- rerolled

        internal static void AddItemMarks(CharacterMaster master, ItemIndex item, int count) => rerolled.Add(master, item, count);

        internal static int TakeItemMarks(CharacterMaster master, ItemIndex item, int stackBefore, int amount) =>
            rerolled.Take(master, item, stackBefore, amount);

        internal static void MarkEquipment(CharacterMaster master, EquipmentIndex equipment) => rerolled.Mark(master, equipment);

        internal static bool TakeEquipmentMark(CharacterMaster master, EquipmentIndex equipment) => rerolled.Take(master, equipment);

        /// <summary>The "rerolled" flag for a dropped copy: forced on with <see cref="RecyclerMode.Never"/>, off with Unlimited.</summary>
        internal static bool FlagFor(bool marked)
        {
            switch (Settings.recycler.Value)
            {
                case RecyclerMode.Never: return true;
                case RecyclerMode.Unlimited: return false;
                default: return marked;
            }
        }

        // ---------------------------------------------------------------- printed

        internal static void AddPrinted(CharacterMaster master, ItemIndex item, int count) => printed.Add(master, item, count);

        internal static int TakePrinted(CharacterMaster master, ItemIndex item, int stackBefore, int amount) =>
            printed.Take(master, item, stackBefore, amount);
    }
}
