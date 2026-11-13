using System.Collections.Generic;
using UnityEngine;

namespace CyberBiker.Pooling {
    [DisallowMultipleComponent]
    public sealed class PooledInstance : MonoBehaviour {
        internal PrefabPool.Bucket Owner {get;private set;}
        public bool IsRented {get;private set;}
        public uint Generation {get;private set;}
        public GameObject Prefab => Owner!=null ? Owner.prefab : null;
        public bool IsCached => cached;
        private bool cached;
        private Transform[] nodes;
        private Vector3[] positions,scales;
        private Quaternion[] rotations;
        private bool[] active;
        private int[] layers;
        private Renderer[] renderers;
        private bool[] rendererEnabled;
        private Collider[] colliders;
        private bool[] colliderEnabled,triggers;
        private ParticleSystem[] particles;
        private bool[] playParticles;
        private TrailRenderer[] trails;
        private IPooledObject[] callbacks;
        private Behaviour[] behaviours;
        private bool[] behaviourEnabled;
        internal void Initialize(PrefabPool.Bucket owner){
            Owner=owner;nodes=GetComponentsInChildren<Transform>(true);positions=new Vector3[nodes.Length];scales=new Vector3[nodes.Length];rotations=new Quaternion[nodes.Length];active=new bool[nodes.Length];layers=new int[nodes.Length];
            for(int i=0;i<nodes.Length;i++){positions[i]=nodes[i].localPosition;scales[i]=nodes[i].localScale;rotations[i]=nodes[i].localRotation;active[i]=nodes[i].gameObject.activeSelf;layers[i]=nodes[i].gameObject.layer;}
            renderers=GetComponentsInChildren<Renderer>(true);rendererEnabled=new bool[renderers.Length];for(int i=0;i<renderers.Length;i++)rendererEnabled[i]=renderers[i].enabled;
            colliders=GetComponentsInChildren<Collider>(true);colliderEnabled=new bool[colliders.Length];triggers=new bool[colliders.Length];for(int i=0;i<colliders.Length;i++){colliderEnabled[i]=colliders[i].enabled;triggers[i]=colliders[i].isTrigger;}
            particles=GetComponentsInChildren<ParticleSystem>(true);playParticles=new bool[particles.Length];for(int i=0;i<particles.Length;i++)playParticles[i]=particles[i].main.playOnAwake;
            trails=GetComponentsInChildren<TrailRenderer>(true);
            // The pool owns retirement; an empty trail must not destroy a child
            // that the next projectile lease still needs.
            foreach(var trail in trails)trail.autodestruct=false;
            behaviours=GetComponentsInChildren<Behaviour>(true);behaviourEnabled=new bool[behaviours.Length];for(int i=0;i<behaviours.Length;i++)behaviourEnabled[i]=behaviours[i].enabled;
            var list=new List<IPooledObject>();foreach(var script in GetComponents<MonoBehaviour>())if(script is IPooledObject callback)list.Add(callback);callbacks=list.ToArray();
        }
        internal void Rent(Vector3 position,Quaternion rotation,Transform parent){
            Generation++;IsRented=true;transform.SetParent(parent,false);transform.SetPositionAndRotation(position,rotation);transform.localScale=Owner.prefab.transform.localScale;
            for(int i=0;i<nodes.Length;i++)if(nodes[i]!=null)nodes[i].gameObject.layer=layers[i];
            for(int i=1;i<nodes.Length;i++)if(nodes[i]!=null){nodes[i].SetLocalPositionAndRotation(positions[i],rotations[i]);nodes[i].localScale=scales[i];nodes[i].gameObject.SetActive(active[i]);}
            for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=rendererEnabled[i];
            for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null){colliders[i].enabled=colliderEnabled[i];colliders[i].isTrigger=triggers[i];}
            foreach(var trail in trails)if(trail!=null)trail.Clear();
            for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null && behaviours[i].enabled!=behaviourEnabled[i])behaviours[i].enabled=behaviourEnabled[i];
            foreach(var callback in callbacks)callback.OnPoolRent(Owner.prefab);
            gameObject.SetActive(true);
            for(int i=0;i<particles.Length;i++)if(particles[i]!=null && playParticles[i])particles[i].Play(false);
        }
        internal void Return(){
            IsRented=false;
            foreach(var callback in callbacks)callback.OnPoolReturn();
            foreach(var particle in particles)if(particle!=null)particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var trail in trails)if(trail!=null)trail.Clear();
            gameObject.SetActive(false);
        }
        internal void EnterCache(){cached=true;Owner.retained++;Owner.scope.retained[Owner.kind]++;}
        internal void LeaveCache(){if(!cached)return;cached=false;Owner.retained--;Owner.scope.retained[Owner.kind]--;}
        private void OnDestroy(){if(Owner!=null)LeaveCache();}
    }
}
