using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.Combat {
    // An air wake only: never changes the original projectile renderers/scale.
    [DisallowMultipleComponent]
    public sealed class BulletAirWake : MonoBehaviour {
        private readonly LineRenderer[] ribbons=new LineRenderer[2];
        private readonly Vector3[] points=new Vector3[12];
        private ProjectileCombatSettings settings;
        private float age;
        private Bullet bullet;
        public bool Visible=>ribbons[0]!=null && ribbons[0].enabled;
        private void Awake(){
            bullet=GetComponent<Bullet>();
            var material=Resources.Load<Material>("BulletAirWake");
            for(int i=0;i<ribbons.Length;i++){
                var root=new GameObject("Bullet airflow "+i);root.transform.SetParent(transform,false);var line=root.AddComponent<LineRenderer>();ribbons[i]=line;line.positionCount=points.Length;line.useWorldSpace=true;line.sharedMaterial=material;line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;line.enabled=false;
                line.widthCurve=new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.15f,1),new Keyframe(.7f,.55f),new Keyframe(1,0));
            }
        }
        public void Configure(ProjectileCombatSettings value){settings=value;age=0;Refresh();}
        private void LateUpdate(){age+=Time.deltaTime;Refresh();}
        private void Refresh(){
            bool visible=settings!=null && settings.bulletAirWake;
            for(int strand=0;strand<ribbons.Length;strand++){
                var line=ribbons[strand];line.enabled=visible;if(!visible)continue;
                float length=Mathf.Min(settings.airWakeLength,.1f+age*(bullet!=null ? bullet.bulletSpeed : 70));
                for(int i=0;i<points.Length;i++){
                    float t=i/(float)(points.Length-1);float angle=t*5.5f-age*10+strand*Mathf.PI;
                    float radius=.08f+Mathf.Sin(t*Mathf.PI)*.16f;
                    points[i]=transform.position-transform.forward*length*t+(transform.right*Mathf.Cos(angle)+transform.up*Mathf.Sin(angle))*radius;
                }
                line.SetPositions(points);line.widthMultiplier=settings.airWakeWidth;
                line.startColor=new Color(.78f,.86f,.9f,settings.airWakeOpacity);line.endColor=new Color(.55f,.65f,.72f,0);
            }
        }
        public void Clear(){settings=null;foreach(var line in ribbons)if(line!=null)line.enabled=false;}
        private void OnDisable(){Clear();}
    }
}
