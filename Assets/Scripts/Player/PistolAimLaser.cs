using CyberBiker.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.Player {
    [DefaultExecutionOrder(15000),DisallowMultipleComponent]
    public sealed class PistolAimLaser:MonoBehaviour {
        [SerializeField] private Material laserMaterial;
        [SerializeField,ColorUsage(true,false)] private Color laserColor=new Color(1,.025f,.015f,.9f);
        [SerializeField,Range(.01f,.08f)] private float beamWidth=.025f;
        [SerializeField,Range(.03f,.15f)] private float hitDotSize=.07f;
        [SerializeField,Range(20,200)] private float maximumDistance=120;
        private PlayerController player;
        private LineRenderer beam,dot;
        private readonly RaycastHit[] hits=new RaycastHit[64];
        public bool Visible => beam!=null && beam.enabled;
        public Vector3 Origin {get;private set;}
        public Vector3 EndPoint {get;private set;}
        public Collider HitCollider {get;private set;}
        private void Awake(){
            player=GetComponent<PlayerController>();if(laserMaterial==null)laserMaterial=Resources.Load<Material>("PlayerPistolLaser");
            beam=MakeLine("Pistol red aim laser",2);dot=MakeLine("Pistol laser hit dot",6);Hide();
        }
        private LineRenderer MakeLine(string name,int caps){
            var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;line.sharedMaterial=laserMaterial;
            line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;line.numCapVertices=caps;line.numCornerVertices=0;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;return line;
        }
        private void LateUpdate(){Refresh(Camera.main);}
        private void OnEnable(){Camera.onPreRender+=BeforeRender;}
        private void OnDisable(){Camera.onPreRender-=BeforeRender;Hide();}
        private void BeforeRender(Camera view){if(view.cameraType==CameraType.Game)Refresh(view);}
        private void Refresh(Camera view){
            if(player==null || laserMaterial==null || !player.TryGetPistolAim(out var origin,out var direction)){Hide();return;}
            Origin=origin;EndPoint=origin+direction*maximumDistance;HitCollider=null;float nearest=maximumDistance;
            int count=Physics.RaycastNonAlloc(origin,direction,hits,maximumDistance,~0,QueryTriggerInteraction.Collide);
            Vector3 normal=-direction;
            for(int i=0;i<count;i++){
                var hit=hits[i];var shape=hit.collider;
                if(shape.transform.IsChildOf(transform) || shape.GetComponentInParent<Bullet>()!=null || (shape.isTrigger && shape.GetComponentInParent<IDamageable>()==null))continue;
                if(hit.distance<nearest){nearest=hit.distance;EndPoint=hit.point;normal=hit.normal;HitCollider=shape;}
            }
            beam.enabled=true;beam.startColor=beam.endColor=laserColor;beam.startWidth=beamWidth;beam.endWidth=beamWidth*.65f;beam.SetPosition(0,origin);beam.SetPosition(1,EndPoint);
            dot.enabled=HitCollider!=null;dot.startColor=dot.endColor=laserColor;dot.startWidth=dot.endWidth=hitDotSize;
            var up=view!=null ? view.transform.up : Vector3.up;var center=EndPoint+normal*.008f;
            dot.SetPosition(0,center-up*hitDotSize*.1f);dot.SetPosition(1,center+up*hitDotSize*.1f);
        }
        private void Hide(){if(beam!=null)beam.enabled=false;if(dot!=null)dot.enabled=false;HitCollider=null;}
    }
}
