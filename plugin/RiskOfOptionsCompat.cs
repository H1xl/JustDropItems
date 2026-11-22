using System.Runtime.CompilerServices;
using H1xl.Shared;

namespace JustDropItems
{
    /// <summary>
    /// In-game settings page for Risk Of Options. Called only when that mod is installed (<see cref="RooPage"/>), so its
    /// types are never loaded otherwise. The options edit the same config entries as the file, so changes apply at once.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Register(string pluginFolder)
        {
            RooHelper.Page(
                "Drop items from your inventory or share them with teammates. Hover an item in the scoreboard and turn the wheel " +
                "(or select it with the gamepad) to choose the amount. Controls and the window are your own settings; " +
                "the rules are the host's.", pluginFolder);

            RooHelper.Choice(Settings.dropButton);
            RooHelper.Key(Settings.dropAllKey);
            RooHelper.IntSlider(Settings.wheelStep, 1, 50);
            RooHelper.Key(Settings.fastStepKey);
            RooHelper.IntSlider(Settings.fastStep, 1, 50);
            RooHelper.Key(Settings.percentStepKey);
            RooHelper.IntSlider(Settings.percentStep, 1, 50, "{0}%");
            RooHelper.CheckBox(Settings.invertWheel);
            RooHelper.Slider(Settings.boundaryPause, 0f, 1f, 0.05f, "{0:0.00} s");
            RooHelper.CheckBox(Settings.resetOnLeave);
            RooHelper.CheckBox(Settings.dragToGive);
            RooHelper.CheckBox(Settings.dragOutToDrop);

            RooHelper.Choice(Settings.popupSide);
            RooHelper.Slider(Settings.popupScale, 0.5f, 2f, 0.05f, "{0:0.00}x");
            RooHelper.CheckBox(Settings.showBreakdown);
            RooHelper.Choice(Settings.barOrder);
            RooHelper.Slider(Settings.popupOpacity, 0.3f, 1f, 0.01f, "{0:0.00}");

            RooHelper.CheckBox(Settings.animation);
            RooHelper.Slider(Settings.travelTime, 0.1f, 5f, 0.1f, "{0:0.0} s", () => !Settings.animation.Value);
            RooHelper.CheckBox(Settings.notify);

            RooHelper.Choice(Settings.takeFrom);
            RooHelper.CheckBox(Settings.allowDrop);
            RooHelper.CheckBox(Settings.allowGive);
            RooHelper.CheckBox(Settings.allowGiveToDead);
            RooHelper.CheckBox(Settings.allowGiveWhileDead);
            RooHelper.IntSlider(Settings.maxPerAction, 1, 1000);

            RooHelper.CheckBox(Settings.allowEquipment);
            RooHelper.CheckBox(Settings.allowLunar);
            RooHelper.CheckBox(Settings.allowVoid);
            RooHelper.CheckBox(Settings.allowQuest);
            RooHelper.CheckBox(Settings.allowHallowed);
            RooHelper.CheckBox(Settings.allowHex);
            RooHelper.CheckBox(Settings.allowPages);
            RooHelper.CheckBox(Settings.allowMeals);
            RooHelper.CheckBox(Settings.allowAspects);
            RooHelper.CheckBox(Settings.allowTemporary);
            RooHelper.CheckBox(Settings.allowEthereal);
            RooHelper.CheckBox(Settings.allowGiveConsumed);
            RooHelper.Text(Settings.blacklist);

            RooHelper.Choice(Settings.recycler);

            RooHelper.CheckBox(Settings.chatCommands);
            RooHelper.CheckBox(Settings.announceDrops);
            RooHelper.CheckBox(Settings.announceGives);

            RooHelper.CheckBox(Settings.verbose);

            RooHelper.CheckBox(Settings.gamepadEnabled);
            RooHelper.Choice(Settings.padDrop);
            RooHelper.Choice(Settings.padDropAll);
            RooHelper.Choice(Settings.padGive);
            RooHelper.Choice(Settings.padCancel);
            RooHelper.Choice(Settings.padLess);
            RooHelper.Choice(Settings.padMore);
            RooHelper.Choice(Settings.padFastStep);
            RooHelper.Choice(Settings.padPercentStep);
            RooHelper.CheckBox(Settings.gamepadHints);
        }
    }
}
