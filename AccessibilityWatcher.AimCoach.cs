using System;
using System.Collections.Generic;
using T17.Services;
using T17.UI;
using Team17.Scripts.Services.Input;
using UnityEngine;

namespace DateEverythingAccess
{
    // AIM COACH. Small things sitting on big things (the magnifying glass on the shelf) are easy to
    // walk up to and hard to land the crosshair on: a few degrees off and the game picks the shelf.
    // When the player is already close enough to use the target, walking is pointless, so Alt+O
    // talks them onto it instead: "up", "a little left", "far right and down", until the game's own
    // selection is the target, then "On target." The player turns and gazes with their usual keys;
    // the coach only speaks. It also takes over when autowalk arrives but can't land the aim itself.
    internal sealed partial class AccessibilityWatcher
    {
        private bool _aimCoachActive;
        private InteractableObj _aimCoachTarget;
        private string _aimCoachLastCue;
        private float _aimCoachLastSpokeAt;
        private float _aimCoachStartedAt;
        private float _aimCoachNoAimSince;

        private const float AimCoachMinGapSeconds = 0.35f;   // don't talk over the player's turning
        private const float AimCoachRepeatSeconds = 4f;      // remind if nothing has changed
        private const float AimCoachTimeoutSeconds = 90f;
        private const float AimCoachLostAimSeconds = 3f;     // no usable aim point for this long -> stop
        private const float AimCoachFarDeg = 45f;
        private const float AimCoachLittleDeg = 12f;
        // Alt+O coaches instead of walking only when the player is already this close ("by the
        // shelf"); from further away it walks as normal even if the game's reach is larger.
        private const float AimCoachStartWithinM = 4f;

        // True when the player is within the target's reach and the game's ray could select it from
        // where the camera is now (some aim point on it is visible, not hidden behind what it sits on).
        private static bool IsTargetAimableFromHere(InteractableObj target, out Vector3 aimPoint)
        {
            return IsTargetAimableFromHere(target, float.PositiveInfinity, out aimPoint);
        }

        private static bool IsTargetAimableFromHere(InteractableObj target, float maxDistance, out Vector3 aimPoint)
        {
            aimPoint = default;
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy)
                return false;
            BetterPlayerControl bpc = BetterPlayerControl.Instance;
            Camera cam = Camera.main;
            if (bpc == null || cam == null)
                return false;
            if (!TryFindSelectableAimPoint(target, cam.transform.position, out aimPoint))
                return false;
            float reach = Mathf.Min(GetInteractableApproachRadius(target), maxDistance);
            return GetFlatDistance(bpc.transform.position, aimPoint) <= reach;
        }

