using UnityEngine;

// A last-contact safeguard using exactly the planner's clearance. A separate
// rectangular bike envelope can reject routes already approved by the traffic
// layout's capsule test, trapping the rider before its next turn-angle node.
internal sealed class EnemyTrafficAvoidance {
    private readonly Transform root;
    private readonly AI.AIController planner;
    private readonly RaycastHit[] hits=new RaycastHit[16];
    private readonly int mask;
    public EnemyTrafficAvoidance(GameObject owner):this(owner,Object.FindAnyObjectByType<AI.AIController>()){}
    public EnemyTrafficAvoidance(GameObject owner,AI.AIController navigation){root=owner.transform;planner=navigation;mask=LayerMask.GetMask("WorldObject");}
    private float Radius=>planner!=null ? Mathf.Max(0,planner.NavigationRadius) : 1;
    private Bounds Shape()=>new Bounds(root.position,Vector3.one*(Radius*2));
    public Vector3 Constrain(Vector3 next,float dt) {
        var current=root.position;var remaining=next-current;
        for(int pass=0;pass<3 && remaining.sqrMagnitude>.000001f;pass++) {
            float length=remaining.magnitude;var direction=remaining/length;
            int count=Physics.SphereCastNonAlloc(current,Radius,direction,hits,length,mask,QueryTriggerInteraction.UseGlobal);
            int nearest=-1;float distance=length;
            for(int i=0;i<count;i++) {
                var car=hits[i].collider.GetComponentInParent<Car>();
                if(car==null || !car.IsAlive || hits[i].distance>=distance)continue;
                distance=hits[i].distance;nearest=i;
            }
            if(nearest<0){current+=remaining;break;}
            var advance=direction*Mathf.Max(0,distance-.001f);current+=advance;
            var normal=Vector3.ProjectOnPlane(hits[nearest].normal,Vector3.up);
            if(normal.sqrMagnitude<.0001f)break;
            remaining=Vector3.ProjectOnPlane(remaining-advance,normal.normalized);
        }
        return current;
    }
}
