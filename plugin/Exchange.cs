using System;
using System.Collections.Generic;
using H1xl.Shared;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

// Inventory.SetEquipmentIndex(index, bool) is marked "for mod compat" but is the call that picks the right equipment set.
#pragma warning disable CS0618

namespace JustDropItems
{
    /// <summary>Host side: checks a request, takes the copies out of the inventory and drops or sends them.</summary>
    internal static class Exchange
    {
        private const float MinRequestInterval = 0.08f;
        private const int DropletsPerTick = 3;

        private static Rules rules;
        internal static Rules CurrentRules => rules ??= Rules.FromConfig();

        private static GameObject pickupPrefab;
        private static readonly Dictionary<NetworkUser, float> lastRequest = new Dictionary<NetworkUser, float>();

        /// <summary>A copy waiting to be dropped; large drops are spread over several physics ticks.</summary>
        private struct PendingDroplet
        {
            public GenericPickupController.CreatePickupInfo info;
            public Vector3 position, velocity;
            public CharacterMaster owner;
        }

        private static readonly Queue<PendingDroplet> pending = new Queue<PendingDroplet>();

        /// <summary>
        /// Equipment that couldn't go back to its owner at stage end (their slot was filled): dropped at the owner's feet on the
        /// next stage, once their body is out of the drop pod. A droplet made before the scene change would vanish with the scene.
        /// </summary>
        private static readonly List<PendingDroplet> carried = new List<PendingDroplet>();
        private static Stage carriedFrom;

        internal static void Init()
        {
            Settings.RulesChanged += () => rules = null;
        }

        /// <summary>
        /// Our own copy of the vanilla pickup prefab. It looks and works the same (clients spawn the vanilla prefab: the network
        /// asset id is unchanged), and AutoLootDistribution never takes a drop that uses a prefab override.
        /// </summary>
        internal static void CreatePickupPrefab()
        {
            GameObject vanilla = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/GenericPickup");
            if (!vanilla)
            {
                Log.Error("GenericPickup prefab not found: dropped items will use the vanilla prefab.");
                return;
            }
            var holder = new GameObject("JustDropItemsPrefabs");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.SetActive(false);
            pickupPrefab = UnityEngine.Object.Instantiate(vanilla, holder.transform);
            pickupPrefab.name = "JustDropItemsPickup";
            pickupPrefab.AddComponent<DroppedPickup>(); // lets the host recognise its own drops (see Recycling)
        }

        internal static void Reset()
        {
            pending.Clear();
            carried.Clear();
            lastRequest.Clear();
            Recycling.Reset();
        }

        // ---------------------------------------------------------------- checks

        private static bool TooFast(NetworkUser sender)
        {
            float now = Time.unscaledTime;
            if (lastRequest.TryGetValue(sender, out float last) && now - last < MinRequestInterval) return true;
            lastRequest[sender] = now;
            return false;
        }

        private static bool IsPlayer(CharacterMaster m) => m && m.inventory && m.playerCharacterMasterController;

        private static bool IsAlive(CharacterMaster m)
        {
            CharacterBody body = m ? m.GetBody() : null;
            return body && body.healthComponent && body.healthComponent.alive;
        }

        private static Tokens.Reject CheckOwner(NetworkUser sender, CharacterMaster owner)
        {
            if (!IsPlayer(owner)) return Tokens.Reject.NothingThere;
            if (sender.master == owner) return Tokens.Reject.None;
            switch (CurrentRules.takeFrom)
            {
                case TakeFromMode.Everyone: return Tokens.Reject.None;
                case TakeFromMode.HostAll: return sender.isLocalPlayer ? Tokens.Reject.None : Tokens.Reject.NotYours;
                default: return Tokens.Reject.NotYours;
            }
        }

        /// <summary>
        /// A player's name for chat lines and the log. Some game copies report an empty name: then the survivor's English name
        /// is used (English, since the name is sent to every player as it is).
        /// </summary>
        internal static string NameOf(CharacterMaster m)
        {
            if (!m || !m.playerCharacterMasterController) return string.Empty;
            string name = m.playerCharacterMasterController.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            CharacterBody body = m.bodyPrefab ? m.bodyPrefab.GetComponent<CharacterBody>() : null;
            string survivor = body ? Language.GetString(body.baseNameToken, "en") : null;
            return string.IsNullOrWhiteSpace(survivor) || survivor == body.baseNameToken ? "Player" : survivor;
        }

