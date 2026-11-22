using System;
using System.IO;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;

namespace H1xl.Shared
{
    /// <summary>
    /// Entry point for a mod's Risk Of Options page; touches none of that mod's types, so it is safe to call when
    /// Risk Of Options isn't installed. Use <see cref="GUID"/> in the soft <c>BepInDependency</c>.
    /// </summary>
    internal static class RooPage
    {
        internal const string GUID = "com.rune580.riskofoptions";

        /// <summary>
        /// Runs <paramref name="register"/> (a lambda calling the mod's <c>RiskOfOptionsCompat.Register</c>) only when
        /// Risk Of Options is installed. A failure leaves one error and no page; the mod keeps working.
        /// </summary>
        internal static void TryRegister(Action register)
        {
            if (!Chainloader.PluginInfos.ContainsKey(GUID)) return;
            try { register(); }
            catch (Exception e) { Log.Error("Risk Of Options page failed: " + e); }
        }
    }

    /// <summary>
    /// Typed adders for a Risk Of Options page. Uses Risk Of Options types: call it only from the mod's
    /// <c>RiskOfOptionsCompat</c>, which <see cref="RooPage.TryRegister"/> runs. <c>disabled</c> greys an option out
    /// while it returns true (e.g. while the mod's Enabled switch is off).
    /// </summary>
    internal static class RooHelper
    {
        /// <summary>Page description and icon (<c>icon.png</c> next to the dll, the Thunderstore icon).</summary>
        internal static void Page(string description, string pluginFolder)
        {
            ModSettingsManager.SetModDescription(description);
            Sprite icon = LoadIcon(Path.Combine(pluginFolder, "icon.png"));
            if (icon) ModSettingsManager.SetModIcon(icon);
        }

        internal static void CheckBox(ConfigEntry<bool> entry, BaseOptionConfig.IsDisabledDelegate disabled = null) =>
            ModSettingsManager.AddOption(new CheckBoxOption(entry, new CheckBoxConfig { checkIfDisabled = disabled }));

        /// <summary>Drop-down for an enum setting.</summary>
        internal static void Choice(ConfigEntryBase entry, BaseOptionConfig.IsDisabledDelegate disabled = null) =>
            ModSettingsManager.AddOption(new ChoiceOption(entry, new ChoiceConfig { checkIfDisabled = disabled }));

        internal static void Key(ConfigEntry<KeyboardShortcut> entry) =>
            ModSettingsManager.AddOption(new KeyBindOption(entry));

        internal static void IntSlider(ConfigEntry<int> entry, int min, int max, string format = "{0}",
            BaseOptionConfig.IsDisabledDelegate disabled = null) =>
            ModSettingsManager.AddOption(new IntSliderOption(entry, new IntSliderConfig
            {
                min = min, max = max, formatString = format, checkIfDisabled = disabled,
            }));

        /// <summary>Float slider that moves in <paramref name="step"/> increments.</summary>
        internal static void Slider(ConfigEntry<float> entry, float min, float max, float step, string format,
            BaseOptionConfig.IsDisabledDelegate disabled = null) =>
            ModSettingsManager.AddOption(new StepSliderOption(entry, new StepSliderConfig
            {
                min = min, max = max, increment = step, FormatString = format, checkIfDisabled = disabled,
            }));

        /// <summary>Text field; applies when the field is left or Enter is pressed, not on every keystroke.</summary>
        internal static void Text(ConfigEntry<string> entry, BaseOptionConfig.IsDisabledDelegate disabled = null) =>
            ModSettingsManager.AddOption(new StringInputFieldOption(entry, new InputFieldConfig
            {
                submitOn = InputFieldConfig.SubmitEnum.OnExitOrSubmit, checkIfDisabled = disabled,
            }));

        private static Sprite LoadIcon(string path)
        {
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}
