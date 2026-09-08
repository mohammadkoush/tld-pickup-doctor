// Logging, with one rule: a message that would repeat every frame is said ONCE and then counted.
//
// The Green Hell version of this mod learned that the hard way. A mod that logs a refusal per item
// per sweep writes forty thousand lines an hour, and the one line that mattered is somewhere in the
// middle of it. Worse, a log that noisy trains you to stop reading it - which is how a fault runs
// for weeks looking like a handful of unrelated moments.

using System.Collections.Generic;
using MelonLoader;

namespace LDPickupDoctor
{
    internal static class Log
    {
        private static MelonLogger.Instance _logger;
        private static readonly Dictionary<string, int> _saidOnce = new Dictionary<string, int>();

        public static void Attach(MelonLogger.Instance logger) { _logger = logger; }

        public static void Info(string message)
        {
            if (_logger != null) _logger.Msg(message); else MelonLogger.Msg("[LDPD] " + message);
        }

        public static void Warn(string message)
        {
            if (_logger != null) _logger.Warning(message); else MelonLogger.Warning("[LDPD] " + message);
        }

        public static void Error(string message)
        {
            if (_logger != null) _logger.Error(message); else MelonLogger.Error("[LDPD] " + message);
        }

        /// <summary>Say it the first time, and count every time after.</summary>
        public static void OnceWarn(string key, string message)
        {
            int seen;
            if (_saidOnce.TryGetValue(key, out seen))
            {
                _saidOnce[key] = seen + 1;
                return;
            }
            _saidOnce[key] = 1;
            Warn(message + "  (this is said once; the count goes in the report)");
        }

        public static void OnceInfo(string key, string message)
        {
            int seen;
            if (_saidOnce.TryGetValue(key, out seen)) { _saidOnce[key] = seen + 1; return; }
            _saidOnce[key] = 1;
            Info(message);
        }

        /// <summary>How many times a once-message would have been said. Zero if never.</summary>
        public static int Suppressed(string key)
        {
            int seen;
            return _saidOnce.TryGetValue(key, out seen) ? seen : 0;
        }

        /// <summary>Every once-message and its count, for the periodic report.</summary>
        public static IEnumerable<KeyValuePair<string, int>> Repeats()
        {
            foreach (KeyValuePair<string, int> kv in _saidOnce)
                if (kv.Value > 1) yield return kv;
        }

        /// <summary>Allow a once-message to be said again - used when the world changes under us.</summary>
        public static void Forget(string key) { _saidOnce.Remove(key); }
    }
}
