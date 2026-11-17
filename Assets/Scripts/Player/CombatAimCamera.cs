using System;
using UnityEngine;

namespace CyberBiker.Player {
    [Serializable]
    public sealed class CombatAimCamera {
        public Vector3 offset;
        [Range(15,120)] public float fov=40;
        public float pitch,yaw,roll;
        public float shiftX=-2,shiftY=.5f,forwardShift=2,steeringRoll=-30;
        [Min(.1f)] public float followResponse=3;
        [HideInInspector] public bool configured;
    }
}
