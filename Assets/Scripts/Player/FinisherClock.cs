using UnityEngine;

namespace CyberBiker.Player {
    // The Lab captures every transition at a fixed rate even when recording a
    // frame is expensive. Player builds always use the ordinary real-time clock.
    public static class FinisherClock {
#if UNITY_EDITOR
        public static float EditorCaptureStep {get;set;}
#endif
        public static float UnscaledDeltaTime {
            get {
#if UNITY_EDITOR
                if(EditorCaptureStep>0)return EditorCaptureStep;
#endif
                return Time.unscaledDeltaTime;
            }
        }
    }
}
