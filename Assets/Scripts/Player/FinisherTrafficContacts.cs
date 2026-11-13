using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberBiker.Player {
    // Only alter this bike's collision pairs. Cars stay solid for other actors.
    public sealed class FinisherTrafficContacts : IDisposable {
        private struct Pair {public Collider bike,traffic;}
        private readonly Transform bikeRoot;
        private readonly Collider[] shapes;
        private readonly List<Pair> pairs=new List<Pair>(32);
        private Collider[] nearby=new Collider[64];
        public FinisherTrafficContacts(GameObject bike){bikeRoot=bike.transform;shapes=bike.GetComponentsInChildren<Collider>(true);}
        public static bool IsTraffic(Collider shape) => shape!=null && !shape.isTrigger &&
            (shape.GetComponentInParent<Car>()!=null || shape.GetComponentInParent<Obstacle>()!=null || shape.CompareTag("Obstacle"));
        public bool Contains(Collider shape) {
            for(int i=0;i<pairs.Count;i++)if(pairs[i].traffic==shape)return true;
            return false;
        }
        private static bool Active(Collider shape)=>shape!=null && shape.enabled && !shape.isTrigger && shape.gameObject.activeInHierarchy;
        public void ProtectPath(Vector3 from,Vector3 to) {
            var bounds=new Bounds(from,Vector3.zero);
            foreach(var shape in shapes)if(Active(shape))bounds.Encapsulate(shape.bounds);
            bounds.Encapsulate(new Bounds(bounds.center+to-from,bounds.size));bounds.Expand(2);
            int count;
            while((count=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents,nearby,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))==nearby.Length)
                Array.Resize(ref nearby,nearby.Length*2);
            for(int i=0;i<count;i++) {
                var traffic=nearby[i];
                if(!IsTraffic(traffic) || traffic.transform.IsChildOf(bikeRoot))continue;
                foreach(var bike in shapes) {
                    if(!Active(bike))continue;
                    bool owned=false;
                    for(int p=0;p<pairs.Count;p++)if(pairs[p].bike==bike && pairs[p].traffic==traffic){owned=true;break;}
                    if(!owned && Physics.GetIgnoreCollision(bike,traffic))continue;
                    // Reapply for pooled colliders that were disabled/re-enabled.
                    Physics.IgnoreCollision(bike,traffic,true);
                    if(!owned)pairs.Add(new Pair{bike=bike,traffic=traffic});
                }
            }
        }
        public void ReleaseClearPairs(bool protecting) {
            for(int i=pairs.Count-1;i>=0;i--) {
                var pair=pairs[i];
                if(pair.bike==null || pair.traffic==null){pairs.RemoveAt(i);continue;}
                bool active=Active(pair.bike) && Active(pair.traffic);
                if(active && protecting)continue;
                var clearance=pair.bike.bounds;clearance.Expand(.6f);
                // Never turn collisions back on inside an overlapping car.
                if(active && clearance.Intersects(pair.traffic.bounds))continue;
                Physics.IgnoreCollision(pair.bike,pair.traffic,false);pairs.RemoveAt(i);
            }
        }
        public void Dispose() {
            foreach(var pair in pairs)if(pair.bike!=null && pair.traffic!=null)Physics.IgnoreCollision(pair.bike,pair.traffic,false);
            pairs.Clear();
        }
    }
}