        /// <summary>
        /// A player's name for chat lines sent to other players. A player without a name is sent as a marker with their
        /// survivor, which each player turns into the survivor's name in their own language (see Notices).
        /// </summary>
        internal static string NetName(CharacterMaster m)
        {
            if (!m || !m.playerCharacterMasterController) return string.Empty;
            string name = m.playerCharacterMasterController.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            CharacterBody body = m.bodyPrefab ? m.bodyPrefab.GetComponent<CharacterBody>() : null;
            return body ? Notices.SurvivorMarker + body.baseNameToken : "Player";
        }

        internal static string NameOf(NetworkUser user)
        {
            if (!user) return string.Empty;
            string name = NameOf(user.master);
            return name.Length > 0 ? name : string.IsNullOrWhiteSpace(user.userName) ? "Player" : user.userName;
        }

        /// <summary>The copies of an item that can move, after all item checks; false (and a refusal sent) if none.</summary>
        private static bool CheckStack(NetworkUser sender, CharacterMaster owner, int index, int requested, bool drop, KindOrder order, out ItemIndex item,
                                       out StackModel model, out int amount)
        {
            item = (ItemIndex)index;
            model = default;
            amount = 0;
            if (!ItemCatalog.IsIndexValid(in item))
            {
                Net.SendReject(sender, Tokens.Reject.NothingThere);
                return false;
            }
            Tokens.Reject reason = CurrentRules.CheckItem(item, drop);
            if (reason != Tokens.Reject.None)
            {
                Net.SendReject(sender, reason, Net.Target.Item, index);
                return false;
            }
            model = StackModel.Of(owner.inventory, item, CurrentRules, order);
            if (model.Total == 0)
            {
                Net.SendReject(sender, Tokens.Reject.NothingThere, Net.Target.Item, index);
                return false;
            }
            int movable = model.Movable;
            if (movable == 0)
            {
                bool onlyShared = model.shared > 0 && model.permanent + model.ethereal + model.temporary == 0;
                Net.SendReject(sender, onlyShared ? Tokens.Reject.SharedOnly : Tokens.Reject.NoCopies, Net.Target.Item, index);
                return false;
            }
            // Never trust the client's amount: clamp it to what the stack has and the host allows.
            amount = Amount.ClampRequest(requested, movable, CurrentRules.maxPerAction);
            return true;
        }

        /// <summary>
        /// Takes the copies out of the inventory. Temporary copies keep their decay. The game keeps one decay timer per item
        /// (copies = Ceil(raw value)), so whole copies go first and the partly decayed one only with the last copy: a part copy
        /// added to a recipient's own part copy would merge into one copy.
        /// </summary>
        private static void TakeCopies(Inventory inv, ItemIndex item, int p, int e, int t, List<float> tempValues)
        {
            if (p > 0) inv.RemoveItemPermanent(item, p);
            if (e > 0) inv.RemoveItemRented(item, e);
            if (t <= 0) return;
            float raw = inv.GetTempItemRawValue(item);
            float sum = 0f;
            for (int i = 0; i < t; i++)
            {
                float v = Mathf.Min(1f, raw - sum);
                if (v <= 0f) break;
                tempValues.Add(v);
                sum += v;
            }
            if (sum > 0f) inv.RemoveItemTemp(item, sum);
        }

        // ---------------------------------------------------------------- drop

        internal static void Drop(NetworkUser sender, CharacterMaster owner, Net.Target target, int index, int requested,
                                  KindOrder order = KindOrder.PermanentEtherealTemporary)
        {
            bool alternate = target == Net.Target.AltEquipment;
            if (alternate) target = Net.Target.Equipment;
            if (TooFast(sender)) { Net.SendReject(sender, Tokens.Reject.TooFast); return; }
            if (!CurrentRules.allowDrop) { Net.SendReject(sender, Tokens.Reject.DropDisabled); return; }
            Tokens.Reject reason = CheckOwner(sender, owner);
            if (reason != Tokens.Reject.None) { Net.SendReject(sender, reason); return; }

            // Dropped in front of the owner, or of the sender when they drop someone else's items while that player is dead.
            CharacterBody body = IsAlive(owner) ? owner.GetBody() : sender.master && IsAlive(sender.master) ? sender.master.GetBody() : null;
            if (!body) { Net.SendReject(sender, Tokens.Reject.NeedsBody); return; }

            Vector3 origin = body.corePosition;
            Vector3 aim = body.inputBank ? body.inputBank.aimDirection : body.transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.01f) aim = body.transform.forward;
            aim.Normalize();

