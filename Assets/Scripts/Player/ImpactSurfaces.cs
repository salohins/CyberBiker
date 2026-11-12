using UnityEngine;

namespace CyberBiker.Player {
    public static class ImpactSurfaces {
        public static bool IsCar(Collider surface) {
            if(surface==null || surface.isTrigger) return false;
            for(var parent=surface.transform;parent!=null;parent=parent.parent)
                if(parent.CompareTag("Obstacle") || parent.GetComponent<Car>()!=null) return true;
            return false;
        }
        public static bool IsBarrier(Collider surface,Vector3 normal) {
            if(surface==null || surface.isTrigger || surface.GetComponentInParent<PlayerController>()!=null) return false;
            bool worldObject=false;
            for(var parent=surface.transform;parent!=null;parent=parent.parent) {
                if(parent.CompareTag("Wall")) return true;
                worldObject|=parent.gameObject.layer==LayerMask.NameToLayer("WorldObject");
            }
            return worldObject && Mathf.Abs(normal.y)<.65f;
        }
        public static bool IsImpactSurface(Collider surface,Vector3 normal) => IsCar(surface) || IsBarrier(surface,normal);
    }
}
