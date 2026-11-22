using RoR2;
using RoR2.UI;
using Rewired;
using UnityEngine;

namespace JustDropItems
{
    /// <summary>
    /// Gamepad controls. The game already moves a selection over the scoreboard's icons with the gamepad (hold Back for the
    /// scoreboard, the D-pad or stick to move), and while something is selected it ignores the character's buttons. The icon
    /// selected there is the one these buttons act on. Buttons are read through Rewired's gamepad template, so they are the same
    /// for any gamepad (Xbox, DualShock and so on).
    /// </summary>
    internal static class Gamepad
    {
        private const float RepeatDelay = 0.35f, RepeatInterval = 0.09f;

        private static TradeSlot current;
        private static readonly bool[] prev = new bool[(int)PadButton.RightStick + 1];
        private static readonly bool[] now = new bool[prev.Length];
        private static float repeatAt;

        /// <summary>Called every frame.</summary>
        internal static void Tick()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            TradeSlot slot = null;
            MPEventSystem events = user?.eventSystem;
            if (Settings.gamepadEnabled.Value && events && events.currentInputSource == MPEventSystem.InputSource.Gamepad)
            {
                GameObject selected = events.currentSelectedGameObject;
                if (selected) slot = selected.GetComponentInParent<TradeSlot>();
            }
            bool entered = slot != current;
            if (entered)
            {
                if (current) current.PadLeave();
                current = slot;
                if (current) current.PadEnter();
            }

            IGamepadTemplate pad = slot ? Find(user) : null;
            for (int i = 0; i < now.Length; i++) now[i] = pad != null && Read(pad, (PadButton)i);
            // A button already held when an icon gets selected (e.g. the A that opened the scoreboard row) is not a new press:
            // it must be released first.
            if (entered)
                for (int i = 0; i < now.Length; i++) prev[i] = now[i];
            if (slot && pad != null) Act(slot);
            for (int i = 0; i < now.Length; i++) prev[i] = now[i];
        }

        private static void Act(TradeSlot slot)
        {
            if (Pressed(Settings.padGive.Value)) slot.PadToggleGive();
            else if (Pressed(Settings.padCancel.Value)) slot.PadCancel();
            else if (Pressed(Settings.padDropAll.Value)) slot.PadDrop(all: true);
            else if (Pressed(Settings.padDrop.Value)) slot.PadDrop(all: false);

            // Less / more: once on press, then repeated while held.
            int dir = Held(Settings.padMore.Value) ? 1 : Held(Settings.padLess.Value) ? -1 : 0;
            if (dir == 0) return;
            bool first = Pressed(dir > 0 ? Settings.padMore.Value : Settings.padLess.Value);
            if (!first && Time.unscaledTime < repeatAt) return;
            repeatAt = Time.unscaledTime + (first ? RepeatDelay : RepeatInterval);
            slot.PadStep(dir, fast: Held(Settings.padFastStep.Value), percent: Held(Settings.padPercentStep.Value));
        }

        private static bool Held(PadButton b) => now[(int)b];

        private static bool Pressed(PadButton b) => now[(int)b] && !prev[(int)b];

        /// <summary>The local player's gamepad: the last one used, else the first that has the gamepad layout.</summary>
        private static IGamepadTemplate Find(LocalUser user)
        {
            Player player = user?.inputPlayer;
            if (player == null) return null;
            if (player.controllers.GetLastActiveController(ControllerType.Joystick) is Joystick last)
            {
                IGamepadTemplate t = last.GetTemplate<IGamepadTemplate>();
                if (t != null) return t;
            }
            foreach (Joystick j in player.controllers.Joysticks)
            {
                IGamepadTemplate t = j.GetTemplate<IGamepadTemplate>();
                if (t != null) return t;
            }
            return null;
        }

        private static bool Read(IGamepadTemplate pad, PadButton b)
        {
            switch (b)
            {
                case PadButton.A: return pad.a.value;
                case PadButton.B: return pad.b.value;
                case PadButton.X: return pad.x.value;
                case PadButton.Y: return pad.y.value;
                case PadButton.LB: return pad.leftShoulder1.value;
                case PadButton.RB: return pad.rightShoulder1.value;
                case PadButton.LT: return pad.leftShoulder2.value > 0.5f;
                case PadButton.RT: return pad.rightShoulder2.value > 0.5f;
                case PadButton.LeftStick: return pad.leftStick.press.value;
                case PadButton.RightStick: return pad.rightStick.press.value;
                default: return false;
            }
        }
    }
}