        // A point on the target that the game's own camera ray would select from camPos.
        private static bool TryFindSelectableAimPoint(InteractableObj target, Vector3 camPos, out Vector3 aimPoint)
        {
            aimPoint = default;
            Collider[] cols = target.GetComponentsInChildren<Collider>();
            for (int c = 0; c < cols.Length; c++)
            {
                if (cols[c] == null || !cols[c].enabled) continue;
                List<Vector3> aims = SimpleNavPlanner.AimCandidates(cols[c], camPos);
                for (int a = 0; a < aims.Count; a++)
                {
                    Vector3 d = aims[a] - camPos;
                    if (d.sqrMagnitude < 1e-6f) continue;
                    if (SimpleNavPlanner.GameRayWouldSelect(camPos, d.normalized, target))
                    {
                        aimPoint = aims[a];
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsAimCoachTargetSelected(InteractableObj target)
        {
            InteractableManager manager = Singleton<InteractableManager>.Instance;
            if (target == null || manager == null || manager.activeObject == null || !manager.IsPlayerInRange)
                return false;
            return IsSameOrRelatedInteractableTarget(target.gameObject, manager.activeObject.gameObject);
        }

        // Begin coaching. `afterWalk` is true when autowalk has just arrived but couldn't land the
        // aim itself (the player hears why the coach is talking).
        private void StartAimCoach(InteractableObj target, bool afterWalk)
        {
            _aimCoachActive = true;
            _aimCoachTarget = target;
            _aimCoachLastCue = null;
            _aimCoachLastSpokeAt = 0f;
            _aimCoachStartedAt = Time.unscaledTime;
            _aimCoachNoAimSince = 0f;

            string label = GetObjectFacingDisplayName(target);
            if (string.IsNullOrWhiteSpace(label)) label = Loc.Get("unknown_object");
            if (Main.Log != null)
                Main.Log.LogInfo("AimCoach: start target=" + target.gameObject.name + " afterWalk=" + afterWalk);

            if (IsAimCoachTargetSelected(target))
            {
                StopAimCoach();
                ObjectTracker.NotifyAimLocked();
                ScreenReader.Say(Loc.Get("aim_coach_already_on", label));
                return;
            }

            string cue = BuildAimCue(target, out _);
            _aimCoachLastCue = cue;
            _aimCoachLastSpokeAt = Time.unscaledTime;
            ScreenReader.Say(Loc.Get(afterWalk ? "aim_coach_start_after_walk" : "aim_coach_start", label) +
                (string.IsNullOrEmpty(cue) ? string.Empty : " " + cue));
        }

        private void StopAimCoach()
        {
            if (_aimCoachActive && Main.Log != null)
                Main.Log.LogInfo("AimCoach: stop");
            _aimCoachActive = false;
            _aimCoachTarget = null;
            _aimCoachLastCue = null;
        }

        // Called every frame from Update.
        private void UpdateAimCoach()
        {
            if (!_aimCoachActive)
                return;

            InteractableObj target = _aimCoachTarget;
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy ||
                Singleton<GameController>.Instance == null ||
                Singleton<GameController>.Instance.viewState != VIEW_STATE.HOUSE)
            {
                StopAimCoach();
                return;
            }

            if (IsAimCoachTargetSelected(target))
            {
                StopAimCoach();
                ObjectTracker.NotifyAimLocked();
                ScreenReader.Say(Loc.Get("aim_coach_on_target"));
                return;
            }

            if (Time.unscaledTime - _aimCoachStartedAt > AimCoachTimeoutSeconds)
            {
                StopAimCoach();
                ScreenReader.Say(Loc.Get("aim_coach_gave_up"));
                return;
            }

            if (Time.unscaledTime - _aimCoachLastSpokeAt < AimCoachMinGapSeconds)
                return;

            string cue = BuildAimCue(target, out bool aimable);
            if (!aimable)
            {
                // Walked out of reach, or something now hides it from this spot.
                if (_aimCoachNoAimSince == 0f) _aimCoachNoAimSince = Time.unscaledTime;
                if (Time.unscaledTime - _aimCoachNoAimSince >= AimCoachLostAimSeconds)
                {
                    StopAimCoach();
                    ScreenReader.Say(Loc.Get("aim_coach_out_of_reach"));
                }
                return;
            }
            _aimCoachNoAimSince = 0f;

            bool changed = !string.Equals(cue, _aimCoachLastCue, StringComparison.Ordinal);
            bool stale = Time.unscaledTime - _aimCoachLastSpokeAt >= AimCoachRepeatSeconds;
            if (string.IsNullOrEmpty(cue) || (!changed && !stale))
                return;

            _aimCoachLastCue = cue;
            _aimCoachLastSpokeAt = Time.unscaledTime;
            ScreenReader.Say(cue);
        }

        // "a little up", "far left and down", or "almost" when both errors are inside the target's
        // own size but the game hasn't picked it yet. `aimable` is false when no aim point on the
        // target can be selected from here.
        private static string BuildAimCue(InteractableObj target, out bool aimable)
        {
            aimable = false;
            Camera cam = Camera.main;
            BetterPlayerControl bpc = BetterPlayerControl.Instance;
            if (cam == null || bpc == null || target == null)
                return null;

            if (!IsTargetAimableFromHere(target, out Vector3 aim))
                return null;
            aimable = true;

            Vector3 camPos = cam.transform.position;
            Vector3 toAim = aim - camPos;

            // Horizontal: camera heading vs direction to the aim point, both flattened.
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            Vector3 flat = toAim; flat.y = 0f;
            float yawErr = 0f;
            if (fwd.sqrMagnitude > 1e-6f && flat.sqrMagnitude > 1e-6f)
                yawErr = Vector3.SignedAngle(fwd.normalized, flat.normalized, Vector3.up);

            // Vertical: elevation of the aim point in the camera's own frame.
            Vector3 local = cam.transform.InverseTransformDirection(toAim);
            float horiz = Mathf.Max(0.001f, new Vector2(local.x, local.z).magnitude);
            float pitchErr = Mathf.Atan2(local.y, horiz) * Mathf.Rad2Deg;

            // How close counts as "on it": half the target's apparent size, within sane limits.
            float tol = 1f;
            Collider col = target.GetComponentInChildren<Collider>();
            if (col != null)
            {
                float dist = Mathf.Max(0.1f, toAim.magnitude);
                float radius = col.bounds.extents.magnitude;
                float angular = Mathf.Atan2(radius, dist) * Mathf.Rad2Deg;
                tol = Mathf.Clamp(angular * 0.5f, 0.75f, 6f);
            }

            string h = null, v = null;
            if (Mathf.Abs(yawErr) > tol)
                h = Graded(yawErr > 0f ? Loc.Get("aim_right") : Loc.Get("aim_left"), Mathf.Abs(yawErr));
            if (Mathf.Abs(pitchErr) > tol)
                v = Graded(pitchErr > 0f ? Loc.Get("aim_up") : Loc.Get("aim_down"), Mathf.Abs(pitchErr));

            if (h == null && v == null)
                return Loc.Get("aim_almost");
            if (h != null && v != null)
                return Loc.Get("aim_and", Mathf.Abs(yawErr) >= Mathf.Abs(pitchErr) ? h : v,
                    Mathf.Abs(yawErr) >= Mathf.Abs(pitchErr) ? v : h);
            return h ?? v;
        }

        private static string Graded(string direction, float errDeg)
        {
            if (errDeg >= AimCoachFarDeg) return Loc.Get("aim_far", direction);
            if (errDeg <= AimCoachLittleDeg) return Loc.Get("aim_little", direction);
            return direction;
        }
    }
}
