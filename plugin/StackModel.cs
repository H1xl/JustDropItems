using RoR2;

namespace JustDropItems
{
    internal partial struct StackModel
    {
        public static StackModel Of(Inventory inv, ItemIndex item, Rules rules, KindOrder order)
        {
            var m = new StackModel { order = order };
            if (!inv || item == ItemIndex.None) return m;
            m.permanent = inv.GetItemCountPermanent(item);
            m.ethereal = inv.GetItemCountRented(item);
            m.temporary = inv.GetItemCountTemp(item);
            m.shared = inv.GetItemCountChanneled(item);
            m.etherealAllowed = rules.allowEthereal;
            m.temporaryAllowed = rules.allowTemporary;
            return m;
        }
    }
}
