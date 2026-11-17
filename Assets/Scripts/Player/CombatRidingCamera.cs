using System;
using UnityEngine;

namespace CyberBiker.Player {
    [Serializable]
    public sealed class CombatRidingCamera {
        public bool enabled=true;
        [Min(1)] public float enemyRange=60;
        [Min(0)] public float exitPadding=10;
        [Min(0)] public float returnDelay=1;
        [Min(.01f)] public float blendTime=.8f;
        public Vector3 offset;
        [Range(15,120)] public float fov=50;
        public float pitch=2,yaw,roll;
        public float shiftX=-2,shiftY=.5f,forwardShift=2,steeringRoll=-30;
        [Min(.1f)] public float followResponse=3;
        [HideInInspector] public bool configured;

        [NonSerialized] private bool nearby;
        [NonSerialized] private float absentFor,blend;
        public bool EnemyNearby=>nearby;
        public float Amount=>Mathf.SmoothStep(0,1,blend);
#if UNITY_EDITOR
        [NonSerialized] private int preview=-1;
        public void SetPreview(bool? combat,bool instant=false){
            preview=combat.HasValue ? combat.Value ? 1 : 0 : -1;
            if(instant && combat.HasValue){nearby=combat.Value;blend=enabled && combat.Value ? 1 : 0;absentFor=0;}
        }
#endif
        public void Tick(Vector3 riderPosition,float deltaTime){
            if(deltaTime<=0)return;
            bool detected=enabled && HasNearbyEnemy(riderPosition,Mathf.Max(1,enemyRange)+(nearby ? Mathf.Max(0,exitPadding) : 0));
#if UNITY_EDITOR
            if(preview>=0)detected=enabled && preview==1;
#endif
            if(detected){nearby=true;absentFor=0;}
            else if(nearby){absentFor+=deltaTime;if(!enabled || absentFor>=Mathf.Max(0,returnDelay))nearby=false;}
            blend=Mathf.MoveTowards(blend,enabled && nearby ? 1 : 0,deltaTime/Mathf.Max(.01f,blendTime));
        }
        private static bool HasNearbyEnemy(Vector3 riderPosition,float range){
            float squaredRange=range*range;
            var enemies=Enemy.Instances;
            for(int i=0;i<enemies.Count;i++){
                var enemy=enemies[i];
                if(enemy==null || !enemy.gameObject.activeInHierarchy || !enemy.IsAlive || enemy.IsEncounterQueued || enemy.ArrivalVisibility<.1f)continue;
                var delta=enemy.transform.position-riderPosition;delta.y=0;
                if(delta.sqrMagnitude<=squaredRange)return true;
            }
            return false;
        }
    }
}
