using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace H1xl.Shared
{
    /// <summary>
    /// A flying item icon, the game's item orb (Mithrix's item steal, Hex of Greed). It grants nothing itself: on arrival,
    /// or at once at stage end (<see cref="ItemFlight.FlushAll"/>), it calls the mod's delivery once.
    /// Made by <see cref="ItemFlight.Launch"/>.
    /// </summary>
    internal sealed class ItemFlightOrb : ItemTransferOrb
    {
        /// <summary>
        /// When valid, the icon flies with the pickup orb's effect, which draws any pickup (equipment, a stand-in icon);
        /// otherwise the game's item orb draws <see cref="ItemTransferOrb.itemIndex"/>.
        /// </summary>
        internal PickupIndex iconPickup = PickupIndex.none;
        /// <summary>Edits the pickup-orb effect before it is sent (e.g. a marker clients with the mod read).</summary>
        internal Action<EffectData> decorate = null;
        /// <summary>The mod's delivery; the argument is true when the stage is ending and the scene is about to unload.</summary>
        internal Action<bool> deliver;
        internal bool done;

        public override void Begin()
        {
            GameObject prefab = iconPickup.isValid ? ItemFlight.PickupOrbEffect : null;
            if (!prefab)
            {
                // The item orb's effect needs an item: without one (equipment, effect not found) fly without a visual.
                if (itemIndex != ItemIndex.None) base.Begin();
                else duration = travelDuration;
                return;
            }

            // Same as PickupTransferOrb.Begin: its effect draws any pickup's icon (items and equipment) on every client.
            duration = travelDuration;
            if (!target && !orbEffectTargetObjectOverride) return;
            var data = new EffectData
            {
                origin = origin,
                genericFloat = duration,
                genericUInt = Util.IntToUintPlusOne(iconPickup.value),
            };
            if (orbEffectTargetObjectOverride) data.SetNetworkedObjectReference(orbEffectTargetObjectOverride.gameObject);
            else data.SetHurtBoxReference(target);
            decorate?.Invoke(data);
            EffectManager.SpawnEffect(prefab, data, transmit: true);
        }

        public override void OnArrival() => ItemFlight.Arrive(this, stageEnding: false);
    }

    /// <summary>
    /// Server-side item flights: <see cref="Launch"/> sends an item or equipment icon from a point to a player and calls
    /// the delivery on arrival; <see cref="FlushAll"/> (stage end, run end) delivers everything still flying, so nothing
    /// is lost with the old scene. Each flight is delivered exactly once.
    /// </summary>
    internal static class ItemFlight
    {
        /// <summary>Just above the body's feet (a droplet spawned there lands at once), or <paramref name="fallback"/> without a body.</summary>
        internal static Vector3 DropPoint(CharacterBody body, Vector3 fallback) => body ? body.footPosition + Vector3.up * 1.5f : fallback;

        private static readonly List<ItemFlightOrb> inFlight = new List<ItemFlightOrb>(64);

        private static readonly FieldInfo pickupOrbEffectField = SharedGameMembers.PickupTransferOrb_orbEffectPrefab.Find();
        private static GameObject pickupOrbEffect;

        /// <summary>False when the game's pickup orb effect can't be found (game changed): equipment flies without a visual.</summary>
        internal static bool PickupOrbEffectFound => pickupOrbEffectField != null;

        /// <summary>The game's pickup orb effect (loaded by the game at startup), or null.</summary>
        internal static GameObject PickupOrbEffect
        {
            get
            {
                if (!pickupOrbEffect && pickupOrbEffectField != null) pickupOrbEffect = pickupOrbEffectField.GetValue(null) as GameObject;
                return pickupOrbEffect;
            }
        }

        /// <summary>
        /// Sends <paramref name="orb"/> (with <see cref="Orb.origin"/>, <see cref="ItemTransferOrb.itemIndex"/> and
        /// <see cref="ItemFlightOrb.iconPickup"/> filled in) to <paramref name="recipient"/>'s body. Without
        /// <paramref name="animate"/> or an orb manager it is delivered at once. Without a body (dead recipient) it has no
        /// visual but still arrives after the travel time. Server only.
        /// </summary>
        internal static void Launch(ItemFlightOrb orb, CharacterMaster recipient, float travelTime, bool animate, Action<bool> deliver)
        {
            orb.deliver = deliver;
            if (!animate || !OrbManager.instance)
            {
                Arrive(orb, stageEnding: false);
                return;
            }

            orb.travelDuration = travelTime;
            CharacterBody body = recipient ? recipient.GetBody() : null;
            if (body)
            {
                if (body.mainHurtBox) orb.target = body.mainHurtBox;
                else orb.orbEffectTargetObjectOverride = body.networkIdentity;
            }

            inFlight.Add(orb);
            try { OrbManager.instance.AddOrb(orb); }
            catch (Exception e)
            {
                // The orb never started flying (Begin threw before OrbManager took it): deliver it now.
                Log.Error("Item orb failed to start, delivering at once: " + e);
                Arrive(orb, stageEnding: false);
            }
        }

        /// <summary>Stage or run end: delivers every flight still in the air, newest first.</summary>
        internal static void FlushAll()
        {
            if (inFlight.Count == 0) return;
            ItemFlightOrb[] flights = inFlight.ToArray();
            inFlight.Clear();
            for (int i = flights.Length - 1; i >= 0; i--) Arrive(flights[i], stageEnding: true);
        }

        /// <summary>
        /// Delivery that never throws: it runs inside OrbManager.FixedUpdate (a throw stalls every other orb) and inside
        /// the stage change (a throw stops the scene from changing).
        /// </summary>
        internal static void Arrive(ItemFlightOrb orb, bool stageEnding)
        {
            if (orb.done) return;
            orb.done = true;
            int i = inFlight.IndexOf(orb);
            if (i >= 0)
            {
                int last = inFlight.Count - 1;
                inFlight[i] = inFlight[last];
                inFlight.RemoveAt(last);
            }
            try { orb.deliver?.Invoke(stageEnding); }
            catch (Exception e) { Log.Error("Item delivery failed: " + e); }
        }
    }
}
