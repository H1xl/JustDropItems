using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace H1xl.Shared
{
    /// <summary>
    /// Plain MonoMod detours found by reflection: no HookGenPatcher/MMHOOK dependency (MMHOOK is stale for DLC4), and a
    /// method renamed by a game update only disables the feature that uses it, with one warning, instead of the mod.
    /// The handler is a static method whose first parameter is the original as a delegate
    /// (<c>static void OnX(Action&lt;Self, A&gt; orig, Self self, A a)</c>; a custom delegate type for ref/out).
    /// </summary>
    internal static class HookUtil
    {
        private static readonly List<Hook> hooks = new List<Hook>();

        /// <summary>Hooks <paramref name="method"/> (declared in the mod's GameMembers) with the static
        /// <paramref name="handlerType"/>.<paramref name="handler"/>. Returns false (and logs why) when it couldn't.</summary>
        internal static bool Add(GameMethod method, Type handlerType, string handler)
        {
            MethodInfo target = method.Find();
            if (target == null)
            {
                Log.Warn($"{method} not found (game updated?). The feature using it is disabled.");
                return false;
            }
            MethodInfo to = handlerType.GetMethod(handler, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (to == null)
            {
                Log.Error($"Hook handler {handlerType.Name}.{handler} not found (mod bug). {method} is not hooked.");
                return false;
            }
            try
            {
                hooks.Add(new Hook(target, to));
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to hook {method}: {e}");
                return false;
            }
        }

        /// <summary>Removes every hook added by this mod.</summary>
        internal static void DisposeAll()
        {
            foreach (Hook h in hooks) h.Dispose();
            hooks.Clear();
        }
    }
}
