#if UNITY_EDITOR
using CyberBiker.Combat;
using UnityEngine;

// Sample after both BulletWorld.Update and the guide's LateUpdate, as rendered.
[DefaultExecutionOrder(15000)]
public sealed class EnemyTrajectoryMotionProbe : MonoBehaviour {
    public Rigidbody Rider;
    public Enemy Source;
    public EnemyShotTrajectory Guide;
    public int Samples {get;private set;}
    public float Travel {get;private set;}
    public float MaximumError {get;private set;}
    public string Failure {get;private set;}
    private Vector3 initialOrigin;
    private float elapsed;
    public void Begin(Rigidbody rider,Enemy source,EnemyShotTrajectory guide){
        Rider=rider;Source=source;Guide=guide;initialOrigin=guide.Origin;
        Samples=0;Travel=MaximumError=elapsed=0;Failure=null;
    }
    private void FixedUpdate(){
        if(Guide==null || !Guide.IsInFlight || Rider==null)return;
        elapsed+=Time.fixedDeltaTime;
        var motion=new Vector3(Mathf.Sin(elapsed*9)*8,0,elapsed<.35f ? 70 : 110)*Time.fixedDeltaTime;
        var position=Rider.position+motion;Rider.transform.position=position;Rider.position=position;Physics.SyncTransforms();
        if(Source!=null)Source.transform.position+=motion+Vector3.right*Time.fixedDeltaTime*6;
    }
    private void LateUpdate(){
        if(Guide==null || !Guide.IsInFlight || !Guide.Visible)return;
        var bullet=Guide.Projectile;if(bullet==null || !bullet.IsHostile)return;
        Samples++;
        var direction=(Guide.EndPoint-Guide.Origin).normalized;
        float error=Vector3.Cross(bullet.transform.position-Guide.Origin,direction).magnitude;
        var renderer=Guide.GetComponent<LineRenderer>();
        error=Mathf.Max(error,Vector3.Distance(renderer.GetPosition(0),Guide.Origin),Vector3.Distance(renderer.GetPosition(1),Guide.EndPoint));
        MaximumError=Mathf.Max(MaximumError,error);Travel=Mathf.Abs(Guide.Origin.z-initialOrigin.z);
        if(error>.01f)Failure="Rendered guide left its bullet by "+error+" world units at sample "+Samples;
    }
}

#endif
