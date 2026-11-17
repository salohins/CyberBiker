using UnityEngine;

[DisallowMultipleComponent]
public sealed class MuzzleFlashVisual : MonoBehaviour {
    private SpriteRenderer[] sprites;
    private Light[] lights;
    private Material material;
    private void Awake(){sprites=GetComponentsInChildren<SpriteRenderer>(true);lights=GetComponentsInChildren<Light>(true);material=Resources.Load<Material>("GunMuzzleFlash");}
    public void Configure(float brightness,float lightLevel){
        if(material==null)material=Resources.Load<Material>("GunMuzzleFlash");
        foreach(var sprite in sprites){if(material!=null)sprite.sharedMaterial=material;sprite.color=new Color(brightness,brightness,brightness,.8f);}
        foreach(var lamp in lights){lamp.intensity=lightLevel;lamp.range=4;lamp.shadows=LightShadows.None;}
    }
}
