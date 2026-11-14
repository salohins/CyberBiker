using System.Collections.Generic;
using UnityEngine;

namespace CyberBiker.Pooling {
    // Scene-owned, outside any weapon/rider hierarchy. Sleeps while idle.
    [DefaultExecutionOrder(32000)]
    public sealed class DeferredPoolReturns : MonoBehaviour {
        private List<(PooledInstance member,uint generation)> pending=new List<(PooledInstance,uint)>(8);
        private List<(PooledInstance member,uint generation)> processing=new List<(PooledInstance,uint)>(8);

        internal void Schedule(PooledInstance member){
            pending.Add((member,member.Generation));enabled=true;
        }
        private void LateUpdate(){
            var batch=processing;processing=pending;pending=batch;
            foreach(var request in processing){
                // Another owner may already have returned and rented it again.
                // A deferred request must never retire that newer lease.
                var member=request.member;
                if(member!=null && member.IsRented && member.Generation==request.generation)
                    PrefabPool.Release(member.gameObject);
            }
            processing.Clear();enabled=pending.Count>0;
        }
    }
}
