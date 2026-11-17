using CyberBiker.Pooling;
using System.Collections.Generic;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CyberBiker.Combat {
    [DefaultExecutionOrder(14500),DisallowMultipleComponent]
    public sealed class EnemyShotTrajectory : MonoBehaviour,IPooledObject {
        private static GameObject prototype;
        private static readonly List<EnemyShotTrajectory> flights=new List<EnemyShotTrajectory>(32);
        public static IReadOnlyList<EnemyShotTrajectory> InFlight=>flights;
        private static readonly int Intensity=Shader.PropertyToID("_Intensity"),EndFade=Shader.PropertyToID("_PlayerEndFade"),VisibleFraction=Shader.PropertyToID("_VisibleFraction");
        [SerializeField,HideInInspector] private bool template;
        private LineRenderer line;
        private MaterialPropertyBlock properties;
        private GameplayManager manager;
        private Enemy owner;
        private Transform muzzle,target;
        private Transform flightFrame;
        private Vector3 frameOrigin,frameEnd;
        private Bullet projectile;
        private int projectileLease;
        private bool rented,fired;
        private float previewRemaining,reachDistance;
        private Vector3 direction;
        public uint Generation {get;private set;}
        public bool Visible=>rented && line!=null && line.enabled;
        public bool IsPreview=>rented && !fired;
        public bool IsInFlight=>rented && fired;
        public Bullet Projectile=>projectile;
        public Vector3 Origin {get;private set;}
        public Vector3 EndPoint {get;private set;}
        public Color CurrentColor=>line!=null ? line.startColor : Color.clear;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatic(){prototype=null;flights.Clear();}
        private static GameObject GetPrototype(){
            var scene=SceneManager.GetActiveScene();
            if(prototype!=null && prototype.scene==scene)return prototype;
            foreach(var candidate in Object.FindObjectsByType<EnemyShotTrajectory>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(candidate.template && candidate.gameObject.scene==scene){prototype=candidate.gameObject;return prototype;}
            prototype=new GameObject("Enemy trajectory template (inactive)");prototype.SetActive(false);
            var renderer=prototype.AddComponent<LineRenderer>();renderer.positionCount=2;renderer.useWorldSpace=true;
            renderer.sharedMaterial=Resources.Load<Material>("EnemyAimLaser");renderer.alignment=LineAlignment.View;renderer.textureMode=LineTextureMode.Stretch;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
            prototype.AddComponent<EnemyShotTrajectory>().template=true;return prototype;
        }
        public static EnemyShotTrajectory Create(Enemy source,Transform gun,Transform player,float warningTime){
            var instance=PrefabPool.Rent(GetPrototype(),Vector3.zero,Quaternion.identity);
            var guide=instance.GetComponent<EnemyShotTrajectory>();guide.owner=source;guide.muzzle=gun;guide.target=player;
            guide.previewRemaining=Mathf.Max(.25f,warningTime+.5f);guide.UpdatePreview();guide.Draw();return guide;
        }
        private void Ensure(){
            if(line==null)line=GetComponent<LineRenderer>();
            if(properties==null)properties=new MaterialPropertyBlock();
            if(manager==null)manager=Object.FindAnyObjectByType<GameplayManager>();
        }
        public bool IsPreviewFor(Enemy source)=>IsPreview && owner==source;
        private void UpdatePreview(){
            if(muzzle==null || target==null)return;
            Origin=muzzle.position;EndPoint=target.position+Vector3.up*3;
        }
        public void Commit(ShotRequest shot,Vector3 aimPoint,Bullet bullet){
            if(!IsPreview || bullet==null){Retire();return;}
            Origin=shot.Origin;direction=shot.Direction.normalized;
            reachDistance=Mathf.Max(.01f,Vector3.Dot(aimPoint-Origin,direction));EndPoint=Origin+direction*reachDistance;
            // Enemy projectiles live under BulletLocal, which follows the rider.
            // Lock the shot in that frame so its guide travels with the bullet,
            // without following later muzzle movement or retargeting the player.
            flightFrame=bullet.transform.parent;
            frameOrigin=flightFrame!=null ? flightFrame.InverseTransformPoint(Origin) : Origin;
            frameEnd=flightFrame!=null ? flightFrame.InverseTransformPoint(EndPoint) : EndPoint;
            projectile=bullet;projectileLease=bullet.Lease;fired=true;muzzle=target=null;owner=null;
            if(!flights.Contains(this))flights.Add(this);Draw();
        }
        private void UpdateFlightFrame(){
            Origin=flightFrame!=null ? flightFrame.TransformPoint(frameOrigin) : frameOrigin;
            EndPoint=flightFrame!=null ? flightFrame.TransformPoint(frameEnd) : frameEnd;
            var segment=EndPoint-Origin;reachDistance=segment.magnitude;direction=segment.normalized;
        }
        public void CancelPreview(Enemy source,bool deferred=false){
            if(!IsPreviewFor(source))return;
            if(line!=null)line.enabled=false;
            if(deferred)PrefabPool.ReleaseAfterHierarchyChange(gameObject);else Retire();
        }
        private void LateUpdate(){
            if(!rented || template)return;
            if(manager==null || !manager.ProjectileCombat.enemyTrajectoryGuides){Retire();return;}
            if(manager.gameState==GameState.Pause){line.enabled=false;return;}
            if(manager.gameState!=GameState.Playing){Retire();return;}
            if(fired){
                UpdateFlightFrame();
                if(projectile==null || !projectile.IsHostile || projectile.Lease!=projectileLease || Vector3.Dot(projectile.transform.position-Origin,direction)>=reachDistance){Retire();return;}
            }else{
                previewRemaining-=Time.deltaTime;
                if(owner==null || !owner.gameObject.activeInHierarchy || !owner.IsAimingWeapons || muzzle==null || target==null || previewRemaining<=0){Retire();return;}
                UpdatePreview();
            }
            Draw();
        }
        private void Draw(){
            Ensure();if(line==null || manager==null)return;
            line.enabled=rented && fired && manager.gameState==GameState.Playing && manager.ProjectileCombat.enemyTrajectoryGuides;
            if(!line.enabled)return;
            var settings=manager.ProjectileCombat;var camera=Camera.main;
            float pixel=camera!=null ? Vector3.Distance(camera.transform.position,Origin)*2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/720f : 0;
            line.startWidth=line.endWidth=Mathf.Max(settings.enemyTrajectoryWidth,Mathf.Min(.08f,pixel*1.3f));
            var color=settings.enemyTrajectorySafeColor;color.a=settings.enemyTrajectoryOpacity;
            line.startColor=line.endColor=color;line.SetPosition(0,Origin);line.SetPosition(1,EndPoint);
            properties.SetFloat(Intensity,settings.enemyTrajectoryBrightness);properties.SetFloat(EndFade,settings.enemyTrajectoryEndFade);properties.SetFloat(VisibleFraction,1);line.SetPropertyBlock(properties);
        }
        private void Retire(){if(!rented)return;line.enabled=false;PrefabPool.Release(gameObject);}
        public void OnPoolRent(GameObject prefab){Ensure();Generation++;template=false;rented=true;fired=false;owner=null;muzzle=target=flightFrame=null;projectile=null;line.enabled=false;}
        public void OnPoolReturn(){flights.Remove(this);rented=false;fired=false;owner=null;muzzle=target=flightFrame=null;projectile=null;if(line!=null)line.enabled=false;}
        private void OnDisable(){flights.Remove(this);if(line!=null)line.enabled=false;}
    }
}
