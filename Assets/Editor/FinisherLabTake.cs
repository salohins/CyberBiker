using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace CyberBiker.EditorTools {
    // Records rendered scene state, not video. Seeking restores 3D poses and full
    // particle state, so it can go backwards and orbit effects after their source dies.
    public sealed class FinisherLabTake : IDisposable {
        public const int MaximumFrames=1200;
        public const long MaximumBytes=256L*1024*1024;
        private const int Layer=31;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class Node {public Transform source,copy;}
        private struct Pose {public Vector3 position,scale;public Quaternion rotation;}
        private sealed class Visual {public Renderer source,copy;public ParticleSystem particles;public Mesh mesh;}
        private sealed class Geometry {public Vector3[] vertices,normals;public Vector2[] uv;public Color[] colors;public int[] triangles;}
        private sealed class ParticleFrame {
            public ParticleSystem.Particle[] particles;public ParticleSystem.PlaybackState playback;
            public ParticleSystem.Trails trails;public bool hasTrails;public List<Vector4> custom1,custom2;
        }
        private sealed class VisualFrame {
            public int id;public int[] materials;public int particleTrail=-1;public MaterialPropertyBlock block;public Geometry mesh;
            public float[] blendShapes;
            public Vector3[] line;public Gradient gradient;public float width;public bool worldLine;
            public ParticleFrame particles;
        }
        private sealed class Lamp {public Light source,copy;}
        private struct LampFrame {public int id;public Color color;public float intensity,range;}
        private struct ScreenFrame {public Shader shader;public float dark,desaturation,flash,side;public Vector2 center;public Vector3 impact;}
        private sealed class Frame {
            public float time,progress,fov,worldSpeed;public BikeSwordCombat.CombatPhase phase;
            public Pose[] nodes;public VisualFrame[] visuals;public LampFrame[] lights;
            public FinisherLabTakeMaterials.State[] materials;
            public Vector3 focus,cameraPosition;public Quaternion cameraRotation;public ScreenFrame screen;
            public int particles,hits,explosions;public long bytes;
        }
        private readonly PreviewRenderUtility preview;
        private readonly Scene scene;
        private readonly List<Node> nodes=new List<Node>();
        private readonly Dictionary<Transform,int> nodeLookup=new Dictionary<Transform,int>();
        private readonly List<Visual> visuals=new List<Visual>();
        private readonly Dictionary<Renderer,int> visualLookup=new Dictionary<Renderer,int>();
        private readonly List<Lamp> lamps=new List<Lamp>();
        private readonly Dictionary<Light,int> lampLookup=new Dictionary<Light,int>();
        private readonly List<Frame> frames=new List<Frame>();
        private readonly FinisherLabTakeMaterials palette=new FinisherLabTakeMaterials();
        private readonly RenderTexture sceneTarget,output;
        private readonly PostProcessLayer post;
        private Material screenMaterial;
        private float yaw,pitch=15,distance=9;
        private Vector3 pan;
        private bool orbit;
        private int selected=-1;
        public int FrameCount=>frames.Count;
        public float Duration=>frames.Count>0 ? frames[frames.Count-1].time : 0;
        public float SelectedTime=>selected>=0 ? frames[selected].time : 0;
        public string SelectedPhase=>selected>=0 ? frames[selected].phase.ToString() : "";
        public int SelectedParticleCount=>selected>=0 ? frames[selected].particles : 0;
        public int SelectedFrame=>selected;
        public float SelectedProgress=>selected>=0 ? frames[selected].progress : 0;
        public float SelectedWorldSpeed=>selected>=0 ? frames[selected].worldSpeed : 1;
        public int RenderedParticleCount=>visuals.Where(value=>value.copy.enabled && value.particles!=null).Sum(value=>value.particles.particleCount);
        public float ImpactTime=>frames.Find(value=>value.hits>0)?.time ?? -1;
        public float ExplosionTime=>frames.Find(value=>value.explosions>0)?.time ?? -1;
        public int PhaseFrameCount(BikeSwordCombat.CombatPhase phase)=>frames.Count(value=>value.phase==phase);
        public bool ObjectVisible(string rootName)=>visuals.Any(value=>value.copy.enabled && value.copy.transform.root.name==rootName);
        public long EstimatedBytes {get;private set;}
        public bool IsFull=>frames.Count>=MaximumFrames || EstimatedBytes>=MaximumBytes;
        public bool Orbiting=>orbit;
        public RenderTexture Texture=>output;
        public FinisherLabTake(Camera source) {
            preview=new PreviewRenderUtility();scene=preview.camera.scene;
            foreach(var light in preview.lights)light.enabled=false;
            preview.camera.gameObject.SetActive(false);preview.camera.CopyFrom(source);preview.camera.scene=scene;preview.camera.cameraType=CameraType.Preview;
            preview.camera.cullingMask=1<<Layer;preview.camera.enabled=false;
            var sourcePost=source.GetComponent<PostProcessLayer>();
            if(sourcePost!=null) {
                post=preview.camera.gameObject.AddComponent<PostProcessLayer>();EditorUtility.CopySerialized(sourcePost,post);
                post.volumeLayer=1<<Layer;post.volumeTrigger=preview.camera.transform;
                foreach(var volume in UnityEngine.Object.FindObjectsByType<PostProcessVolume>()) {
                    if(volume.gameObject.scene!=source.gameObject.scene || !volume.isGlobal)continue;
                    var obj=NewObject("Recorded post processing");var clone=obj.AddComponent<PostProcessVolume>();EditorUtility.CopySerialized(volume,clone);
                }
            }
            preview.camera.gameObject.SetActive(true);
            sceneTarget=new RenderTexture(1280,720,24,source.allowHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32){name="Finisher take scene",hideFlags=HideFlags.HideAndDontSave};sceneTarget.Create();
            output=new RenderTexture(1280,720,0){name="Finisher take view",hideFlags=HideFlags.HideAndDontSave};output.Create();
            preview.camera.targetTexture=sceneTarget;
        }
        private GameObject NewObject(string name) {var obj=new GameObject(name){hideFlags=HideFlags.HideAndDontSave,layer=Layer};SceneManager.MoveGameObjectToScene(obj,scene);return obj;}
        private Transform NodeFor(Transform source) {
            if(nodeLookup.TryGetValue(source,out int existing))return nodes[existing].copy;
            var parent=source.parent!=null ? NodeFor(source.parent) : null;
            var node=NewObject(source.name).transform;node.SetParent(parent,false);
            nodeLookup.Add(source,nodes.Count);nodes.Add(new Node{source=source,copy=node});return node;
        }
        private int Register(Renderer source) {
            if(visualLookup.TryGetValue(source,out int existing))return existing;
            var node=NodeFor(source.transform);var visual=new Visual{source=source};
            if(source is SkinnedMeshRenderer skin) {
                var copy=node.gameObject.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(skin,copy);
                copy.bones=skin.bones.Select(bone=>bone!=null ? NodeFor(bone) : null).ToArray();copy.rootBone=skin.rootBone!=null ? NodeFor(skin.rootBone) : null;
                copy.updateWhenOffscreen=true;copy.forceMatrixRecalculationPerRender=true;visual.copy=copy;
            } else if(source is ParticleSystemRenderer particleRenderer) {
                var system=source.GetComponent<ParticleSystem>();var copy=node.gameObject.AddComponent<ParticleSystem>();
                copy.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);EditorUtility.CopySerialized(system,copy);copy.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=copy.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                if(system.main.customSimulationSpace!=null)main.customSimulationSpace=NodeFor(system.main.customSimulationSpace);
                var emit=copy.emission;emit.enabled=false;var sub=copy.subEmitters;sub.enabled=false;
                copy.useAutoRandomSeed=false;copy.randomSeed=system.randomSeed;
                copy.Play(false);copy.Pause(false);visual.particles=copy;
                visual.copy=copy.GetComponent<ParticleSystemRenderer>();EditorUtility.CopySerialized(particleRenderer,visual.copy);
            } else if(source is LineRenderer line) {
                visual.copy=node.gameObject.AddComponent<LineRenderer>();EditorUtility.CopySerialized(line,visual.copy);
            } else if(source is TrailRenderer trail) {
                var copy=node.gameObject.AddComponent<LineRenderer>();copy.useWorldSpace=true;copy.widthCurve=trail.widthCurve;
                copy.widthMultiplier=trail.widthMultiplier;copy.colorGradient=trail.colorGradient;copy.alignment=trail.alignment;
                copy.textureMode=trail.textureMode;copy.numCapVertices=trail.numCapVertices;copy.numCornerVertices=trail.numCornerVertices;visual.copy=copy;
            } else if(source is MeshRenderer meshRenderer && source.GetComponent<MeshFilter>()!=null) {
                var filter=node.gameObject.AddComponent<MeshFilter>();var sourceMesh=source.GetComponent<MeshFilter>().sharedMesh;
                if(sourceMesh!=null && !AssetDatabase.Contains(sourceMesh))visual.mesh=new Mesh{name="Recorded "+sourceMesh.name,hideFlags=HideFlags.HideAndDontSave};
                filter.sharedMesh=visual.mesh!=null ? visual.mesh : sourceMesh;
                visual.copy=node.gameObject.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(meshRenderer,visual.copy);
            } else return -1;
            visual.copy.enabled=false;visual.copy.sortingLayerID=source.sortingLayerID;visual.copy.sortingOrder=source.sortingOrder;
            int id=visuals.Count;visuals.Add(visual);visualLookup.Add(source,id);return id;
        }
        public void Capture(float time,Camera camera,PlayerController player,BikeSwordCombat sword,SwordFinisherEffects effects) {
            if(IsFull)return;
            var frame=new Frame{time=time,worldSpeed=Time.timeScale,phase=sword.Phase,progress=sword.Phase==BikeSwordCombat.CombatPhase.Striking ? sword.SwingProgress : FinisherLabPreview.PhaseProgress,
                focus=player.transform.position+Vector3.up*1.8f,cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation,fov=camera.fieldOfView,hits=sword.HitCount,explosions=effects.CarriedExplosionCount};
            var captured=new List<VisualFrame>();
            foreach(var source in UnityEngine.Object.FindObjectsByType<Renderer>()) {
                if(source.gameObject.scene!=player.gameObject.scene || !source.enabled || !source.gameObject.activeInHierarchy)continue;
                int id=Register(source);if(id<0)continue;var visual=visuals[id];
                var state=new VisualFrame{id=id,materials=source.sharedMaterials.Select(palette.Register).ToArray()};
                if(source is ParticleSystemRenderer particlesRenderer)state.particleTrail=palette.Register(particlesRenderer.trailMaterial);
                if(source is SkinnedMeshRenderer skin) {
                    int count=skin.sharedMesh!=null ? skin.sharedMesh.blendShapeCount : 0;state.blendShapes=new float[count];
                    for(int shape=0;shape<count;shape++)state.blendShapes[shape]=skin.GetBlendShapeWeight(shape);
                }
                var block=new MaterialPropertyBlock();source.GetPropertyBlock(block);if(!block.isEmpty)state.block=block;
                if(visual.mesh!=null) {
                    var mesh=source.GetComponent<MeshFilter>().sharedMesh;
                    state.mesh=new Geometry{vertices=mesh.vertices,normals=mesh.normals,uv=mesh.uv,colors=mesh.colors,triangles=mesh.triangles};
                    frame.bytes+=state.mesh.vertices.Length*48L+state.mesh.triangles.Length*4L;
                }
                if(source is LineRenderer line) {
                    state.line=new Vector3[line.positionCount];line.GetPositions(state.line);state.gradient=line.colorGradient;state.width=line.widthMultiplier;state.worldLine=line.useWorldSpace;
                } else if(source is TrailRenderer trail) {
                    state.line=new Vector3[trail.positionCount];trail.GetPositions(state.line);state.gradient=trail.colorGradient;state.width=trail.widthMultiplier;state.worldLine=true;
                }
                if(visual.particles!=null) {
                    var system=source.GetComponent<ParticleSystem>();
                    var particles=new ParticleSystem.Particle[system.particleCount];int count=system.GetParticles(particles);if(count!=particles.Length)Array.Resize(ref particles,count);
                    var data=new ParticleFrame{particles=particles,playback=system.GetPlaybackState(),hasTrails=system.trails.enabled};
                    if(data.hasTrails)data.trails=system.GetTrails();
                    if(system.customData.enabled){data.custom1=new List<Vector4>();data.custom2=new List<Vector4>();system.GetCustomParticleData(data.custom1,ParticleSystemCustomData.Custom1);system.GetCustomParticleData(data.custom2,ParticleSystemCustomData.Custom2);}
                    state.particles=data;frame.particles+=count;frame.bytes+=count*256L+1024;
                }
                if(state.line!=null)frame.bytes+=state.line.Length*16L+128;
                frame.bytes+=state.materials.Length*4L+128;captured.Add(state);
            }
            var lightStates=new List<LampFrame>();
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>()) {
                if(light.gameObject.scene!=player.gameObject.scene || !light.enabled || !light.gameObject.activeInHierarchy)continue;
                if(!lampLookup.TryGetValue(light,out int id)) {
                    var copy=NodeFor(light.transform).gameObject.AddComponent<Light>();EditorUtility.CopySerialized(light,copy);copy.cullingMask=1<<Layer;copy.enabled=false;
                    id=lamps.Count;lampLookup.Add(light,id);lamps.Add(new Lamp{source=light,copy=copy});
                }
                lightStates.Add(new LampFrame{id=id,color=light.color,intensity=light.intensity,range=light.range});
            }
            frame.nodes=new Pose[nodes.Count];
            for(int i=0;i<nodes.Count;i++) {
                var source=nodes[i].source;
                if(source!=null)frame.nodes[i]=new Pose{position=source.localPosition,rotation=source.localRotation,scale=source.localScale};
                else frame.nodes[i]=new Pose{position=nodes[i].copy.localPosition,rotation=nodes[i].copy.localRotation,scale=nodes[i].copy.localScale};
            }
            frame.visuals=captured.ToArray();frame.lights=lightStates.ToArray();frame.materials=palette.Capture(out long materialBytes);
            var screen=camera.GetComponent<SwordImpactScreen>();
            if(screen!=null) {
                var type=typeof(SwordImpactScreen);var material=(Material)type.GetField("material",Private).GetValue(screen);
                frame.screen=new ScreenFrame{shader=material!=null ? material.shader : null,dark=(float)type.GetField("darkness",Private).GetValue(screen),desaturation=(float)type.GetField("desaturation",Private).GetValue(screen),
                    flash=(float)type.GetField("flash",Private).GetValue(screen),side=(float)type.GetField("side",Private).GetValue(screen),center=(Vector2)type.GetField("center",Private).GetValue(screen),
                    impact=(Vector3)typeof(SwordFinisherEffects).GetField("hitPosition",Private).GetValue(effects)};
            }
            frame.bytes+=frame.nodes.Length*40L+materialBytes+lightStates.Count*32L+256;EstimatedBytes+=frame.bytes;frames.Add(frame);
        }
        public int Nearest(float time) {
            if(frames.Count==0)return -1;int lo=0,hi=frames.Count-1;
            while(lo<hi){int mid=(lo+hi)/2;if(frames[mid].time<time)lo=mid+1;else hi=mid;}
            return lo>0 && Mathf.Abs(frames[lo-1].time-time)<Mathf.Abs(frames[lo].time-time) ? lo-1 : lo;
        }
        public void Seek(float time){Restore(Nearest(time));}
        public void Step(int direction){Restore(Mathf.Clamp(selected+direction,0,frames.Count-1));}
        private void Restore(int index) {
            if(index<0 || index>=frames.Count)return;selected=index;var frame=frames[index];
            for(int i=0;i<frame.nodes.Length;i++){var pose=frame.nodes[i];var copy=nodes[i].copy;copy.localPosition=pose.position;copy.localRotation=pose.rotation;copy.localScale=pose.scale;}
            foreach(var visual in visuals)visual.copy.enabled=false;foreach(var lamp in lamps)lamp.copy.enabled=false;
            palette.Restore(frame.materials);
            foreach(var state in frame.visuals) {
                var visual=visuals[state.id];visual.copy.enabled=true;visual.copy.sharedMaterials=state.materials.Select(palette.Get).ToArray();visual.copy.SetPropertyBlock(state.block);
                if(visual.copy is ParticleSystemRenderer particlesRenderer)particlesRenderer.trailMaterial=palette.Get(state.particleTrail);
                if(state.blendShapes!=null){var skin=(SkinnedMeshRenderer)visual.copy;for(int shape=0;shape<state.blendShapes.Length;shape++)skin.SetBlendShapeWeight(shape,state.blendShapes[shape]);}
                if(state.mesh!=null) {
                    var mesh=visual.mesh;mesh.Clear();mesh.vertices=state.mesh.vertices;
                    if(state.mesh.normals.Length==state.mesh.vertices.Length)mesh.normals=state.mesh.normals;
                    if(state.mesh.uv.Length==state.mesh.vertices.Length)mesh.uv=state.mesh.uv;
                    if(state.mesh.colors.Length==state.mesh.vertices.Length)mesh.colors=state.mesh.colors;
                    mesh.triangles=state.mesh.triangles;mesh.RecalculateBounds();
                }
                if(state.line!=null) {
                    var line=(LineRenderer)visual.copy;line.useWorldSpace=state.worldLine;line.positionCount=state.line.Length;line.SetPositions(state.line);line.colorGradient=state.gradient;line.widthMultiplier=state.width;
                }
                if(state.particles!=null) {
                    var system=visual.particles;var data=state.particles;
                    system.Pause(false);system.SetPlaybackState(data.playback);system.SetParticles(data.particles,data.particles.Length);
                    if(data.hasTrails)system.SetTrails(data.trails);
                    if(data.custom1!=null){system.SetCustomParticleData(data.custom1,ParticleSystemCustomData.Custom1);system.SetCustomParticleData(data.custom2,ParticleSystemCustomData.Custom2);}
                    system.Pause(false);
                }
            }
            foreach(var state in frame.lights){var light=lamps[state.id].copy;light.color=state.color;light.intensity=state.intensity;light.range=state.range;light.enabled=true;}
            Render();
        }
        public void Orbit(float horizontal,float vertical){orbit=true;yaw+=horizontal;pitch=Mathf.Clamp(pitch+vertical,-65,80);Render();}
        public void Zoom(float multiplier){orbit=true;distance=Mathf.Clamp(distance*multiplier,2,35);Render();}
        public void Pan(float horizontal,float vertical){orbit=true;pan+=preview.camera.transform.right*horizontal+preview.camera.transform.up*vertical;Render();}
        public void GameplayCamera(){orbit=false;pan=Vector3.zero;Render();}
        public void CameraAngle(float degrees){orbit=true;yaw=degrees;pan=Vector3.zero;Render();}
        public void Render() {
            if(selected<0)return;var frame=frames[selected];var camera=preview.camera;
            if(orbit){var focus=frame.focus+pan;camera.transform.position=focus+Quaternion.Euler(pitch,yaw,0)*Vector3.back*distance;camera.transform.LookAt(focus);camera.fieldOfView=50;}
            else {camera.transform.SetPositionAndRotation(frame.cameraPosition,frame.cameraRotation);camera.fieldOfView=frame.fov;}
            camera.aspect=1280f/720;post?.ResetHistory();camera.Render();
            var screen=frame.screen;
            if(screen.shader!=null && Mathf.Max(screen.dark,screen.flash)>.001f) {
                if(screenMaterial==null || screenMaterial.shader!=screen.shader){if(screenMaterial!=null)UnityEngine.Object.DestroyImmediate(screenMaterial);screenMaterial=new Material(screen.shader){hideFlags=HideFlags.HideAndDontSave};}
                Vector2 center=screen.center;
                if(orbit){var point=camera.WorldToViewportPoint(screen.impact);center=point.z>0 ? new Vector2(Mathf.Clamp(point.x,.15f,.85f),Mathf.Clamp(point.y,.2f,.8f)) : new Vector2(.5f,.5f);}
                screenMaterial.SetVector("_Impact",new Vector4(screen.dark,screen.desaturation,screen.flash,screen.side));screenMaterial.SetVector("_Contact",new Vector4(center.x,center.y,1280f/720,0));Graphics.Blit(sceneTarget,output,screenMaterial);
            } else Graphics.Blit(sceneTarget,output);
        }
        public float FirstPhaseTime(BikeSwordCombat.CombatPhase phase){var frame=frames.Find(value=>value.phase==phase);return frame!=null ? frame.time : -1;}
        public string StateFingerprint() {
            if(selected<0)return "";
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                Action<float> number=value=>writer.Write(Mathf.RoundToInt(value*10000));
                Action<Vector3> vector=value=>{number(value.x);number(value.y);number(value.z);};
                for(int i=0;i<frames[selected].nodes.Length;i++){var node=nodes[i].copy;vector(node.localPosition);vector(node.localScale);var q=node.localRotation;number(q.x);number(q.y);number(q.z);number(q.w);}
                foreach(var visual in visuals)if(visual.copy.enabled && visual.particles!=null) {
                    var particles=new ParticleSystem.Particle[visual.particles.particleCount];visual.particles.GetParticles(particles);writer.Write(particles.Length);
                    foreach(var particle in particles){vector(particle.position);vector(particle.velocity);number(particle.remainingLifetime);writer.Write(particle.randomSeed);}
                }
                writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray()));
            }
        }
        public void Dispose() {
            preview.camera.targetTexture=null;preview.Cleanup();
            foreach(var visual in visuals)if(visual.mesh!=null)UnityEngine.Object.DestroyImmediate(visual.mesh);
            palette.Dispose();if(screenMaterial!=null)UnityEngine.Object.DestroyImmediate(screenMaterial);
            sceneTarget.Release();output.Release();UnityEngine.Object.DestroyImmediate(sceneTarget);UnityEngine.Object.DestroyImmediate(output);
            frames.Clear();nodes.Clear();visuals.Clear();lamps.Clear();
        }
    }
}
