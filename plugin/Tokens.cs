using System;
using System.Collections.Generic;
using RoR2;

namespace JustDropItems
{
    /// <summary>
    /// The mod's own language tokens (English and Russian; English is the game's fallback for every other language).
    /// Messages travel between players as token keys and values, and each player formats them in their own language.
    /// </summary>
    internal static partial class Tokens
    {
        internal const string Permanent = "JUSTDROP_KIND_PERMANENT", Shared = "JUSTDROP_KIND_SHARED",
                              Dropped = "JUSTDROP_DROPPED", DroppedFrom = "JUSTDROP_DROPPED_FROM",
                              Gave = "JUSTDROP_GAVE", GaveFrom = "JUSTDROP_GAVE_FROM", NoHost = "JUSTDROP_NO_HOST",
                              OfStack = "JUSTDROP_OF_STACK", PadHint = "JUSTDROP_PAD_HINT", PadHintEquipment = "JUSTDROP_PAD_HINT_EQUIPMENT",
                              PadGiveTo = "JUSTDROP_PAD_GIVE_TO", PadGiveHint = "JUSTDROP_PAD_GIVE_HINT", PadNoPlayers = "JUSTDROP_PAD_NO_PLAYERS";

        // Reject (why the host refused a request): plugin/Logic/ItemRules.cs.

        private static readonly Dictionary<string, string> en = new Dictionary<string, string>
        {
            [Permanent] = "Permanent",
            [OfStack] = "of {0}",
            [PadHint] = "{0}/{1} amount  ·  {2} drop  ·  {3} all  ·  {4} give",
            [PadHintEquipment] = "{0} drop  ·  {1} give",
            [PadGiveTo] = "Give {0} to {1}",
            [PadGiveHint] = "{0}/{1} player  ·  {2} give  ·  {3} cancel",
            [PadNoPlayers] = "No one to give to.",
            [Shared] = "Shared",
            [Dropped] = "<style=cEvent>{0} dropped {1}</style>",
            [DroppedFrom] = "<style=cEvent>{0} dropped {1} from {2}'s inventory</style>",
            [Gave] = "<style=cEvent>{0} gave {1} to {2}</style>",
            [GaveFrom] = "<style=cEvent>{0} gave {1} from {2}'s inventory to {3}</style>",
            [NoHost] = "The host doesn't have Just Drop Items, so items can't be dropped or given.",
            [R(Reject.DropDisabled)] = "The host doesn't allow dropping items.",
            [R(Reject.GiveDisabled)] = "The host doesn't allow giving items.",
            [R(Reject.NotYours)] = "You can only drop or give your own items.",
            [R(Reject.Blacklisted)] = "{0} can't be dropped or given.",
            [R(Reject.Lunar)] = "The host doesn't allow dropping or giving lunar items.",
            [R(Reject.Void)] = "The host doesn't allow dropping or giving void items.",
            [R(Reject.Quest)] = "The host doesn't allow dropping or giving quest items.",
            [R(Reject.NoCopies)] = "None of these copies can be dropped or given.",
            [R(Reject.SharedOnly)] = "Shared copies come from an aura and can't be moved.",
            [R(Reject.NeedsBody)] = "You need to be alive to drop items.",
            [R(Reject.DeadGiver)] = "The host doesn't allow giving items while dead.",
            [R(Reject.TargetDead)] = "The host doesn't allow giving items to a dead player.",
            [R(Reject.TargetSelf)] = "The item is already theirs.",
            [R(Reject.TargetEquipmentFull)] = "{0} already has equipment.",
            [R(Reject.NothingThere)] = "That item is no longer there.",
            [R(Reject.TooFast)] = "Too fast, try again.",
            [R(Reject.Hidden)] = "This item can't be dropped or given.",
            [R(Reject.EquipmentDisabled)] = "The host doesn't allow dropping or giving equipment.",
            [R(Reject.NoTarget)] = "That player isn't in the game.",
            [R(Reject.ConsumedNoDrop)] = "Used-up items can't be dropped, only given to a player.",
            [R(Reject.ConsumedDisabled)] = "The host doesn't allow giving used-up items.",
            [R(Reject.Hallowed)] = "The host doesn't allow dropping or giving Hallowed items.",
            [R(Reject.Hex)] = "The host doesn't allow dropping or giving Hexes.",
            [R(Reject.Page)] = "The host doesn't allow dropping or giving N'tormat Pages.",
            [R(Reject.Meal)] = "The host doesn't allow dropping or giving meals.",
            [R(Reject.EliteAspect)] = "The host doesn't allow dropping or giving elite aspects.",
        };

