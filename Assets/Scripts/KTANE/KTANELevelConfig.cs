// KTANELevelConfig.cs
// ScriptableObject that defines one difficulty level.
// Assets live in Assets/Resources/KTANE/Levels/ so they can be loaded at
// runtime with Resources.LoadAll<KTANELevelConfig>("KTANE/Levels").
// Create them via  Tools ▶ KTANE ▶ Create Level Assets.

using UnityEngine;

namespace KTANE
{
    [CreateAssetMenu(fileName = "Level_1", menuName = "KTANE/Level Config")]
    public class KTANELevelConfig : ScriptableObject
    {
        [Header("Identity")]
        public string levelName   = "Training";
        public int    levelNumber = 1;
        [TextArea(2, 4)]
        public string description = "One module, plenty of time. Read the manual first!";
        public Color  accentColour = Color.green;

        [Header("Timer")]
        [Tooltip("Total countdown in seconds.")]
        public float timerSeconds = 420f;   // 7 minutes

        [Header("Strikes")]
        [Tooltip("How many wrong moves are allowed before the bomb explodes.")]
        public int maxStrikes = 3;

        [Header("Active Modules")]
        [Tooltip("Which modules appear on this level's bomb.")]
        public bool wiresActive  = true;
        public bool buttonActive = false;
        public bool keypadActive = false;
        public bool simonActive  = false;

        [Header("Module Difficulty Tuning")]
        [Tooltip("Number of Simon rounds to complete (higher = harder).")]
        [Range(1, 6)]
        public int simonRounds = 3;

        [Tooltip("How long each Simon pad stays lit (lower = faster = harder).")]
        [Range(0.15f, 0.6f)]
        public float simonFlashSeconds = 0.5f;

        [Tooltip("Enable the harder wire-cut rules (see WiresModule.cs for details).")]
        public bool hardWireRules = false;

        // ── convenience ─────────────────────────────────────────────────────
        /// <summary>How many modules must be solved to defuse this level.</summary>
        public int ModulesToSolve =>
            (wiresActive  ? 1 : 0) +
            (buttonActive ? 1 : 0) +
            (keypadActive ? 1 : 0) +
            (simonActive  ? 1 : 0);
    }
}
