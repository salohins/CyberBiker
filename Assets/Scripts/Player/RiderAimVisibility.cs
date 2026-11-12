using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace CyberBiker.Player {
    public sealed class RiderAimVisibility : IDisposable {
        [Serializable]
        public sealed class Settings {
            public bool enabled=true;
            [Range(0,.8f)] public float obstructedOpacity=.2f;
            [Tooltip("Seconds to reach the transparent appearance.")]
            [Range(.03f,.75f)] public float fadeOutTime=.22f;
            [Tooltip("Seconds to restore full visibility, beginning as soon as the view clears.")]
            [Range(.05f,1)] public float fadeInTime=.18f;
            [Tooltip("Also fade when the rider covers the camera centre, including while a target is selected.")]
            [FormerlySerializedAs("fadeDuringShoulderCrossing")]
            public bool fadeNearCameraCenter=true;
        }
        private sealed class Binding {
            public SkinnedMeshRenderer renderer;
            public Material[] originals,faded;
        }
        private readonly List<Binding> bindings=new List<Binding>();
        private readonly Dictionary<Material,Material> variants=new Dictionary<Material,Material>();
        private readonly BikeAutoAim aim;
        private readonly Shader shader;
        private readonly RiderMeshRaycaster riderRays;
        private float opacity=1,fadeAmount;
        private bool swapped;
        private Camera depthCamera;
        private CommandBuffer depthBuffer;
        private RenderTexture frontDepth;
        public float Opacity => opacity;
        public bool BlockingView {get;private set;}

        public RiderAimVisibility(PlayerController player,Shader fadeShader) {
            aim=player.GetComponent<BikeAutoAim>();shader=fadeShader;
            riderRays=new RiderMeshRaycaster(player);
            foreach(var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                // The motorcycle is a MeshRenderer; the pistol is a protected
                // material slot inside PlayerMesh, so neither is faded.
                bindings.Add(new Binding {renderer=renderer,originals=renderer.sharedMaterials});
            }
        }
        public void Update(Camera camera,Settings settings,bool aiming,float dt) {
            if(dt<=0) return;
            BlockingView=false;
            if(settings.enabled && aiming) {
                if(aim!=null && aim.Target!=null) BlockingView=BlocksTarget(camera,aim.TargetViewPoint);
                // The centre check stays active throughout Aim. Target lock or
                // shoulder-switch velocity must not suppress a centred rider.
                if(!BlockingView && settings.fadeNearCameraCenter)
                    BlockingView=riderRays.Raycast(new Ray(camera.transform.position,camera.transform.forward),camera.farClipPlane);
            }
            bool fade=settings.enabled && aiming && BlockingView;
            float time=fade ? settings.fadeOutTime : settings.fadeInTime;
            fadeAmount=Mathf.MoveTowards(fadeAmount,fade?1:0,dt/Mathf.Max(.03f,time));
            opacity=Mathf.Lerp(1,settings.obstructedOpacity,Mathf.SmoothStep(0,1,fadeAmount));
            if(fadeAmount==0) {Restore();return;}
            if(shader==null) return;
            EnsureVariants();
            foreach(var pair in variants) {
                var original=pair.Key;var material=pair.Value;
                material.SetFloat("_RiderOpacity",opacity);
                // TeleportController continues to own its dissolve animation.
                foreach(var property in TeleportProperties) if(original.HasProperty(property)) material.SetFloat(property,original.GetFloat(property));
                if(original.HasProperty("_ObjectBasePos")) material.SetVector("_ObjectBasePos",original.GetVector("_ObjectBasePos"));
            }
            UpdateDepthSurface(camera);
        }
        private static readonly string[] TeleportProperties={"_FadeRate","_ObjectHeight","_ObjectFadeHeight","_RisePower","_SpreadPower","_TwistPower"};
        private bool BlocksTarget(Camera camera,Vector3 target) {
            var viewport=camera.WorldToViewportPoint(target);
            if(viewport.z<=camera.nearClipPlane || viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1)return false;
            // Include a small patch around the enemy's aim point: the rider can
            // hide part of the enemy without covering its exact centre pixel.
            return CastTo(camera,target)
                || CastTo(camera,target+camera.transform.up*.4f)
                || CastTo(camera,target-camera.transform.up*.4f)
                || CastTo(camera,target+camera.transform.right*.3f)
                || CastTo(camera,target-camera.transform.right*.3f);
        }
        private bool CastTo(Camera camera,Vector3 point) {
            var delta=point-camera.transform.position;
            return riderRays.Raycast(new Ray(camera.transform.position,delta.normalized),delta.magnitude-.05f);
        }
        private static bool IsPistol(Material material) => material!=null && material.name.StartsWith("Gun",StringComparison.OrdinalIgnoreCase);
        private void UpdateDepthSurface(Camera camera) {
            if(depthBuffer==null) {
                depthCamera=camera;depthBuffer=new CommandBuffer {name="Rider fade front surface"};
                camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,depthBuffer);
            }
            depthBuffer.Clear();
            if(frontDepth==null || frontDepth.width!=camera.pixelWidth || frontDepth.height!=camera.pixelHeight) {
                if(frontDepth!=null){frontDepth.Release();UnityEngine.Object.Destroy(frontDepth);}
                frontDepth=new RenderTexture(Mathf.Max(1,camera.pixelWidth),Mathf.Max(1,camera.pixelHeight),24,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear) {
                    name="Rider front surface depth",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp
                };
                frontDepth.Create();
                foreach(var material in variants.Values){material.SetTexture("_RiderFrontDepth",frontDepth);material.SetFloat("_UseFrontSurfaceDepth",1);}
            }
            // Keep rider depth separate from scene depth, so transparent road
            // lights, glass and particles behind the faded rider still draw.
            depthBuffer.SetRenderTarget(frontDepth);
            depthBuffer.ClearRenderTarget(true,true,Color.white*(camera.farClipPlane+1));
            // Establish the closest rider surface across ALL its materials and
            // both meshes before blending. Hidden layers cannot accumulate.
            foreach(var binding in bindings) {
                if(binding.renderer==null || !binding.renderer.enabled || !binding.renderer.gameObject.activeInHierarchy)continue;
                for(int i=0;i<binding.faded.Length;i++) {
                    if(binding.faded[i]==null || IsPistol(binding.originals[i]))continue;
                    depthBuffer.DrawRenderer(binding.renderer,binding.faded[i],i,binding.faded[i].FindPass("RIDER_DEPTH"));
                }
            }
            depthBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        }
        private void EnsureVariants() {
            if(swapped)return;
            foreach(var binding in bindings) {
                if(binding.renderer==null)continue;
                if(binding.faded==null) {
                    binding.faded=(Material[])binding.originals.Clone();
                    for(int i=0;i<binding.faded.Length;i++) {
                        var original=binding.originals[i];
                        if(original==null || IsPistol(original))continue;
                        if(!variants.TryGetValue(original,out var material)) {
                            material=new Material(original) {name=original.name+" (Aim view fade)",shader=shader,renderQueue=3000};
                            bool teleport=original.shader.name.StartsWith("TeleportFade/",StringComparison.Ordinal);
                            material.SetFloat("_UseUnlit",teleport?0:1);
                            if(!original.HasProperty("_MainTex"))material.SetTexture("_MainTex",Texture2D.whiteTexture);
                            variants.Add(original,material);
                        }
                        binding.faded[i]=material;
                    }
                }
                binding.renderer.sharedMaterials=binding.faded;
            }
            swapped=true;
        }
        private void Restore() {
            depthBuffer?.Clear();
            if(!swapped)return;
            foreach(var binding in bindings)if(binding.renderer!=null)binding.renderer.sharedMaterials=binding.originals;
            swapped=false;
        }
        public void Reset() {opacity=1;fadeAmount=0;BlockingView=false;Restore();}
        public void Dispose() {
            riderRays.Dispose();
            Restore();foreach(var material in variants.Values)UnityEngine.Object.Destroy(material);variants.Clear();
            if(depthBuffer!=null) {
                if(depthCamera!=null)depthCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,depthBuffer);
                depthBuffer.Release();depthBuffer=null;
            }
            if(frontDepth!=null){frontDepth.Release();UnityEngine.Object.Destroy(frontDepth);frontDepth=null;}
        }
    }
}
