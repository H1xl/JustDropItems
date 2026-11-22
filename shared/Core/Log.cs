using System.Collections.Generic;
using BepInEx.Logging;

namespace H1xl.Shared
{
    /// <summary>
    /// The mod's log. <see cref="Init"/> in Plugin.Awake with the plugin's Logger; <see cref="IsVerbose"/> points at the
    /// mod's "VerboseLogging" setting if it has one. Anything that can repeat (per frame, per hook call) goes through
    /// <see cref="ErrorOnce"/>/<see cref="WarnOnce"/>, so a broken feature leaves one line, not thousands.
    /// </summary>
    internal static class Log
    {
        private static ManualLogSource source;
        private static readonly HashSet<string> logged = new HashSet<string>();

        /// <summary>Returns true when detail logs are wanted; null = never.</summary>
        internal static System.Func<bool> IsVerbose = null;

        internal static void Init(ManualLogSource logger) => source = logger;

        internal static bool Verbose => IsVerbose != null && IsVerbose();

        internal static void Info(object message) => source?.LogInfo(message);
        internal static void Warn(object message) => source?.LogWarning(message);
        internal static void Error(object message) => source?.LogError(message);

        /// <summary>Detail log, only when <see cref="Verbose"/>. Build expensive messages after checking Verbose yourself.</summary>
        internal static void Debug(object message)
        {
            if (Verbose) source?.LogInfo(message);
        }

        /// <summary>Logs the error the first time <paramref name="key"/> is seen; returns true if it was logged now.</summary>
        internal static bool ErrorOnce(string key, object message)
        {
            if (!logged.Add(key)) return false;
            source?.LogError(message);
            return true;
        }

        /// <summary>Logs the warning the first time <paramref name="key"/> is seen; returns true if it was logged now.</summary>
        internal static bool WarnOnce(string key, object message)
        {
            if (!logged.Add(key)) return false;
            source?.LogWarning(message);
            return true;
        }
    }
}
