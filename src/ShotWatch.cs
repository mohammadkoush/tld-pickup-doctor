// Who is taking these screenshots?
//
// WHY THIS FILE EXISTS
//
// Fifty-two high-resolution screenshots appeared on the desktop across one evening, 409 MB of them.
// The first explanation offered was a hotkey collision - the mod's F9 toggle and the game's own
// screenshot key - and one coincidence did support it:
//
//     01:04:42.252   auto pickup off      (the mod)
//     01:04:42, 01:04:42, 01:04:43        (three screenshots)
//
// But that toggle logged ONCE all evening, and there were fifty-two files. One press cannot explain
// fifty-two, and the person playing says he never pressed it deliberately. So the honest position is
// that the cause is unknown, and a guess dressed up as an answer is worse than an open question.
//
// The rule on this station is that if a fault cannot be counted, the counting IS the first fix. So
// this watches the folder and writes down what the mod was doing at the moment each file appeared.
// Next time there is a file, there is also a line saying whether a hotkey had just been pressed,
// whether the settings window had just opened or closed, or whether nothing of ours was happening at
// all - which would clear the mod entirely and point at the game's own saving.
//
// It costs one directory listing every two seconds, of one folder, and it turns itself off after an
// hour of quiet.

using System;
using System.IO;
using UnityEngine;

namespace LDPickupDoctor
{
    internal static class ShotWatch
    {
        private static string _folder;
        private static int _known = -1;
        private static float _nextCheck;
        private static float _startedAt;
        private static bool _off;

        // What the mod was doing most recently, so a file that appears can be attributed or cleared.
        private static string _lastAction = "nothing";
        private static float _lastActionAt = -999f;

        public static int Seen;

        // What the on-screen banner says, and when it was raised. A log line is no use to somebody
        // who is playing: the whole point of catching a collision is to catch it at the moment it
        // happens, on the screen being looked at.
        public static string CollisionText = "";
        public static float CollisionAt = -999f;
        public static string LastCollisionKey = "";

        /// <summary>Called from anywhere the mod does something a game might react to.</summary>
        public static void Note(string what)
        {
            _lastAction = what;
            _lastActionAt = Time.realtimeSinceStartup;
        }

        public static void Tick()
        {
            if (_off || !Settings.WatchScreenshots.Value) return;

            float now = Time.realtimeSinceStartup;
            if (_startedAt <= 0f) _startedAt = now;
            if (now < _nextCheck) return;
            _nextCheck = now + 2f;

            try
            {
                if (_folder == null)
                {
                    _folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (string.IsNullOrEmpty(_folder) || !Directory.Exists(_folder))
                    {
                        _off = true;
                        Log.Info("screenshot watch: no desktop folder to watch, so it is off.");
                        return;
                    }
                }

                string[] files = Directory.GetFiles(_folder, "screen_*.png");
                if (_known < 0)
                {
                    _known = files.Length;
                    Log.Info("screenshot watch: " + _known + " screen_*.png already on the desktop. "
                        + "Anything new from here is reported with what this mod was doing at the time.");
                    return;
                }

                if (files.Length > _known)
                {
                    int added = files.Length - _known;
                    Seen += added;
                    float since = now - _lastActionAt;

                    // The attribution, stated as a fact and not as a conclusion: what the mod last
                    // did, and how long before the file appeared. A gap of many seconds means the mod
                    // had nothing to do with it.
                    // NAMING THE COLLISION IS THE WHOLE POINT NOW.
                    //
                    // A mod cannot read the game's key bindings, so it cannot know in advance which
                    // keys are safe - that was learned the expensive way, with F8, F9 and F10 all
                    // turning out to be screenshot keys and 409 MB of PNGs to show for it. What it
                    // CAN do is notice the symptom within two seconds and name the suspect, so the
                    // next bad key costs one line in a log instead of an evening.
                    string blame = _lastActionAt < 0f
                        ? "this mod has done nothing at all this session - it is not us"
                        : (since < 1.5f
                            ? "COLLISION: this happened " + since.ToString("0.0") + "s after "
                              + _lastAction + ". That key is very likely one of the game's own "
                              + "screenshot keys - rebind it in the Keys tab. The game binds F8, F9 "
                              + "and F10 to screenshots and ignores modifiers on them."
                            : "the mod's last action was " + since.ToString("0.0")
                              + "s earlier (" + _lastAction + "), which is too long ago to be the cause");

                    // ON SCREEN, not only in the log. A log line is no use to somebody who is
                    // playing: the moment worth telling them about a bad hotkey is the moment they
                    // press it, on the screen they are looking at.
                    if (since < 1.5f && _lastActionAt >= 0f)
                    {
                        LastCollisionKey = _lastAction;
                        CollisionAt = now;
                        CollisionText = _lastAction + " is also one of the game's own screenshot keys "
                            + "- it just wrote a PNG to your desktop. Rebind it in the Keys tab.";
                    }

                    Log.Warn("screenshot watch: " + added + " new screen_*.png on the desktop ("
                        + files.Length + " total). " + blame);
                    _known = files.Length;
                }
                else if (files.Length < _known)
                {
                    _known = files.Length;      // some were deleted; re-baseline quietly
                }

                // Stop after an hour of watching if nothing has ever appeared. The question will have
                // been answered by then, and a poll that runs forever for no reason is the kind of
                // thing this mod complains about elsewhere.
                if (Seen == 0 && now - _startedAt > 3600f)
                {
                    _off = true;
                    Log.Info("screenshot watch: an hour with no new files, so it has stopped looking.");
                }
            }
            catch (Exception e)
            {
                _off = true;
                Log.OnceWarn("shotwatch", "the screenshot watch could not read the desktop: " + e.Message
                    + " - it is off for this session and nothing else is affected.");
            }
        }
    }
}