            if (target == Net.Target.Equipment)
            {
                DropEquipment(sender, owner, index, origin, aim, alternate);
                return;
            }
            if (!CheckStack(sender, owner, index, requested, true, order, out ItemIndex item, out StackModel model, out int amount)) return;

            model.Split(amount, out int p, out int e, out int t);
            int marks = Recycling.TakeItemMarks(owner, item, model.Total, amount);
            int printedMarks = Recycling.TakePrinted(owner, item, model.Total, amount);
            var temps = new List<float>(t);
            TakeCopies(owner.inventory, item, p, e, t, temps);
            t = temps.Count;

            int n = QueueCopies(owner, item, p, e, temps, marks, printedMarks, origin, aim);

            if (Log.Verbose)
                Log.Info($"{NameOf(sender)} dropped {n} {ItemCatalog.GetItemDef(item).name} from {NameOf(owner)} " +
                         $"(permanent {p}, ethereal {e}, temporary {t}, recycler marks {marks}, printed {printedMarks})");
            if (Settings.announceDrops.Value)
                Net.BroadcastNotice(Net.NoticeType.Dropped, NetName(sender.master), NetName(owner), string.Empty, Net.Target.Item, index, p, e, t);
        }

        /// <summary>Queues the copies as droplets fanned out along <paramref name="aim"/>; the marked copies first. Returns the count.</summary>
        private static int QueueCopies(CharacterMaster owner, ItemIndex item, int p, int e, List<float> temps, int marks, int printedMarks,
                                       Vector3 origin, Vector3 aim)
        {
            PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(item);
            int n = p + e + temps.Count;
            for (int i = 0; i < n; i++)
            {
                var pickup = new UniquePickup(pickupIndex) { ignoreArtifactBasedPickupReplacements = true };
                if (i >= p && i < p + e) pickup.isRental = true;
                else if (i >= p + e) pickup.decayValue = temps[i - p - e];
                Queue(owner, pickup, Recycling.FlagFor(i < marks), i < printedMarks, origin, Fan(aim, i, n));
            }
            return n;
        }

        private static void DropEquipment(NetworkUser sender, CharacterMaster owner, int index, Vector3 origin, Vector3 aim, bool alternate)
        {
            var equipment = (EquipmentIndex)index;
            Tokens.Reject reason = CurrentRules.CheckEquipment(equipment);
            if (reason != Tokens.Reject.None) { Net.SendReject(sender, reason, Net.Target.Equipment, index); return; }
            if (!TakeEquipment(owner.inventory, equipment, alternate)) { Net.SendReject(sender, Tokens.Reject.NothingThere, Net.Target.Equipment, index); return; }

            bool marked = Recycling.TakeEquipmentMark(owner, equipment);
            var pickup = new UniquePickup(PickupCatalog.FindPickupIndex(equipment)) { ignoreArtifactBasedPickupReplacements = true };
            Queue(owner, pickup, Recycling.FlagFor(marked), false, origin, Fan(aim, 0, 1));

            if (Log.Verbose) Log.Info($"{NameOf(sender)} dropped equipment {EquipmentCatalog.GetEquipmentDef(equipment).name} from {NameOf(owner)}");
            if (Settings.announceDrops.Value)
                Net.BroadcastNotice(Net.NoticeType.Dropped, NetName(sender.master), NetName(owner), string.Empty, Net.Target.Equipment, index, 1, 0, 0);
        }

        /// <summary>
        /// Removes the equipment: from the slot the used icon shows (the second icon shows MUL-T's other slot, or the other
        /// set), else from whichever slot holds it.
        /// </summary>
        private static bool TakeEquipment(Inventory inv, EquipmentIndex equipment, bool alternate)
        {
            if (equipment == EquipmentIndex.None) return false;
            if (alternate)
            {
                // The same choice the second equipment icon makes (EquipmentIcon.displayAlternateEquipment).
                EquipmentLocation next = inv.accessibleEquipmentSetCount > 1 ? inv.nextSetEquipmentLocation : inv.nextSlotEquipmentLocation;
                if (inv.IsEquipmentLocationInBounds(in next) && inv.GetEquipmentIndex(in next) == equipment)
                {
                    inv.SetEquipmentIndex(EquipmentIndex.None, in next);
                    return true;
                }
            }
            if (inv.currentEquipmentIndex == equipment)
            {
                inv.SetEquipmentIndex(EquipmentIndex.None, true);
                return true;
            }
            for (uint slot = 0; slot < inv.equipmentSlotCount; slot++)
                for (uint set = 0; set < inv.equipmentSetCount; set++)
                {
                    var location = new EquipmentLocation { slot = slot, set = set };
                    if (inv.GetEquipmentIndex(in location) != equipment) continue;
                    inv.SetEquipmentIndex(EquipmentIndex.None, in location);
                    return true;
                }
            return false;
        }

