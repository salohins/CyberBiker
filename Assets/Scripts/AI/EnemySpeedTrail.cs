using UnityEngine;
using UnityEngine.Rendering;

// Reuse the authored trail anchors; one shared material, no extra renderers.
internal sealed class EnemySpeedTrail {
    private readonly TrailRenderer[] trails;
    private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
    private static readonly int Brightness=Shader.PropertyToID("_Brightness"), Bottom=Shader.PropertyToID("_RiderBottom"),
        Top=Shader.PropertyToID("_RiderTop"), Radius=Shader.PropertyToID("_RiderRadius");
    private bool hidden;
    public bool Visible {get {foreach(var trail in trails)if(trail!=null && trail.enabled && trail.emitting)return true;return false;}}
    public EnemySpeedTrail(GameObject enemy) {
        trails=enemy.GetComponentsInChildren<TrailRenderer>(true);
        var material=Resources.Load<Material>("EnemySpeedTrail");
        var width=new AnimationCurve(new Keyframe(0,1),new Keyframe(.15f,1),new Keyframe(.65f,.7f),new Keyframe(1,0));
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.16f,.055f),0),new GradientColorKey(new Color(.85f,.025f,.04f),1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.95f,.35f),new GradientAlphaKey(.55f,.75f),new GradientAlphaKey(0,1)});
        foreach(var trail in trails) {
            if(material!=null)trail.sharedMaterial=material;
            trail.widthCurve=width;trail.colorGradient=gradient;trail.minVertexDistance=.18f;
            trail.numCornerVertices=2;trail.numCapVertices=2;trail.alignment=LineAlignment.View;trail.textureMode=LineTextureMode.Stretch;
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.autodestruct=false;trail.Clear();
        }
    }
    public void Tick(Transform rider,float speed,float lifetime,float maxLength,float width,float brightness,bool suppress) {
        if(suppress){Hide();return;}
        foreach(var trail in trails) {
            if(trail==null)continue;
            if(hidden){trail.Clear();trail.enabled=true;trail.emitting=true;}
            trail.time=Mathf.Min(Mathf.Max(.02f,lifetime),Mathf.Max(1,maxLength)/Mathf.Max(1,Mathf.Abs(speed)));
            trail.widthMultiplier=Mathf.Max(.01f,width);
            trail.GetPropertyBlock(block);block.SetFloat(Brightness,Mathf.Max(0,brightness));
            block.SetVector(Bottom,rider!=null ? rider.position+Vector3.up*.35f : Vector3.zero);
            block.SetVector(Top,rider!=null ? rider.position+Vector3.up*2.8f : Vector3.zero);
            block.SetFloat(Radius,rider!=null ? 1.25f : 0);trail.SetPropertyBlock(block);
        }
        hidden=false;
    }
    public void Hide() {
        if(hidden)return;
        foreach(var trail in trails)if(trail!=null){trail.emitting=false;trail.Clear();trail.enabled=false;}
        hidden=true;
    }
}
