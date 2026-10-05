using System;
using System.Collections.Generic;
using T17.Services;
using T17.UI;
using Team17.Scripts.Services.Input;
using UnityEngine;

namespace DateEverythingAccess
{
    // MOD TUTORIAL. Short spoken lessons that run alongside the game's own tutorial and teach the
    // mod's keys at the moment the player needs them. Each step fires once, in order, when the game
    // reaches the point it is written for. Most steps key off the game's tutorial objective (the
    // signpost text), so a player who loads a save part way through hears the step for where they
    // are, not the ones before it. Steps that depend on something the player does in between
    // (turning on tracking, a message appearing, morning coming round, a popup being closed) only
    // fire after the step before them. H repeats the most recent tutorial message at any time.
    internal sealed partial class AccessibilityWatcher
    {
        private delegate bool TutorialTrigger(AccessibilityWatcher w);

        private sealed class ModTutorialStep
        {
            public string Key;
            // True: may fire straight away even if earlier steps never did (it reads game state).
            // False: only fires as the very next step after the one before it.
            public bool StandsAlone;
            public TutorialTrigger Trigger;
        }

        private static readonly ModTutorialStep[] ModTutorialSteps =
        {
            new ModTutorialStep { Key = "modtut_start", StandsAlone = true,
                Trigger = w => InHouse() && ObjectiveHas("start your new job at your computer") },
            new ModTutorialStep { Key = "modtut_tracking_on", StandsAlone = false,
                Trigger = w => ObjectTracker.IsTracking && ObjectiveHas("start your new job at your computer") },
            new ModTutorialStep { Key = "modtut_tasks", StandsAlone = false,
                Trigger = w => w.ModTutorialHeard("task") },
            new ModTutorialStep { Key = "modtut_phone_messages", StandsAlone = true,
                Trigger = w => InHouse() && ObjectiveHas("message on your phone") },
            new ModTutorialStep { Key = "modtut_leave_office", StandsAlone = true,
                Trigger = w => InHouse() && (ObjectiveHas("leave the office") || ObjectiveHas("leave your office")) },
            new ModTutorialStep { Key = "modtut_package", StandsAlone = true,
                Trigger = w => InHouse() && (ObjectiveHas("check the delivery") || ObjectiveHas("check the package")) },
            new ModTutorialStep { Key = "modtut_dorian", StandsAlone = true,
                Trigger = w => InHouse() && (ObjectiveHas("awaken a door") || ObjectiveHas("talk to dorian")) },
            new ModTutorialStep { Key = "modtut_awaken_phone", StandsAlone = true,
                Trigger = w => InHouse() && ObjectiveHas("awaken your phone") },
            new ModTutorialStep { Key = "modtut_magnifying_glass", StandsAlone = true,
                Trigger = w => InHouse() && ObjectiveHas("locate the magnifying glass") },
            new ModTutorialStep { Key = "modtut_rumors", StandsAlone = true,
                Trigger = w => InHouse() && ObjectiveHas("follow the clue in roomers") },
            new ModTutorialStep { Key = "modtut_bed", StandsAlone = true,
                Trigger = w => InHouse() && ObjectiveHas("charge the dateviators by going to sleep") },
            new ModTutorialStep { Key = "modtut_morning", StandsAlone = false,
                Trigger = w => w.ModTutorialMorningArrived() },
            new ModTutorialStep { Key = "modtut_finish", StandsAlone = false,
                Trigger = w => w.ModTutorialMessageClosed() },
        };

        private const float ModTutorialPollSeconds = 0.25f;
        // How long a trigger must hold before the step speaks, so the game's own announcement (the
        // new objective, the end of a conversation) is heard first.
        private const float ModTutorialSettleSeconds = 1.0f;

        private int _modTutorialLastFired = -1;
        private int _modTutorialPendingStep = -1;
        private float _modTutorialPendingSince;
        private float _modTutorialNextPoll;
        private string _modTutorialLastMessage;
        private bool _modTutorialSpeaking;
        private bool _modTutorialHooked;
        private readonly List<string> _modTutorialHeard = new List<string>();
        private readonly object _modTutorialHeardLock = new object();
        private string _modTutorialStartPhase;
        private bool _modTutorialSawMorningLeave;
        private bool _modTutorialMessageWasOpen;
        private bool _hKeyWasDown;

        private static bool InHouse()
        {
            return Singleton<GameController>.Instance != null &&
                Singleton<GameController>.Instance.viewState == VIEW_STATE.HOUSE;
        }

        private static bool ObjectiveHas(string phrase)
        {
            return TryGetCurrentTutorialObjectiveText(out string text) && ContainsToken(text, phrase);
        }