        /// <summary>The DropItem arc: 8 m/s forward and 15 m/s up lands the item a few metres away, fanned out for many items.</summary>
        private static Vector3 Fan(Vector3 aim, int i, int n)
        {
            float spread = n <= 1 ? 0f : Mathf.Min(90f, 12f * (n - 1));
            float angle = n <= 1 ? 0f : -spread / 2f + spread * i / (n - 1);
            return Quaternion.AngleAxis(angle, Vector3.up) * aim * 8f + Vector3.up * 15f;
        }

        private static void Queue(CharacterMaster owner, UniquePickup pickup, bool recycled, bool printed, Vector3 position, Vector3 velocity)
        {
            var info = new GenericPickupController.CreatePickupInfo
            {
                position = position,
                rotation = Quaternion.identity,
                pickup = pickup,
                prefabOverride = pickupPrefab,
                recycled = recycled,
                // Only a printed copy keeps the printer's flag (the Drifter's Tinker gives no Scrap for it). Substandard
                // Duplicator ignores every copy the mod dropped anyway: see Recycling.OnAttemptGrant.
                duplicated = printed,
            };
            pending.Enqueue(new PendingDroplet { info = info, position = position, velocity = velocity, owner = owner });
        }

        /// <summary>Called every physics tick on the host.</summary>
        internal static void Tick()
        {
            for (int i = 0; i < DropletsPerTick && pending.Count > 0; i++)
            {
                PendingDroplet d = pending.Dequeue();
                try { PickupDropletController.CreatePickupDroplet(d.info, d.position, d.velocity); }
                catch (Exception e) { Log.Error(e); }
            }
            if (carried.Count == 0 || !Stage.instance || Stage.instance == carriedFrom) return;
            for (int i = carried.Count - 1; i >= 0; i--)
            {
                PendingDroplet d = carried[i];
                if (!d.owner)
                {
                    carried.RemoveAt(i);
                    continue;
                }
                if (!IsAlive(d.owner)) continue;
                CharacterBody body = d.owner.GetBody();
                if (body.currentVehicle) continue; // still in the drop pod
                carried.RemoveAt(i);
                d.position = d.info.position = ItemFlight.DropPoint(body, body.footPosition);
                d.velocity = Vector3.up * 5f;
                pending.Enqueue(d);
            }
        }

        // ---------------------------------------------------------------- give

        internal static void Give(NetworkUser sender, CharacterMaster owner, Net.Target target, int index, int requested, CharacterMaster recipient,
                                  KindOrder order = KindOrder.PermanentEtherealTemporary)
        {
            bool alternate = target == Net.Target.AltEquipment;
            if (alternate) target = Net.Target.Equipment;
            if (TooFast(sender)) { Net.SendReject(sender, Tokens.Reject.TooFast); return; }
            if (!CurrentRules.allowGive) { Net.SendReject(sender, Tokens.Reject.GiveDisabled); return; }
            Tokens.Reject reason = CheckOwner(sender, owner);
            if (reason != Tokens.Reject.None) { Net.SendReject(sender, reason); return; }
            if (!IsPlayer(recipient) || !recipient.playerCharacterMasterController.isConnected) { Net.SendReject(sender, Tokens.Reject.NoTarget); return; }
            if (recipient == owner) { Net.SendReject(sender, Tokens.Reject.TargetSelf); return; }
            if (!IsAlive(recipient) && !CurrentRules.allowGiveToDead) { Net.SendReject(sender, Tokens.Reject.TargetDead); return; }
            bool ownerAlive = IsAlive(owner);
            if (!ownerAlive && !CurrentRules.allowGiveWhileDead) { Net.SendReject(sender, Tokens.Reject.DeadGiver); return; }

            Vector3 origin = ownerAlive ? owner.GetBody().corePosition : owner.deathFootPosition;

            if (target == Net.Target.Equipment)
            {
                GiveEquipment(sender, owner, index, recipient, origin, alternate);
                return;
            }
            if (!CheckStack(sender, owner, index, requested, false, order, out ItemIndex item, out StackModel model, out int amount)) return;

            model.Split(amount, out int p, out int e, out int t);
            int marks = Recycling.TakeItemMarks(owner, item, model.Total, amount);
            int printedMarks = Recycling.TakePrinted(owner, item, model.Total, amount);
            var temps = new List<float>(t);
            TakeCopies(owner.inventory, item, p, e, t, temps);
            t = temps.Count;
            float tempSum = 0f;
            foreach (float v in temps) tempSum += v;
            Vector3 seen = LastSeen(recipient, origin);

            Launch(origin, recipient, PickupCatalog.FindPickupIndex(item), item, _ =>
            {
                if (Left(recipient))
                {
                    // The recipient left while the item was flying: it falls where they were last seen, marks and all
                    // (at stage end Flush gives the droplets back to the owner).
                    QueueCopies(owner, item, p, e, temps, marks, printedMarks, LastSeen(recipient, seen), Vector3.forward);
                    return;
                }
                Grant(recipient, item, p, e, tempSum, temps.Count, Settings.notify.Value);
                Recycling.AddItemMarks(recipient, item, marks);
                Recycling.AddPrinted(recipient, item, printedMarks);
            });

            if (Log.Verbose)
                Log.Info($"{NameOf(sender)} gave {p + e + t} {ItemCatalog.GetItemDef(item).name} from {NameOf(owner)} to {NameOf(recipient)} " +
                         $"(permanent {p}, ethereal {e}, temporary {t}, recycler marks {marks}, printed {printedMarks})");
            if (Settings.announceGives.Value)
                Net.BroadcastNotice(Net.NoticeType.Gave, NetName(sender.master), NetName(owner), NetName(recipient), Net.Target.Item, index, p, e, t);
        }

