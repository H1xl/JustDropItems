namespace JustDropItems
{
    /// <summary>Kinds of copies in one stack.</summary>
    public enum ItemKind : byte { Permanent, Ethereal, Temporary, Shared }

    /// <summary>The order the movable kinds take on the amount bar, which is also the order copies leave the stack.</summary>
    public enum KindOrder : byte
    {
        PermanentEtherealTemporary,
        PermanentTemporaryEthereal,
        EtherealPermanentTemporary,
        EtherealTemporaryPermanent,
        TemporaryPermanentEthereal,
        TemporaryEtherealPermanent,
    }

    /// <summary>
    /// How many copies of each kind a stack holds, which kinds the host allows to move, and how an amount splits over them.
    /// The client builds it to draw the amount bar, the host builds it again from its own inventory (with the order the client
    /// sent) to check and execute a request, so both always split an amount the same way.
    /// </summary>
    internal partial struct StackModel
    {
        public int permanent, ethereal, temporary, shared;
        public bool etherealAllowed, temporaryAllowed;
        public KindOrder order;

        private static readonly ItemKind[][] orders =
        {
            new[] { ItemKind.Permanent, ItemKind.Ethereal, ItemKind.Temporary },
            new[] { ItemKind.Permanent, ItemKind.Temporary, ItemKind.Ethereal },
            new[] { ItemKind.Ethereal, ItemKind.Permanent, ItemKind.Temporary },
            new[] { ItemKind.Ethereal, ItemKind.Temporary, ItemKind.Permanent },
            new[] { ItemKind.Temporary, ItemKind.Permanent, ItemKind.Ethereal },
            new[] { ItemKind.Temporary, ItemKind.Ethereal, ItemKind.Permanent },
        };

        /// <summary>The three movable kinds in the given order (Shared copies never move and always come last on the bar).</summary>
        public static ItemKind[] Kinds(KindOrder order) => orders[(int)order < orders.Length ? (int)order : 0];

        public ItemKind[] Kinds() => Kinds(order);

        public int Count(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Permanent: return permanent;
                case ItemKind.Ethereal: return ethereal;
                case ItemKind.Temporary: return temporary;
                default: return shared;
            }
        }

        public bool Allowed(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Permanent: return true;
                case ItemKind.Ethereal: return etherealAllowed;
                case ItemKind.Temporary: return temporaryAllowed;
                default: return false; // Shared copies belong to an aura: moving them would duplicate the item.
            }
        }

        public int Total => permanent + ethereal + temporary + shared;

        /// <summary>Copies that can move: every allowed kind (a forbidden kind is skipped, it stops nothing).</summary>
        public int Movable => permanent + (etherealAllowed ? ethereal : 0) + (temporaryAllowed ? temporary : 0);

        /// <summary>Splits an amount over the allowed kinds in this model's order.</summary>
        public void Split(int amount, out int p, out int e, out int t)
        {
            p = e = t = 0;
            foreach (ItemKind k in Kinds())
            {
                int take = Allowed(k) ? System.Math.Min(amount, Count(k)) : 0;
                amount -= take;
                if (k == ItemKind.Permanent) p = take;
                else if (k == ItemKind.Ethereal) e = take;
                else t = take;
            }
        }

        /// <summary>The amounts where the bar has a border: the end of each allowed kind, except the last.</summary>
        public int[] Borders()
        {
            var list = new System.Collections.Generic.List<int>(3);
            int sum = 0;
            foreach (ItemKind k in Kinds())
            {
                if (!Allowed(k) || Count(k) == 0) continue;
                sum += Count(k);
                list.Add(sum);
            }
            if (list.Count > 0) list.RemoveAt(list.Count - 1); // the last end is the maximum, not a border
            return list.ToArray();
        }
    }
}
