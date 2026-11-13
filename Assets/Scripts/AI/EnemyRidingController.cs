using UnityEngine;

// Presentation and clear-road lane preference only. Dodge-point navigation owns
// the motorcycle's heading and movement whenever an original path is available.
internal sealed class EnemyRidingController {
    private readonly Transform root;
    private readonly float phase;
    private float lateralSpeed;
    public float Lane {get;private set;}
    public Difficulty Settings {get;set;}
    public EnemyRidingController(Transform owner,float lane,Difficulty settings) {
        root=owner;Lane=Mathf.Clamp01(lane);Settings=settings;
        phase=((uint)owner.GetEntityId().GetHashCode()&65535)*(Mathf.PI*2/65536);
    }
    public void Assign(float lane,Difficulty settings){Lane=Mathf.Clamp01(lane);Settings=settings;}
    public Vector3 ClearRoadTarget(RoadLaneFrame frame,float ahead) {
        float laneWidth=(frame.max-frame.min)/Mathf.Max(1,frame.tile.carLines.Length-1);
        float weave=Settings!=null ? Mathf.Min(Mathf.Max(0,Settings.enemyLaneWeave),laneWidth*.22f)*Mathf.Sin(Time.time*1.7f+phase) : 0;
        float current=Vector3.Dot(root.position-frame.origin,frame.right);
        float desired=Mathf.Lerp(frame.min,frame.max,Lane)+weave;
        return root.position+frame.forward*ahead+frame.right*(Mathf.Clamp(desired,frame.min,frame.max)-current);
    }
    public void Feedback(Vector3 velocity,RoadLaneFrame frame){lateralSpeed=Vector3.Dot(velocity,frame.right);}
    public void ApplyVisual(Transform bike,float dt) {
        if(Settings==null)return;
        float amount=Mathf.Clamp(lateralSpeed/Mathf.Max(1,Settings.enemyLateralSpeed),-1,1);
        // This rotation affects only the mesh. Never turn the navigation root
        // toward last frame's velocity instead of its selected dodge point.
        bike.localRotation=Quaternion.Slerp(bike.localRotation,Quaternion.Euler(0,-amount*Settings.enemyDriftAngle,-amount*Settings.enemyRidingLean),1-Mathf.Exp(-dt*11));
    }
}