        /// <summary>The player left the game (their master stays, but nobody plays it any more).</summary>
        private static bool Left(CharacterMaster m) =>
            !m || !m.inventory || !m.playerCharacterMasterController || !m.playerCharacterMasterController.isConnected;

        /// <summary>Just above the player's feet, or <paramref name="fallback"/> without a body.</summary>
        private static Vector3 LastSeen(CharacterMaster m, Vector3 fallback)
        {
            CharacterBody body = m ? m.GetBody() : null;
            return ItemFlight.DropPoint(body, fallback);
        }

        private static void Grant(CharacterMaster to, ItemIndex item, int p, int e, float temp, int tempCopies, bool notify)
        {
            // The vanilla arrival of an item orb grants every kind.
            ItemTransferOrb.DefaultOnArrivalBehavior(new ItemTransferOrb
            {
                inventoryToGrantTo = to.inventory,
                itemIndex = item,
                stack = p,
                rentalStack = e,
                tempStack = temp,
                isPickup = false,
            });
            if (!notify) return;
            // Its pickup effects and message, as in DefaultOnArrivalBehavior, but counting copies: the game shows
            // (int)tempStack, which loses a partly decayed copy (0.5 + 1 showed 1).
            bool isTemp = tempCopies > p && tempCopies > e;
            CharacterBody body = to.GetBody();
            if (body) body.OnPickup(isTemp ? CharacterBody.PickupClass.TempItem : CharacterBody.PickupClass.Item);
            GenericPickupController.SendPickupMessage(to, new UniquePickup(PickupCatalog.FindPickupIndex(item))
            {
                decayValue = isTemp ? 1f : 0f,
                isRental = e > p && e > tempCopies,
            }, p + e + tempCopies);
        }

