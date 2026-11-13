using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Bounded body tint with edge-only emission preserves the baked textures.
internal sealed class EnemyFinisherGlow : IDisposable {
    private readonly Enemy enemy;
    private readonly Material material;
    private readonly List<(Renderer source,Renderer glow)> renderers=new List<(Renderer,Renderer)>();
    private float phase;
    private Color color;
    private bool active;
    public int VisibleRendererCount { get; private set; }
    public Color CurrentColor => color;

    public EnemyFinisherGlow(Enemy owner,Shader shader) {
        enemy=owner;
        if(shader==null || !shader.isSupported)return;
        material=new Material(shader){name="Finisher highlight (instance)",hideFlags=HideFlags.DontSave};
        foreach(var source in owner.GetComponentsInChildren<Renderer>(true)) {
            if(source.gameObject.name=="Finisher glow"){source.gameObject.SetActive(false);UnityEngine.Object.Destroy(source.gameObject);continue;}
            Mesh mesh=null;Renderer overlay=null;
            if(source is SkinnedMeshRenderer skin && skin.sharedMesh!=null) {
                mesh=skin.sharedMesh;
                var go=new GameObject("Finisher glow");go.transform.SetParent(source.transform,false);
                var copy=go.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=mesh;copy.bones=skin.bones;copy.rootBone=skin.rootBone;
                copy.localBounds=skin.localBounds;copy.quality=skin.quality;copy.updateWhenOffscreen=skin.updateWhenOffscreen;overlay=copy;
            } else if(source is MeshRenderer && source.GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh!=null) {
                mesh=filter.sharedMesh;
                var go=new GameObject("Finisher glow",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(source.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;overlay=go.GetComponent<MeshRenderer>();
            }
            if(overlay==null)continue;
            overlay.gameObject.layer=source.gameObject.layer;
            var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=material;
            overlay.sharedMaterials=materials;overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;
            overlay.lightProbeUsage=LightProbeUsage.Off;overlay.reflectionProbeUsage=ReflectionProbeUsage.Off;overlay.enabled=false;
            renderers.Add((source,overlay));
        }
        color=owner.FinisherBlueColor;
    }
    public void Tick(float dt,float intensity,float radiance) {
        if(material==null)return;
        if(!enemy.IsFinisherReady){Hide();return;}
        if(!active){phase=0;active=true;}
        float urgency=1-Mathf.Clamp01(enemy.FinisherTimeRemaining/Mathf.Max(1,enemy.FinisherWindow));
        phase+=dt*Mathf.Lerp(enemy.FinisherFlashStartRate,enemy.FinisherFlashEndRate,urgency*urgency);
        // Alternate the two saturated colors rather than blending them into a
        // pale intermediate. Distance has no part in this warning signal.
        color=(Mathf.FloorToInt(phase)&1)==0 ? enemy.FinisherBlueColor : enemy.FinisherOrangeColor;
        material.SetColor("_HighlightColor",color);material.SetFloat("_Intensity",Mathf.Clamp01(intensity));
        material.SetFloat("_GlowStrength",Mathf.Clamp(radiance,0,1.5f));
        material.SetFloat("_Pulse",.875f+.125f*Mathf.Sin(phase*Mathf.PI*2));material.SetFloat("_Phase",phase);
        VisibleRendererCount=0;
        foreach(var pair in renderers) {
            if(pair.glow==null || pair.source==null)continue;
            pair.glow.enabled=pair.source.enabled && pair.source.gameObject.activeInHierarchy;
            if(pair.glow.enabled)VisibleRendererCount++;
            if(pair.source is SkinnedMeshRenderer source && pair.glow is SkinnedMeshRenderer target)
                for(int i=0;i<source.sharedMesh.blendShapeCount;i++)target.SetBlendShapeWeight(i,source.GetBlendShapeWeight(i));
        }
    }
    public void Hide(){if(!active && VisibleRendererCount==0)return;active=false;VisibleRendererCount=0;foreach(var pair in renderers)if(pair.glow!=null)pair.glow.enabled=false;}
    public void Dispose(){foreach(var pair in renderers)if(pair.glow!=null)UnityEngine.Object.Destroy(pair.glow.gameObject);if(material!=null)UnityEngine.Object.Destroy(material);}
}
