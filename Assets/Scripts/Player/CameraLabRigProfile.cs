using UnityEngine;

namespace CyberBiker.Player {
    // Only created when the Camera Lab's Save for game is used. An absent
    // profile leaves the scene's authored camera settings unchanged.
    public sealed class CameraLabRigProfile : ScriptableObject {
        [TextArea(4,12)] public string rigJson="{}";
        public void Apply(CameraController controller){
            if(controller==null || string.IsNullOrEmpty(rigJson))return;
            try{
                JsonUtility.FromJsonOverwrite(rigJson,controller);
                controller.InitializeCombatCameraDefaults(!rigJson.Contains("\"combatRiding\""));
                controller.InitializeCombatAimDefaults(!rigJson.Contains("\"combatAim\""),!rigJson.Contains("\"aimFollowResponse\""));
            }
            catch(System.ArgumentException){Debug.LogWarning("Ignoring invalid saved Camera Lab rig settings.");}
        }
    }
}