        private static void GiveEquipment(NetworkUser sender, CharacterMaster owner, int index, CharacterMaster recipient, Vector3 origin, bool alternate)
        {
            var equipment = (EquipmentIndex)index;
            Tokens.Reject reason = CurrentRules.CheckEquipment(equipment);
            if (reason != Tokens.Reject.None) { Net.SendReject(sender, reason, Net.Target.Equipment, index); return; }
            if (recipient.inventory.currentEquipmentIndex != EquipmentIndex.None)
            {
                Net.SendReject(sender, Tokens.Reject.TargetEquipmentFull, Net.Target.Equipment, index, NetName(recipient));
                return;
            }
            if (!TakeEquipment(owner.inventory, equipment, alternate)) { Net.SendReject(sender, Tokens.Reject.NothingThere, Net.Target.Equipment, index); return; }
            bool marked = Recycling.TakeEquipmentMark(owner, equipment);
            PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(equipment);
            Vector3 seen = LastSeen(recipient, origin);

            Launch(origin, recipient, pickupIndex, ItemIndex.None, _ =>
            {
                if (Left(recipient))
                {
                    // The recipient left while it was flying: it falls where they were last seen (at stage end Flush gives it back).
                    Queue(owner, new UniquePickup(pickupIndex) { ignoreArtifactBasedPickupReplacements = true }, Recycling.FlagFor(marked), false,
                          LastSeen(recipient, seen), Vector3.up * 5f);
                    return;
                }
                CharacterMaster to = recipient;
                if (to.inventory.currentEquipmentIndex == EquipmentIndex.None)
                {
                    to.inventory.SetEquipmentIndex(equipment, false);
                    if (marked) Recycling.MarkEquipment(to, equipment);
                    if (Settings.notify.Value) GenericPickupController.SendPickupMessage(to, new UniquePickup(pickupIndex));
                    return;
                }
                // The slot was filled while the equipment flew: drop it at the recipient's feet instead.
                CharacterBody body = to.GetBody();
                Vector3 at = ItemFlight.DropPoint(body, origin);
                Queue(to, new UniquePickup(pickupIndex) { ignoreArtifactBasedPickupReplacements = true }, Recycling.FlagFor(marked), false, at, Vector3.up * 5f);
            });

            if (Log.Verbose)
                Log.Info($"{NameOf(sender)} gave equipment {EquipmentCatalog.GetEquipmentDef(equipment).name} from {NameOf(owner)} to {NameOf(recipient)}");
            if (Settings.announceGives.Value)
                Net.BroadcastNotice(Net.NoticeType.Gave, NetName(sender.master), NetName(owner), NetName(recipient), Net.Target.Equipment, index, 1, 0, 0);
        }

        // ---------------------------------------------------------------- flight

        /// <summary>
        /// The flying icon: the game's item orb (the one Mithrix, Hex of Greed and AutoLootDistribution use); equipment flies
        /// with the pickup orb's effect, which can draw any pickup's icon. The orb grants nothing itself: on arrival (or at
        /// stage end) <paramref name="deliver"/> gives the copies.
        /// </summary>
        private static void Launch(Vector3 origin, CharacterMaster recipient, PickupIndex pickup, ItemIndex item, Action<bool> deliver)
        {
            var orb = new ItemFlightOrb
            {
                origin = origin,
                itemIndex = item,
                stack = 0, // the game's arrival never runs (OnArrival is ours); nothing is granted through the orb itself
            };
            if (item == ItemIndex.None) orb.iconPickup = pickup;
            ItemFlight.Launch(orb, recipient, Settings.travelTime.Value, Settings.animation.Value, deliver);
        }

        /// <summary>
        /// Stage end: every item still flying arrives at once, and copies not dropped yet go back to their owner, so nothing is lost
        /// with the old scene.
        /// </summary>
        internal static void Flush()
        {
            ItemFlight.FlushAll();
            while (pending.Count > 0)
            {
                PendingDroplet d = pending.Dequeue();
                if (!d.owner || !d.owner.inventory) continue;
                PickupDef def = PickupCatalog.GetPickupDef(d.info.pickup.pickupIndex);
                if (def == null) continue;
                if (def.itemIndex != ItemIndex.None)
                {
                    UniquePickup u = d.info.pickup;
                    if (u.isRental) d.owner.inventory.GiveItemRented(def.itemIndex, 1);
                    else if (u.isTempItem) d.owner.inventory.GiveItemTemp(def.itemIndex, u.decayValue);
                    else d.owner.inventory.GiveItemPermanent(def.itemIndex, 1);
                    if (d.info.recycled && Settings.recycler.Value == RecyclerMode.OncePerItem) Recycling.AddItemMarks(d.owner, def.itemIndex, 1);
                    if (d.info.duplicated) Recycling.AddPrinted(d.owner, def.itemIndex, 1);
                }
                else if (def.equipmentIndex != EquipmentIndex.None)
                {
                    if (d.owner.inventory.currentEquipmentIndex == EquipmentIndex.None)
                    {
                        d.owner.inventory.SetEquipmentIndex(def.equipmentIndex, false);
                        if (d.info.recycled && Settings.recycler.Value == RecyclerMode.OncePerItem) Recycling.MarkEquipment(d.owner, def.equipmentIndex);
                    }
                    else
                    {
                        // The slot is filled: never lose it, drop it at the owner's feet on the next stage (see Tick).
                        carried.Add(d);
                        carriedFrom = Stage.instance;
                    }
                }
            }
        }
    }
}