        // Everything the mod says passes through here, so a step can wait for the game's own text
        // (as read out by the mod) without knowing which screen shows it. The tutorial's own lines
        // are left out so "Press R to see your tasks" can't trigger itself.
        private void OnModTutorialSpoken(string text)
        {
            if (_modTutorialSpeaking || string.IsNullOrEmpty(text)) return;
            lock (_modTutorialHeardLock)
            {
                _modTutorialHeard.Add(text);
                if (_modTutorialHeard.Count > 40) _modTutorialHeard.RemoveAt(0);
            }
        }

        private bool ModTutorialHeard(string phrase)
        {
            lock (_modTutorialHeardLock)
            {
                for (int i = 0; i < _modTutorialHeard.Count; i++)
                    if (_modTutorialHeard[i].IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
            }
            return false;
        }

        private static string CurrentDayPhaseName()
        {
            if (Singleton<DayNightCycle>.Instance == null) return null;
            try { return Singleton<DayNightCycle>.Instance.GetTime().ToString(); }
            catch { return null; }
        }

        // Morning after the night's sleep: the phase has to move away from what it was when the bed
        // step spoke, then land on morning.
        private bool ModTutorialMorningArrived()
        {
            string phase = CurrentDayPhaseName();
            if (string.IsNullOrEmpty(phase)) return false;
            bool isMorning = phase.IndexOf("morn", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!string.Equals(phase, _modTutorialStartPhase, StringComparison.Ordinal) || !isMorning)
                _modTutorialSawMorningLeave = true;
            return _modTutorialSawMorningLeave && isMorning && InHouse();
        }

        // The "glasses off mode" message: any game popup or dialog box that opens after the morning
        // step and is then closed.
        private bool ModTutorialMessageClosed()
        {
            bool open = TryBuildPopupAnnouncement(out _) || TryBuildUIDialogAnnouncement(out _);
            if (open)
            {
                _modTutorialMessageWasOpen = true;
                return false;
            }
            return _modTutorialMessageWasOpen;
        }

        private void UpdateModTutorial()
        {
            if (!_modTutorialHooked)
            {
                _modTutorialHooked = true;
                ScreenReader.Spoken += OnModTutorialSpoken;
            }
            if (_modTutorialLastFired >= ModTutorialSteps.Length - 1) return;
            if (UnityEngine.Time.unscaledTime < _modTutorialNextPoll) return;
            _modTutorialNextPoll = UnityEngine.Time.unscaledTime + ModTutorialPollSeconds;

            // The latest step that is ready wins; earlier ones the player has moved past are skipped.
            int ready = -1;
            try
            {
                for (int i = ModTutorialSteps.Length - 1; i > _modTutorialLastFired; i--)
                {
                    ModTutorialStep step = ModTutorialSteps[i];
                    if (!step.StandsAlone && i != _modTutorialLastFired + 1) continue;
                    if (step.Trigger(this)) { ready = i; break; }
                }
            }
            catch (Exception ex)
            {
                if (Main.Log != null) Main.Log.LogWarning("Mod tutorial check failed: " + ex.Message);
                return;
            }

            if (ready < 0)
            {
                _modTutorialPendingStep = -1;
                return;
            }
            if (ready != _modTutorialPendingStep)
            {
                _modTutorialPendingStep = ready;
                _modTutorialPendingSince = UnityEngine.Time.unscaledTime;
                return;
            }
            if (UnityEngine.Time.unscaledTime - _modTutorialPendingSince < ModTutorialSettleSeconds)
                return;

            FireModTutorialStep(ready);
        }

        private void FireModTutorialStep(int index)
        {
            ModTutorialStep step = ModTutorialSteps[index];
            _modTutorialLastFired = index;
            _modTutorialPendingStep = -1;
            lock (_modTutorialHeardLock) _modTutorialHeard.Clear();
            if (step.Key == "modtut_bed")
            {
                _modTutorialStartPhase = CurrentDayPhaseName();
                _modTutorialSawMorningLeave = false;
            }
            if (step.Key == "modtut_morning")
                _modTutorialMessageWasOpen = TryBuildPopupAnnouncement(out _) || TryBuildUIDialogAnnouncement(out _);

            _modTutorialLastMessage = Loc.Get(step.Key);
            if (Main.Log != null) Main.Log.LogInfo("Mod tutorial step " + (index + 1) + "/" + ModTutorialSteps.Length + ": " + step.Key);
            SpeakModTutorial(_modTutorialLastMessage, interrupt: false);
        }

        private void SpeakModTutorial(string text, bool interrupt)
        {
            _modTutorialSpeaking = true;
            try { ScreenReader.Say(text, interrupt: interrupt); }
            finally { _modTutorialSpeaking = false; }
        }

        // H: say the latest tutorial message again.
        private void HandleModTutorialRepeat(bool pressed)
        {
            if (!pressed) return;
            if (string.IsNullOrEmpty(_modTutorialLastMessage))
            {
                SpeakModTutorial(Loc.Get("modtut_none"), interrupt: true);
                return;
            }
            SpeakModTutorial(_modTutorialLastMessage, interrupt: true);
        }
    }
}