        private static readonly Dictionary<string, string> ru = new Dictionary<string, string>
        {
            [Permanent] = "Постоянные",
            [OfStack] = "из {0}",
            [PadHint] = "{0}/{1} количество  ·  {2} выбросить  ·  {3} все  ·  {4} передать",
            [PadHintEquipment] = "{0} выбросить  ·  {1} передать",
            [PadGiveTo] = "Передать {0}: {1}",
            [PadGiveHint] = "{0}/{1} игрок  ·  {2} передать  ·  {3} отмена",
            [PadNoPlayers] = "Некому передать.",
            [Shared] = "Общие",
            [Dropped] = "<style=cEvent>{0} выбрасывает: {1}</style>",
            [DroppedFrom] = "<style=cEvent>{0} выбрасывает из инвентаря игрока {2}: {1}</style>",
            [Gave] = "<style=cEvent>{0} передаёт игроку {2}: {1}</style>",
            [GaveFrom] = "<style=cEvent>{0} передаёт из инвентаря игрока {2} игроку {3}: {1}</style>",
            [NoHost] = "У хоста нет Just Drop Items: выбрасывать и передавать предметы нельзя.",
            [R(Reject.DropDisabled)] = "Хост запретил выбрасывать предметы.",
            [R(Reject.GiveDisabled)] = "Хост запретил передавать предметы.",
            [R(Reject.NotYours)] = "Выбрасывать и передавать можно только свои предметы.",
            [R(Reject.Blacklisted)] = "{0}: этот предмет нельзя выбросить или передать.",
            [R(Reject.Lunar)] = "Хост запретил выбрасывать и передавать лунные предметы.",
            [R(Reject.Void)] = "Хост запретил выбрасывать и передавать предметы Бездны.",
            [R(Reject.Quest)] = "Хост запретил выбрасывать и передавать квестовые предметы.",
            [R(Reject.NoCopies)] = "Ни одну из этих копий нельзя выбросить или передать.",
            [R(Reject.SharedOnly)] = "Общие копии даёт аура, их нельзя передать.",
            [R(Reject.NeedsBody)] = "Чтобы выбрасывать предметы, нужно быть живым.",
            [R(Reject.DeadGiver)] = "Хост запретил передавать предметы, пока вы мертвы.",
            [R(Reject.TargetDead)] = "Хост запретил передавать предметы мёртвым игрокам.",
            [R(Reject.TargetSelf)] = "Этот предмет уже у него.",
            [R(Reject.TargetEquipmentFull)] = "У игрока {0} уже есть снаряжение.",
            [R(Reject.NothingThere)] = "Этого предмета уже нет.",
            [R(Reject.TooFast)] = "Слишком быстро, попробуйте ещё раз.",
            [R(Reject.Hidden)] = "Этот предмет нельзя выбросить или передать.",
            [R(Reject.EquipmentDisabled)] = "Хост запретил выбрасывать и передавать снаряжение.",
            [R(Reject.NoTarget)] = "Этого игрока нет в игре.",
            [R(Reject.ConsumedNoDrop)] = "Использованные предметы нельзя выбросить, только передать игроку.",
            [R(Reject.ConsumedDisabled)] = "Хост запретил передавать использованные предметы.",
            [R(Reject.Hallowed)] = "Хост запретил выбрасывать и передавать освящённые предметы.",
            [R(Reject.Hex)] = "Хост запретил выбрасывать и передавать проклятия (Hex).",
            [R(Reject.Page)] = "Хост запретил выбрасывать и передавать страницы Н'тормата.",
            [R(Reject.Meal)] = "Хост запретил выбрасывать и передавать блюда.",
            [R(Reject.EliteAspect)] = "Хост запретил выбрасывать и передавать аспекты элит.",
        };

        internal static string R(Reject reason) => "JUSTDROP_REJECT_" + reason.ToString().ToUpperInvariant();

        /// <summary>Adds the tokens to a language right after the game loads its strings.</summary>
        internal static void OnLoadStrings(Action<Language> orig, Language self)
        {
            orig(self);
            Apply(self);
        }

        /// <summary>Adds the tokens to languages loaded before the hook was installed.</summary>
        internal static void ApplyLoaded()
        {
            foreach (Language language in Language.GetAllLanguages()) Apply(language);
        }

        private static void Apply(Language language)
        {
            if (language == null || string.IsNullOrEmpty(language.name)) return;
            Dictionary<string, string> table = language.name.Equals("en", StringComparison.OrdinalIgnoreCase) ? en
                : language.name.Equals("ru", StringComparison.OrdinalIgnoreCase) ? ru : null;
            if (table == null) return;
            foreach (KeyValuePair<string, string> pair in table) language.SetStringByToken(pair.Key, pair.Value);
        }

        /// <summary>
        /// The game's own word for a kind of copy, in the current language and colour: "Temporary" and "Ethereal" come from the
        /// item-name modifiers "{0} (Temporary)" and "{0} (Ethereal)".
        /// </summary>
        internal static string KindName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Temporary: return FromModifier("ITEM_MODIFIER_TEMP", "Temporary");
                case ItemKind.Ethereal: return FromModifier("ITEM_MODIFIER_RENTAL", "Ethereal");
                case ItemKind.Shared: return Language.GetString(Shared);
                default: return Language.GetString(Permanent);
            }
        }

        private static string FromModifier(string token, string fallback)
        {
            string s = Language.GetString(token);
            if (string.IsNullOrEmpty(s) || s == token) return fallback;
            s = s.Replace("{0}", string.Empty).Trim();
            if (s.StartsWith("(")) s = s.Substring(1);
            if (s.EndsWith(")")) s = s.Substring(0, s.Length - 1);
            return s.Trim();
        }
    }
}
