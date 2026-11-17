using UnityEngine;

namespace Gameplay {
    public enum RunPlayMode { Progression, ConstantCombat }

    // Survives scene loads/restarts within a play session, without putting the
    // mode in saved tuning values that could override a fresh menu choice.
    public static class RunModeSelection {
        public static bool HasSelection {get;private set;}
        public static RunPlayMode Selected {get;private set;}
        public static RunPlayMode Normalize(RunPlayMode mode)=>mode==RunPlayMode.ConstantCombat ? mode : RunPlayMode.Progression;
        public static void Select(RunPlayMode mode){Selected=Normalize(mode);HasSelection=true;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Selected=RunPlayMode.Progression;HasSelection=false;}
    }
}
