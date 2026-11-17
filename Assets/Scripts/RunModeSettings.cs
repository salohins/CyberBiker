using UnityEngine;

namespace Gameplay {
    // An explicit shared asset reference also works when the already-open
    // Editor scene has not picked up the new serialized scene field yet.
    public sealed class RunModeSettings:ScriptableObject {
        public Difficulty constantCombatDifficulty;
    }
}
