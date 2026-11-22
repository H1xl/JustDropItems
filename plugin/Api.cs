using RoR2;

namespace JustDropItems
{
    /// <summary>
    /// For other mods that give items straight into an inventory instead of letting the player pick them up
    /// (AutoLootDistribution). Without this call Just Drop Items can't know that such an item was rerolled by the Recycler
    /// or came out of a printer, and those marks decide how the item behaves once it is dropped again.
    /// Call it on the host, by reflection, so there is no hard dependency:
    /// <code>Type.GetType("JustDropItems.Api, JustDropItems")?.GetMethod("ItemGranted")?.Invoke(null, new object[] { master, item, recycled, duplicated });</code>
    /// </summary>
    public static class Api
    {
        /// <param name="master">The player who got the item.</param>
        /// <param name="item">The item.</param>
        /// <param name="recycled">The pickup's "Recycled" flag (<c>CreatePickupInfo.recycled</c>).</param>
        /// <param name="duplicated">The pickup's "Duplicated" flag (<c>CreatePickupInfo.duplicated</c>: a printer's output).</param>
        public static void ItemGranted(CharacterMaster master, ItemIndex item, bool recycled, bool duplicated) =>
            Recycling.OnGranted(master, item, recycled, duplicated);
    }
}
