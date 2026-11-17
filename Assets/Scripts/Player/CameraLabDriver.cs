#if UNITY_EDITOR
using System;
using UnityEngine;

namespace CyberBiker.Player {
    // Runs after player input and before the real camera. Only installed in a Lab preview.
    [DefaultExecutionOrder(90)]
    public sealed class CameraLabDriver : MonoBehaviour {
        public Action Tick;
        private void Update()=>Tick?.Invoke();
    }
}
#endif
