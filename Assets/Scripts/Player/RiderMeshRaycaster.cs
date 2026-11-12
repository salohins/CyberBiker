using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberBiker.Player {
    // Query the animated rider directly. No collision shapes or layers are
    // changed, and the motorcycle/pistol never participate in these raycasts.
    public sealed class RiderMeshRaycaster : IDisposable {
        private sealed class Surface {
            public SkinnedMeshRenderer renderer;
            public Material[] materials;
            public Mesh snapshot;
            public readonly List<Vector3> vertices=new List<Vector3>();
            public List<int> indices;
            public int frame=-1;

            public void Bake() {
                if(frame==Time.frameCount)return;
                if(snapshot==null) {
                    snapshot=new Mesh {name="Rider sight raycast mesh",hideFlags=HideFlags.HideAndDontSave};
                    snapshot.MarkDynamic();
                }
                // Compensate for renderer scale before applying localToWorld.
                // Without this, a scaled player would be scaled a second time.
                renderer.BakeMesh(snapshot,true);snapshot.GetVertices(vertices);frame=Time.frameCount;
                if(indices!=null)return;
                indices=new List<int>();var submesh=new List<int>();
                for(int i=0;i<snapshot.subMeshCount;i++) {
                    if(i<materials.Length && materials[i]!=null && materials[i].name.StartsWith("Gun",StringComparison.OrdinalIgnoreCase))continue;
                    if(snapshot.GetTopology(i)!=MeshTopology.Triangles)continue;
                    snapshot.GetTriangles(submesh,i);indices.AddRange(submesh);
                }
            }
        }
        private readonly List<Surface> surfaces=new List<Surface>();

        public RiderMeshRaycaster(PlayerController player) {
            foreach(var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                surfaces.Add(new Surface {renderer=renderer,materials=renderer.sharedMaterials});
        }

        public bool Raycast(Ray ray,float distance) {
            if(distance<=0)return false;
            foreach(var surface in surfaces) {
                var renderer=surface.renderer;
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sharedMesh==null)continue;
                // Broad phase only: padding cannot trigger fading. Animated
                // triangles must actually intersect the ray below.
                var bounds=renderer.bounds;bounds.Expand(2);
                if(!bounds.Contains(ray.origin) && (!bounds.IntersectRay(ray,out float entry) || entry>distance))continue;
                surface.Bake();
                var matrix=renderer.transform.worldToLocalMatrix;
                var start=matrix.MultiplyPoint3x4(ray.origin);
                var delta=matrix.MultiplyVector(ray.direction*distance);
                var vertices=surface.vertices;var indices=surface.indices;
                for(int i=0;i<indices.Count;i+=3)
                    if(Intersect(start,delta,vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]))return true;
            }
            return false;
        }
        private static bool Intersect(Vector3 origin,Vector3 delta,Vector3 a,Vector3 b,Vector3 c) {
            var ab=b-a;var ac=c-a;var p=Vector3.Cross(delta,ac);float determinant=Vector3.Dot(ab,p);
            if(Mathf.Abs(determinant)<.0000001f)return false;
            float inverse=1/determinant;var offset=origin-a;float u=Vector3.Dot(offset,p)*inverse;
            if(u<0 || u>1)return false;
            var q=Vector3.Cross(offset,ab);float v=Vector3.Dot(delta,q)*inverse;
            if(v<0 || u+v>1)return false;
            float t=Vector3.Dot(ac,q)*inverse;return t>=0 && t<=1;
        }
        public void Dispose() {
            foreach(var surface in surfaces)if(surface.snapshot!=null)UnityEngine.Object.Destroy(surface.snapshot);
            surfaces.Clear();
        }
    }
}
