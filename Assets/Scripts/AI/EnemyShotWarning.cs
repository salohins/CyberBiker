using CyberBiker.Audio;
using CyberBiker.Combat;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(13500),DisallowMultipleComponent]
public sealed class EnemyShotWarning : MonoBehaviour {
    private static readonly int Intensity=Shader.PropertyToID("_Intensity");
    private static readonly int PlayerEndFade=Shader.PropertyToID("_PlayerEndFade");
    private static readonly int VisibleFraction=Shader.PropertyToID("_VisibleFraction");
    private readonly LineRenderer[] lasers=new LineRenderer[2];
    private readonly RaycastHit[] hits=new RaycastHit[64];
    private readonly float[] shotRemaining=new float[2];
    private readonly int[] shotFrame=new int[2];
    private MaterialPropertyBlock brightness;
    private Material laserMaterial;
    private Enemy enemy;
    private Gameplay.GameplayManager manager;
    private Transform player;
    private float remaining;
    private int warningGun=-1;
    public bool Visible=>(lasers[0]!=null && lasers[0].enabled) || (lasers[1]!=null && lasers[1].enabled);
    public bool IsWarning=>remaining>0 && warningGun>=0;
    public float WarningRemaining=>remaining;
    public Color CurrentColor=>lasers[warningGun>=0 ? warningGun : 0]!=null ? lasers[warningGun>=0 ? warningGun : 0].startColor : Color.clear;
    public Vector3 Origin=>lasers[warningGun>=0 ? warningGun : 0]!=null ? lasers[warningGun>=0 ? warningGun : 0].GetPosition(0) : Vector3.zero;
    public Vector3 EndPoint=>lasers[warningGun>=0 ? warningGun : 0]!=null ? lasers[warningGun>=0 ? warningGun : 0].GetPosition(1) : Vector3.zero;
    private void Awake(){Ensure();}
    private void OnEnable(){Ensure();}
    private void Ensure(){
        if(brightness==null)brightness=new MaterialPropertyBlock();
        if(laserMaterial==null)laserMaterial=Resources.Load<Material>("EnemyAimLaser");
        enemy=GetComponent<Enemy>();manager=FindAnyObjectByType<Gameplay.GameplayManager>();
        if(player==null){var rider=GameObject.FindGameObjectWithTag("Player");if(rider!=null)player=rider.transform;}
        for(int i=0;i<lasers.Length;i++){
            if(lasers[i]!=null)continue;
            string label=i==0 ? "Enemy shot aim laser" : "Enemy shot aim laser 1";
            var existing=transform.Find(label);var root=existing!=null ? existing.gameObject : new GameObject(label);root.transform.SetParent(transform,false);
            var line=root.GetComponent<LineRenderer>();if(line==null)line=root.AddComponent<LineRenderer>();lasers[i]=line;
            line.sharedMaterial=laserMaterial;line.useWorldSpace=true;line.positionCount=2;line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;line.enabled=false;
        }
    }
    public void Begin(Transform origin,float seconds){
        if(lasers[0]==null || manager==null)Ensure();
        warningGun=enemy!=null && origin==enemy.GunMuzzle(1) ? 1 : 0;remaining=Mathf.Max(0,seconds);
        if(seconds>0 && origin!=null)SoundEvents.Emit(SoundCue.EnemyShotWarning,origin.position);
    }
    public void Fired(int gunIndex){
        if(manager==null)Ensure();
        if(gunIndex<0 || gunIndex>=shotRemaining.Length || manager==null)return;
        if(warningGun==gunIndex)Cancel();
        shotRemaining[gunIndex]=Mathf.Max(0,manager.ProjectileCombat.enemyAimLaserShotDimDuration)+Mathf.Max(.01f,manager.ProjectileCombat.enemyAimLaserShotRestore);
        shotFrame[gunIndex]=Time.frameCount;
    }
    private void LateUpdate(){
        if(manager==null || enemy==null){Hide();return;}
        if(manager.gameState!=Gameplay.GameState.Playing){Hide();return;}
        if(manager.ProjectileCombat.enemyTrajectoryGuides){Cancel();ClearShotResponse();Hide();return;}
        if(!enemy.IsAimingWeapons || player==null){Cancel();ClearShotResponse();Hide();return;}
        if(laserMaterial==null)laserMaterial=Resources.Load<Material>("EnemyAimLaser");
        // The firing coroutine owns the red window; it cannot expire one frame
        // early while WaitForSeconds is still waiting to release the projectile.
        if(IsWarning)remaining=Mathf.Max(.0001f,remaining-Time.deltaTime);
        var camera=Camera.main;
        for(int i=0;i<lasers.Length;i++){
            if(Time.frameCount!=shotFrame[i])shotRemaining[i]=Mathf.Max(0,shotRemaining[i]-Time.deltaTime);
            var line=lasers[i];if(line==null)continue;
            if(line.sharedMaterial!=laserMaterial)line.sharedMaterial=laserMaterial;
            var muzzle=enemy.GunMuzzle(i);line.enabled=manager.ProjectileCombat.enemyAimLaser && muzzle!=null;
            if(!line.enabled)continue;
            var origin=muzzle.position;var target=player.position+Vector3.up*3;var offset=target-origin;float distance=offset.magnitude;float fullDistance=distance;bool endsOnPlayer=false;var direction=offset.normalized;
            int count=Physics.RaycastNonAlloc(origin,direction,hits,distance,~0,QueryTriggerInteraction.Collide);
            for(int j=0;j<count;j++){
                var shape=hits[j].collider;
                if(shape.transform.IsChildOf(transform) || shape.GetComponentInParent<Bullet>()!=null || (shape.isTrigger && shape.GetComponentInParent<IDamageable>()==null))continue;
                if(hits[j].distance<distance){target=hits[j].point;distance=hits[j].distance;endsOnPlayer=shape.transform.IsChildOf(player);}
            }
            float pixel=camera!=null ? Vector3.Distance(camera.transform.position,origin)*2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/720f : 0;
            float width=Mathf.Max(manager.ProjectileCombat.enemyAimLaserWidth,Mathf.Min(.14f,pixel*2.4f));
            bool red=IsWarning && warningGun==i;
            if(!red)width=Mathf.Max(width*Mathf.Clamp(manager.ProjectileCombat.enemyAimLaserWhiteWidthMultiplier,.15f,.8f),Mathf.Min(.065f,pixel*.9f));
            var color=red ? manager.ProjectileCombat.enemyAimLaserWarningColor : manager.ProjectileCombat.enemyAimLaserIdleColor;color.a*=red ? 1 : IsWarning ? .4f : .9f;
            if(shotRemaining[i]>0){
                float restore=Mathf.Max(.01f,manager.ProjectileCombat.enemyAimLaserShotRestore);
                float recovery=1-Mathf.Clamp01(shotRemaining[i]/restore);
                color.a*=Mathf.Lerp(manager.ProjectileCombat.enemyAimLaserShotDimFactor,1,Mathf.SmoothStep(0,1,recovery));
            }
            line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.SetPosition(0,origin);line.SetPosition(1,target);
            line.sortingOrder=red ? 1 : 0;
            brightness.SetFloat(Intensity,manager.ProjectileCombat.enemyAimLaserBrightness);
            brightness.SetFloat(PlayerEndFade,manager.ProjectileCombat.enemyAimLaserPlayerFadeFraction);
            brightness.SetFloat(VisibleFraction,endsOnPlayer ? 1 : Mathf.Clamp01(distance/Mathf.Max(.001f,fullDistance)));line.SetPropertyBlock(brightness);
        }
    }
    public void Cancel(){remaining=0;warningGun=-1;}
    private void Hide(){foreach(var line in lasers)if(line!=null)line.enabled=false;}
    private void ClearShotResponse(){for(int i=0;i<shotRemaining.Length;i++)shotRemaining[i]=0;}
    private void OnDisable(){Cancel();ClearShotResponse();Hide();}
}
