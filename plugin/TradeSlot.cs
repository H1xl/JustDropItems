using System.Reflection;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace JustDropItems
{
    /// <summary>
    /// Added to every item icon and equipment icon (inventory bar and scoreboard). Hover and turn the wheel to choose an amount,
    /// click to drop, drag onto a player in the scoreboard to give. Only the request is sent: the host does the rest.
    /// </summary>
    internal sealed class TradeSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool isEquipment;

        private ItemIcon itemIcon;
        private EquipmentIcon equipmentIcon;
        private bool hovered;
        private int amount; // 0 = none chosen
        private float pauseUntil;
        private bool dragging;
        private GameObject ghost;

        /// <summary>Frame of the last click this slot handled; the vanilla inspect click is skipped on that frame.</summary>
        internal int handledFrame = -1;

        private static readonly FieldInfo displayInventory = GameMembers.ItemInventoryDisplay_inventory.Find();
        private static readonly FieldInfo equipmentInventory = GameMembers.EquipmentIcon_targetInventory.Find();
        private static readonly FieldInfo equipmentDisplayData = GameMembers.EquipmentIcon_currentDisplayData.Find();
        private static readonly FieldInfo displayDataDef = GameMembers.DisplayData_equipmentDef.Find();

        private static bool hostMissingShown;

        /// <summary>Run start: the "host has no mod" line may show again (the next game may be with another host).</summary>
        internal static void ResetNotices() => hostMissingShown = false;

        private void Awake()
        {
            itemIcon = GetComponent<ItemIcon>();
            equipmentIcon = GetComponent<EquipmentIcon>();
        }

        private void OnDisable()
        {
            hovered = false;
            ResetAmount();
            EndGhost();
        }

        // ---------------------------------------------------------------- what this icon shows

        private Inventory Inventory
        {
            get
            {
                if (isEquipment) return equipmentIcon ? equipmentInventory?.GetValue(equipmentIcon) as Inventory : null;
                ItemInventoryDisplay display = GetComponentInParent<ItemInventoryDisplay>();
                return display ? displayInventory?.GetValue(display) as Inventory : null;
            }
        }

        private CharacterMaster Owner
        {
            get
            {
                Inventory inv = Inventory;
                return inv ? inv.GetComponent<CharacterMaster>() : null;
            }
        }

        private int Index
        {
            get
            {
                if (!isEquipment) return itemIcon ? (int)itemIcon.itemIndex : -1;
                if (!equipmentIcon || equipmentDisplayData == null) return -1;
                object data = equipmentDisplayData.GetValue(equipmentIcon);
                var def = displayDataDef?.GetValue(data) as EquipmentDef;
                return def ? (int)def.equipmentIndex : -1;
            }
        }

        /// <summary>The equipment target of this icon: the second equipment icon shows MUL-T's other slot.</summary>
        private Net.Target EquipmentTarget =>
            equipmentIcon && equipmentIcon.displayAlternateEquipment ? Net.Target.AltEquipment : Net.Target.Equipment;

        private static CharacterMaster LocalMaster => LocalUserManager.GetFirstLocalUser()?.currentNetworkUser?.master;

        /// <summary>The local player may use this icon: it is theirs, or the host's rules let them use other players' items.</summary>
        private bool Usable(out CharacterMaster owner)
        {
            owner = Owner;
            if (!owner || !owner.playerCharacterMasterController || Index < 0) return false;
            Rules rules = Net.HostRules;
            if (owner == LocalMaster) return true;
            if (rules == null) return false;
            return rules.takeFrom == TakeFromMode.Everyone || (rules.takeFrom == TakeFromMode.HostAll && NetworkServer.active);
        }

        private bool HostReady()
        {
            if (Net.HostReady) return true;
            if (Net.HostMissing && !hostMissingShown)
            {
                hostMissingShown = true;
                Chat.AddMessage("<color=#FF8282>" + Language.GetString(Tokens.NoHost) + "</color>");
            }
            return false;
        }

        private StackModel Model(CharacterMaster owner) =>
            StackModel.Of(owner.inventory, (ItemIndex)Index, Net.HostRules ?? new Rules { allowEthereal = true, allowTemporary = true },
                          Settings.barOrder.Value);

        private int MaxAmount(StackModel model) => Amount.Max(model.Movable, Net.HostRules?.maxPerAction ?? 100);

        // ---------------------------------------------------------------- hover and wheel

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Net.SendHello(); // early, so the rules are there by the first click
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (dragging) return; // the cursor leaves the icon while dragging it: keep the chosen amount for the drop or give
            if (Settings.resetOnLeave.Value) ResetAmount();
            else AmountPopup.Hide(this);
        }

        private void ResetAmount()
        {
            amount = 0;
            AmountPopup.Hide(this);
        }

        private void Update()
        {
            if (!hovered || isEquipment || dragging) return;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f) return;
            if (!Usable(out CharacterMaster owner) || !HostReady()) return;
            if (Time.unscaledTime < pauseUntil) return; // stopped at a border
            int dir = wheel > 0 ? 1 : -1;
            if (Settings.invertWheel.Value) dir = -dir;
            bool fast = Settings.Held(Settings.fastStepKey);
            Step(owner, dir, fast, !fast && Settings.Held(Settings.percentStepKey));
            StackModel model = Model(owner);
            AmountPopup.Show(this, (RectTransform)transform, model, amount, MaxAmount(model));
        }

        private void Step(CharacterMaster owner, int dir, bool fast, bool percent)
        {
            StackModel model = Model(owner);
            int max = MaxAmount(model);
            if (max <= 0) return;
            int step = Amount.StepSize(max, fast, percent, Settings.wheelStep.Value, Settings.fastStep.Value, Settings.percentStep.Value);
            amount = Amount.Next(amount, max, dir, step, model.Borders(), out bool atBorder);
            // A border holds the amount for a moment, so the player can stop on it.
            if (atBorder) pauseUntil = Time.unscaledTime + Settings.boundaryPause.Value;
        }

        // ---------------------------------------------------------------- gamepad (see Gamepad)

        internal bool padSelected;
        private bool giving;
        private int recipient;

        /// <summary>The local player may use this icon with the gamepad (then the game's own submit on it is skipped).</summary>
        internal bool WantsPad() => Usable(out _);

        internal void PadEnter()
        {
            padSelected = true;
            giving = false;
            amount = 0;
            Net.SendHello();
        }

        internal void PadLeave()
        {
            padSelected = false;
            giving = false;
            ResetAmount();
        }

        internal void PadStep(int dir, bool fast, bool percent)
        {
            if (!Usable(out CharacterMaster owner) || !HostReady()) return;
            if (giving)
            {
                var list = Recipients(owner);
                if (list.Count > 0) recipient = (recipient + dir + list.Count) % list.Count;
                return;
            }
            if (isEquipment || Time.unscaledTime < pauseUntil) return;
            if (amount == 0) amount = 1;
            Step(owner, dir, fast, percent);
        }

        internal void PadDrop(bool all)
        {
            if (!Usable(out CharacterMaster owner) || !HostReady()) return;
            if (giving)
            {
                if (all) return; // only the Drop button confirms a gift
                var list = Recipients(owner);
                if (list.Count == 0) return;
                CharacterMaster to = list[Mathf.Clamp(recipient, 0, list.Count - 1)];
                Net.RequestGive(owner, isEquipment ? EquipmentTarget : Net.Target.Item, Index, isEquipment ? 1 : Mathf.Max(1, amount), to);
                giving = false;
                amount = 0;
                return;
            }
            if (isEquipment)
            {
                Net.RequestDrop(owner, EquipmentTarget, Index, 1);
                return;
            }
            int count = all ? MaxAmount(Model(owner)) : Mathf.Max(1, amount);
            if (count <= 0) return;
            Net.RequestDrop(owner, Net.Target.Item, Index, count);
            amount = 0;
        }

        internal void PadToggleGive()
        {
            if (!Usable(out CharacterMaster owner) || !HostReady()) return;
            giving = !giving;
            if (!giving) return;
            if (!isEquipment && amount == 0) amount = 1;
            // Start on the player the last gift went to, if they are still there.
            var list = Recipients(owner);
            recipient = Mathf.Clamp(list.IndexOf(lastRecipient), 0, Mathf.Max(0, list.Count - 1));
        }

        internal void PadCancel() => giving = false;

        private static CharacterMaster lastRecipient;

        private static readonly System.Collections.Generic.List<CharacterMaster> recipients = new System.Collections.Generic.List<CharacterMaster>();

        /// <summary>Every other connected player, in the scoreboard's order. One shared list, refilled on every call.</summary>
        private static System.Collections.Generic.List<CharacterMaster> Recipients(CharacterMaster owner)
        {
            recipients.Clear();
            foreach (PlayerCharacterMasterController pc in PlayerCharacterMasterController.instances)
                if (pc && pc.master && pc.master != owner && pc.isConnected) recipients.Add(pc.master);
            return recipients;
        }

        private static string Button(PadButton b) => "<b>" + b + "</b>";

        // Button hints of the gamepad window: built again only when a button setting or the language changes, not every frame.
        private static string hintItem, hintEquipment, hintGive, hintLanguage;
        private static long hintButtons = -1;

        private static void RefreshHints()
        {
            long buttons = (long)Settings.padLess.Value | (long)Settings.padMore.Value << 8 | (long)Settings.padDrop.Value << 16
                | (long)Settings.padDropAll.Value << 24 | (long)Settings.padGive.Value << 32 | (long)Settings.padCancel.Value << 40;
            string language = Language.currentLanguageName;
            if (buttons == hintButtons && language == hintLanguage) return;
            hintButtons = buttons;
            hintLanguage = language;
            hintItem = "<size=85%><color=#B4B4B4>" + Language.GetStringFormatted(Tokens.PadHint, Button(Settings.padLess.Value),
                Button(Settings.padMore.Value), Button(Settings.padDrop.Value), Button(Settings.padDropAll.Value),
                Button(Settings.padGive.Value)) + "</color></size>";
            hintEquipment = "<size=85%><color=#B4B4B4>" + Language.GetStringFormatted(Tokens.PadHintEquipment,
                Button(Settings.padDrop.Value), Button(Settings.padGive.Value)) + "</color></size>";
            hintGive = "\n<size=85%><color=#B4B4B4>" + Language.GetStringFormatted(Tokens.PadGiveHint, Button(Settings.padLess.Value),
                Button(Settings.padMore.Value), Button(Settings.padDrop.Value), Button(Settings.padCancel.Value)) + "</color></size>";
        }

        /// <summary>The window while the icon is selected with the gamepad: the amount and the buttons, or the player to give to.</summary>
        private void PadRefresh()
        {
            if (!Usable(out CharacterMaster owner) || !Net.HostReady)
            {
                AmountPopup.Hide(this);
                return;
            }
            var rect = (RectTransform)transform;
            bool hints = Settings.gamepadHints.Value;
            if (hints) RefreshHints();
            if (giving)
            {
                var list = Recipients(owner);
                string text;
                if (list.Count == 0) text = Language.GetString(Tokens.PadNoPlayers);
                else
                {
                    CharacterMaster to = list[Mathf.Clamp(recipient, 0, list.Count - 1)];
                    lastRecipient = to;
                    string what = isEquipment ? Notices.EquipmentName(Index) : Notices.ItemName((ItemIndex)Index, amount > 1 ? "(" + amount + ")" : string.Empty);
                    string name = Util.EscapeRichTextForTextMeshPro(Notices.LocalName(to));
                    text = Language.GetStringFormatted(Tokens.PadGiveTo, what, "<color=#AAE6F0>" + name + "</color>");
                }
                if (hints) text += hintGive;
                AmountPopup.ShowText(this, rect, text);
                return;
            }
            if (isEquipment)
            {
                if (!hints) { AmountPopup.Hide(this); return; }
                AmountPopup.ShowText(this, rect, hintEquipment);
                return;
            }
            StackModel model = Model(owner);
            int max = MaxAmount(model);
            if (max <= 0) { AmountPopup.Hide(this); return; }
            if (amount > max) amount = max;
            AmountPopup.Show(this, rect, model, Mathf.Max(1, amount), max, hints ? hintItem : null);
        }

        /// <summary>Redraws the popup when the stack changes while it is open.</summary>
        private void LateUpdate()
        {
            if (padSelected)
            {
                PadRefresh();
                return;
            }
            if (amount <= 0 || isEquipment || !hovered) return;
            if (!Usable(out CharacterMaster owner)) { ResetAmount(); return; }
            StackModel model = Model(owner);
            int max = MaxAmount(model);
            if (max <= 0) { ResetAmount(); return; }
            if (amount > max) amount = max;
            AmountPopup.Show(this, (RectTransform)transform, model, amount, max);
        }

        // ---------------------------------------------------------------- click: drop

        /// <summary>True if a click with the drop button on this icon is ours (the vanilla inspect click is skipped then).</summary>
        internal bool WantsClick(PointerEventData.InputButton button) =>
            button == (PointerEventData.InputButton)(int)Settings.dropButton.Value && Usable(out _);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragging || !WantsClick(eventData.button)) return;
            if (!Usable(out CharacterMaster owner) || !HostReady()) return; // no host rules: the vanilla click stays
            handledFrame = Time.frameCount;

            if (isEquipment)
            {
                Net.RequestDrop(owner, EquipmentTarget, Index, 1);
                return;
            }
            int count = amount > 0 ? amount
                : Settings.Held(Settings.dropAllKey) ? MaxAmount(Model(owner))
                : 1;
            if (count <= 0) return;
            Net.RequestDrop(owner, Net.Target.Item, Index, count);
            ResetAmount();
        }

        // ---------------------------------------------------------------- drag: give

        public void OnBeginDrag(PointerEventData eventData)
        {
            if ((!Settings.dragToGive.Value && !Settings.dragOutToDrop.Value) || eventData.button != (PointerEventData.InputButton)(int)Settings.dropButton.Value) return;
            if (!Usable(out _) || !HostReady()) return;
            dragging = true;
            AmountPopup.Hide(this);
            BeginGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging) MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            EndGhost();
            if (!Usable(out CharacterMaster owner)) return;

            GameObject over = eventData.pointerCurrentRaycast.gameObject;
            ScoreboardStrip strip = over ? over.GetComponentInParent<ScoreboardStrip>() : null;
            Inventory targetInventory = strip && strip.itemInventoryDisplay ? displayInventory?.GetValue(strip.itemInventoryDisplay) as Inventory : null;
            CharacterMaster recipient = targetInventory ? targetInventory.GetComponent<CharacterMaster>() : null;
            int count = isEquipment ? 1 : amount > 0 ? amount : 1;
            Net.Target target = isEquipment ? EquipmentTarget : Net.Target.Item;

            if (recipient && recipient != owner)
            {
                if (Settings.dragToGive.Value) Net.RequestGive(owner, target, Index, count, recipient);
            }
            else if (Settings.dragOutToDrop.Value && !recipient && !OverOwnItems(over, owner))
            {
                // Released on an empty place: drop it.
                Net.RequestDrop(owner, target, Index, count);
            }
            ResetAmount();
        }

        /// <summary>Released on the owner's own items (their scoreboard row, item bar or equipment): the drag is cancelled.</summary>
        private bool OverOwnItems(GameObject over, CharacterMaster owner)
        {
            if (!over) return false;
            if (over.transform.IsChildOf(transform)) return true;
            ItemInventoryDisplay display = over.GetComponentInParent<ItemInventoryDisplay>();
            if (display && displayInventory?.GetValue(display) as Inventory == owner.inventory) return true;
            EquipmentIcon equipment = over.GetComponentInParent<EquipmentIcon>();
            return equipment && equipmentInventory?.GetValue(equipment) as Inventory == owner.inventory;
        }

        private void BeginGhost(PointerEventData eventData)
        {
            Texture texture = isEquipment ? equipmentIcon?.iconImage?.texture : itemIcon?.image?.texture;
            ghost = new GameObject("JustDropItemsDrag", typeof(RectTransform));
            Overlay.Attach(ghost.transform, transform);
            var image = ghost.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0.7f);
            var rt = (RectTransform)ghost.transform;
            Vector2 size = ((RectTransform)transform).rect.size;
            rt.sizeDelta = size.sqrMagnitude > 1f ? size : new Vector2(48f, 48f);
            MoveGhost(eventData);
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (!ghost) return;
            var parent = (RectTransform)ghost.transform.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, Overlay.Camera, out Vector2 local))
                ghost.transform.localPosition = local;
        }

        private void EndGhost()
        {
            if (ghost) Destroy(ghost);
            ghost = null;
        }
    }
}
