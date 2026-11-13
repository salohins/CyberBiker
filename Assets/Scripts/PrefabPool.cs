using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberBiker.Pooling {
    public interface IPooledObject {
        void OnPoolRent(GameObject prefab);
        void OnPoolReturn();
    }

    // Scene-owned, bounded inactive caches. Active demand is never capped.
    public static class PrefabPool {
        internal sealed class Scope {
            internal Transform storage;
            internal readonly Dictionary<GameObject,Bucket> buckets=new Dictionary<GameObject,Bucket>();
            internal readonly int[] retained=new int[4];
            internal bool disposed;
        }
        internal sealed class Bucket {
            internal Scope scope;
            internal GameObject prefab;
            internal int kind,limit,retained;
            internal readonly Stack<PooledInstance> free=new Stack<PooledInstance>();
        }
        private static readonly Dictionary<Scene,Scope> scopes=new Dictionary<Scene,Scope>();
        private static readonly int[] budgets={96,16,32,8};
        public static int CreatedCount {get;private set;}
        public static int ReusedCount {get;private set;}
        public static int ReturnedCount {get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize(){scopes.Clear();CreatedCount=ReusedCount=ReturnedCount=0;SceneManager.sceneUnloaded-=Unload;SceneManager.sceneUnloaded+=Unload;}
        private static void Unload(Scene scene){if(scopes.TryGetValue(scene,out var scope)){scope.disposed=true;scope.buckets.Clear();scopes.Remove(scene);}}
        private static Bucket GetBucket(GameObject prefab,Scene scene){
            if(!scopes.TryGetValue(scene,out var scope) || scope.storage==null){
                scope=new Scope();var root=new GameObject("Runtime pool (inactive)");SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);scope.storage=root.transform;scopes[scene]=scope;
            }
            if(!scope.buckets.TryGetValue(prefab,out var bucket)){
                int kind=prefab.GetComponent<Bullet>()!=null ? 0 : prefab.GetComponent<Car>()!=null ? 2 : prefab.GetComponent<Tile>()!=null ? 3 : 1;
                bucket=new Bucket{scope=scope,prefab=prefab,kind=kind,limit=kind==0 ? 64 : kind==3 ? 2 : 4};scope.buckets.Add(prefab,bucket);
            }
            return bucket;
        }
        public static GameObject Rent(GameObject prefab,Vector3 position,Quaternion rotation,Transform parent=null){
            var scene=parent!=null ? parent.gameObject.scene : SceneManager.GetActiveScene();var bucket=GetBucket(prefab,scene);PooledInstance member=null;
            while(bucket.free.Count>0 && member==null){member=bucket.free.Pop();if(member!=null){member.LeaveCache();ReusedCount++;}}
            if(member==null){
                var go=Object.Instantiate(prefab,bucket.scope.storage,false);go.SetActive(false);member=go.AddComponent<PooledInstance>();member.Initialize(bucket);CreatedCount++;
            }
            member.Rent(position,rotation,parent);return member.gameObject;
        }
        public static void Release(GameObject instance){
            if(instance==null)return;
            var member=instance.GetComponent<PooledInstance>();
            if(member==null){Object.Destroy(instance);return;}
            if(member.Owner==null){Object.Destroy(instance);return;}
            if(!member.IsRented)return;
            var bucket=member.Owner;member.Return();ReturnedCount++;
            if(bucket.scope.disposed || bucket.scope.storage==null){Object.Destroy(instance);return;}
            instance.transform.SetParent(bucket.scope.storage,false);
            if(bucket.retained>=bucket.limit || bucket.scope.retained[bucket.kind]>=budgets[bucket.kind]){Object.Destroy(instance);return;}
            member.EnterCache();bucket.free.Push(member);
        }
    }
}
