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

        public static void Validate()
        {
            _errors = 0;
            var rooms = LevelData.Rooms;

            Check(rooms.Length == 11, "rooms == 11, got " + rooms.Length);
            Check(rooms.Sum(r => r.checkpoints.Length) == 13, "checkpoints == 13, got " + rooms.Sum(r => r.checkpoints.Length));
            Check(rooms.Sum(r => r.enemies.Length) == 5, "enemies == 5, got " + rooms.Sum(r => r.enemies.Length));
            Check(rooms.Sum(r => r.sensors.Length) == 2, "sensors == 2, got " + rooms.Sum(r => r.sensors.Length));
            Check(rooms.Sum(r => r.doors.Length) == 2, "doors == 2, got " + rooms.Sum(r => r.doors.Length));
            Check(rooms.Sum(r => r.pickups.Length) == 3, "pickups == 3 (negative/flash/shutter), got " + rooms.Sum(r => r.pickups.Length));
            Check(rooms.All(r => !string.IsNullOrEmpty(r.title)), "every room has a HUD title");
            Check(rooms.Sum(r => r.exits.Length) == 1, "exits == 1, got " + rooms.Sum(r => r.exits.Length));
            Check(rooms.Sum(r => r.hints.Length) == 18, "hints == 18, got " + rooms.Sum(r => r.hints.Length));

            var allBoxes = rooms.SelectMany(r => r.boxes).ToList();
            int darkPaths = allBoxes.Count(b => b.type == ExposureObjectType.DarkPath);
            int barriers = allBoxes.Count(b => b.type == ExposureObjectType.BrightBarrier);
            Check(darkPaths == 11, "DarkPath boxes == 11, got " + darkPaths);
            Check(barriers == 5, "BrightBarrier boxes == 5, got " + barriers);

            // anti-sequence-break ceilings at every chokepoint
            string[] ceilings =
            {
                "R5_GateCeiling", "R6_DoorCeiling", "R8_BarrierCeiling",
                "R9_CapCeiling", "R10_CeilingA", "R10_DoorCeiling", "R10_FinalCeiling",
            };
            foreach (var name in ceilings)
                Check(allBoxes.Any(b => b.name == name), "chokepoint ceiling present: " + name);

            // sensor->door ids resolve
            var doorIds = new HashSet<string>(rooms.SelectMany(r => r.doors).Select(d => d.id));
            foreach (var s in rooms.SelectMany(r => r.sensors))
                Check(doorIds.Contains(s.doorId), "sensor " + s.name + " door id resolves: " + s.doorId);

            SeamAudit(allBoxes);

            // edit-mode instantiation: every def becomes exactly one direct child
            int expected = rooms.Sum(r =>
                r.boxes.Length + r.enemies.Length + r.sensors.Length + r.doors.Length +
                r.pickups.Length + r.checkpoints.Length + r.hints.Length + r.exits.Length);
            var root = LevelBuilder.Build(10);
            int actual = root.transform.childCount;
            Check(actual == expected, "built object count == " + expected + ", got " + actual);
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
