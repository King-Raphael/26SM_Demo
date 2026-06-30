using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Darkroom
{
    /// Batchmode validation: data counts vs the spec tables, chokepoint
    /// ceilings, a seam audit printout, and an edit-mode build count.
    /// Run: Unity -batchmode -nographics -projectPath ... -executeMethod Darkroom.EditorTools.Validate
    public static class EditorTools
    {
        static int _errors;

        // The dev mechanic sandbox (LevelBuilder.BuildDevSandbox, gated on
        // GameManager.DevWarpEnabled) is always built and lives far to the right at
        // x >= ~390 under "Lab_" names. The authored game spans x <= 176, so this
        // threshold cleanly separates the two when counting built children. We also
        // keep a "Lab_" name filter: the dev trail sits at the origin (Trail() never
        // offsets its transform), so position alone wouldn't exclude it.
        const float DevSandboxMinX = 250f;

        public static void Validate()
        {
            _errors = 0;
            var rooms = LevelData.Rooms;

            // ---- data-table counts (literals current as of Milestone 15) ----
            // These stay hard-coded on purpose: they are the regression guard. If an
            // authored element is silently dropped or duplicated the sum drifts off
            // the expected value and the check fails.
            Check(rooms.Length == 11, "rooms == 11, got " + rooms.Length);
            Check(rooms.Sum(r => r.checkpoints.Length) == 14, "checkpoints == 14, got " + rooms.Sum(r => r.checkpoints.Length));
            Check(rooms.Sum(r => r.enemies.Length) == 5, "enemies == 5, got " + rooms.Sum(r => r.enemies.Length));
            Check(rooms.Sum(r => r.sensors.Length) == 4, "sensors == 4 (R6 body + R6 meter + R10 photo + R10 wash tray), got " + rooms.Sum(r => r.sensors.Length));
            Check(rooms.Sum(r => r.doors.Length) == 3, "doors == 3 (Door_R6 + Door_R6L + Door_R10), got " + rooms.Sum(r => r.doors.Length));
            Check(rooms.Sum(r => r.pickups.Length) == 2, "pickups == 2 (flash/shutter; Under granted at boot), got " + rooms.Sum(r => r.pickups.Length));
            Check(rooms.All(r => !string.IsNullOrEmpty(r.title)), "every room has a HUD title");
            Check(rooms.Sum(r => r.exits.Length) == 2, "exits == 2 (R0 paper-door + R10 finale), got " + rooms.Sum(r => r.exits.Length));
            Check(rooms.Sum(r => r.hints.Length) == 18, "hints == 18 (R9 curtain teach pulled; R10 wash), got " + rooms.Sum(r => r.hints.Length));

            // ---- newer authored mechanics (added across M11–M16) ----
            Check(rooms.Sum(r => r.trails.Length)    == 1, "trails == 1 (R1_SeeTrail; R0 now climbs DarkPath steps), got " + rooms.Sum(r => r.trails.Length));
            Check(rooms.Sum(r => r.lifts.Length)     == 1, "shadow lifts == 1 (R9), got "           + rooms.Sum(r => r.lifts.Length));
            Check(rooms.Sum(r => r.riseLifts.Length) == 1, "rise lifts == 1 (R4), got "             + rooms.Sum(r => r.riseLifts.Length));
            Check(rooms.Sum(r => r.bridges.Length)   == 1, "light bridges == 1 (R8), got "          + rooms.Sum(r => r.bridges.Length));
            Check(rooms.Sum(r => r.burns.Length)     == 4, "burn-papers == 4 (R2/R5/R10x2), got "   + rooms.Sum(r => r.burns.Length));
            Check(rooms.Sum(r => r.umbrals.Length)   == 1, "umbral barriers == 1 (R10 only; R9's teach pulled to keep the drop pure), got " + rooms.Sum(r => r.umbrals.Length));
            Check(rooms.Sum(r => r.fixPlats.Length)  == 5, "latent platforms == 5 (R3 x2 debut + R7/R8/R10 x1 each alt routes), got " + rooms.Sum(r => r.fixPlats.Length));
            Check(rooms.Sum(r => r.lostFrames.Length) == 4, "lost frames == 4 (verb-gated pockets in R1/R5/R8/R10), got " + rooms.Sum(r => r.lostFrames.Length));

            var allBoxes = rooms.SelectMany(r => r.boxes).ToList();
            int darkPaths = allBoxes.Count(b => b.type == ExposureObjectType.DarkPath);
            int barriers = allBoxes.Count(b => b.type == ExposureObjectType.BrightBarrier);
            Check(darkPaths == 7, "DarkPath boxes == 7 (R0 x3, R6 x2, R8, R10; R3's 3 retired for the FixPlatform debut), got " + darkPaths);
            // BrightBarrier is retired from the levels — every white wall is a
            // BurnPaper now. Guard that none creep back into the data.
            Check(barriers == 0, "BrightBarrier boxes == 0 (retired), got " + barriers);

            // anti-sequence-break ceilings at every chokepoint. R8 dropped its
            // ceiling when it was rebuilt as wide light/dark bridges.
            string[] ceilings =
            {
                "R5_GateCeiling", "R6_DoorCeiling",
                "R9_CapCeiling", "R10_CeilingA", "R10_DoorCeiling", "R10_FinalCeiling",
            };
            foreach (var name in ceilings)
                Check(allBoxes.Any(b => b.name == name), "chokepoint ceiling present: " + name);

            // sensor->door ids resolve (now also covers R6's light-meter -> Door_R6L)
            var doorIds = new HashSet<string>(rooms.SelectMany(r => r.doors).Select(d => d.id));
            foreach (var s in rooms.SelectMany(r => r.sensors))
                if (!s.wash) Check(doorIds.Contains(s.doorId), "sensor " + s.name + " door id resolves: " + s.doorId);

            SeamAudit(allBoxes);

            // ---- edit-mode instantiation cross-check ----
            // Every authored def must become exactly one direct child of the level
            // root. `expected` is DERIVED from LevelData (not a literal) so it tracks
            // the data automatically while still catching a builder that drops or
            // doubles a def. Add the structural extras LevelBuilder injects: each
            // RiseLift hangs 2 shaft rails on the root, and rooms 9+ add the
            // R9_Blackout set piece. The dev sandbox is excluded (see DevSandboxMinX).
            int defChildren = rooms.Sum(r =>
                r.boxes.Length + r.enemies.Length + r.sensors.Length + r.doors.Length +
                r.pickups.Length + r.checkpoints.Length + r.hints.Length + r.exits.Length +
                r.umbrals.Length + r.lifts.Length + r.burns.Length + r.bridges.Length +
                r.riseLifts.Length + r.trails.Length + r.fixPlats.Length + r.lostFrames.Length);
            int riseRails = rooms.Sum(r => r.riseLifts.Length) * 2;
            int blackout = rooms.Length > 9 ? 1 : 0;
            int prologueProps = 1; // the "PrologueProps" decoration container (room 0)
            int expected = defChildren + riseRails + blackout + prologueProps;

            var root = LevelBuilder.Build(rooms.Length - 1);
            int actual = root.transform.Cast<Transform>()
                .Count(t => !t.name.StartsWith("Lab_") && t.position.x < DevSandboxMinX);
            Check(actual == expected, "built level object count == " + expected + ", got " + actual);
            Object.DestroyImmediate(root);

            if (_errors == 0)
            {
                Debug.Log("[DarkroomValidator] PASS — all checks green.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[DarkroomValidator] FAIL — " + _errors + " check(s) failed.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[DarkroomValidator] OK: " + what);
            else { _errors++; Debug.LogError("[DarkroomValidator] FAIL: " + what); }
        }

        /// Prints walkable tops sorted by x for human seam review
        /// (the spec allows puzzle-bridged gaps, so this never hard-fails).
        static void SeamAudit(List<BoxDef> boxes)
        {
            var floors = boxes
                .Where(b => b.w >= 1.0f && b.h >= 0.3f)
                .OrderBy(b => b.cx - b.w / 2f)
                .Select(b => string.Format("{0,-20} x [{1,7:0.00} .. {2,7:0.00}]  top {3,6:0.00}  ({4})",
                    b.name, b.cx - b.w / 2f, b.cx + b.w / 2f, b.cy + b.h / 2f, b.type))
                .ToList();
            Debug.Log("[DarkroomValidator] Seam audit (" + floors.Count + " walkable boxes):\n" + string.Join("\n", floors));
        }
    }
}
