using System.Collections.Generic;
using CyberBiker.Pooling;
using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.Scoring {
    // Actual world-space canvases: camera movement produces perspective and
    // parallax. Numbers inherit action motion and never clamp to screen edges.
    public sealed class WorldStyleNumbers:MonoBehaviour {
        private sealed class Number {
            public Canvas canvas;public RectTransform rect;public CanvasGroup group;public Text text;public WorldScorePlate plate;
            public Transform anchor;public Rigidbody body;public Enemy enemy;public PooledInstance lease;public uint generation;
            public Vector3 position,offset,velocity;public Color tint;public float age=100,riseAge,scale;public int points;public StyleAction action;
        }
        private readonly Number[] pool=new Number[8];
        private readonly List<Canvas> canvases=new List<Canvas>(8);
        private StyleScoreController owner;
        private Transform holder;
        private PlayerController player;
        private Rigidbody playerBody;
        private int cursor;
        public IReadOnlyList<Canvas> Canvases=>canvases;
        public int ActiveCount {get {int count=0;foreach(var item in pool)if(item!=null && item.age<Duration)count++;return count;}}
        public RectTransform FirstActive {get {foreach(var item in pool)if(item!=null && item.age<Duration)return item.rect;return null;}}
        private StyleScoringSettings Settings=>owner.Settings;
        private float Duration=>Mathf.Max(.3f,Settings.worldNumberLifetime);
        private bool Motion=>!Settings.reducedMotion && Settings.animationIntensity>.001f;
        public void Initialize(StyleScoreController source) {
            owner=source;holder=new GameObject("3D style numbers").transform;holder.gameObject.layer=5;
            var font=Settings.popupTypeface;if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");font.RequestCharactersInTexture("+0123456789",64,FontStyle.Normal);
            for(int i=0;i<pool.Length;i++) {
                var go=new GameObject("World score "+i,typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));go.layer=5;var rect=go.GetComponent<RectTransform>();rect.SetParent(holder,false);rect.sizeDelta=new Vector2(360,96);
                var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=0;canvas.enabled=false;
                var group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
                var backing=new GameObject("Dark backing",typeof(RectTransform),typeof(CanvasRenderer),typeof(WorldScorePlate));backing.layer=5;var plate=backing.GetComponent<WorldScorePlate>();plate.rectTransform.SetParent(rect,false);plate.rectTransform.sizeDelta=new Vector2(150,76);plate.raycastTarget=false;plate.color=new Color(.006f,.014f,.023f,Settings.worldNumberBackgroundOpacity);
                var label=new GameObject("Points",typeof(RectTransform),typeof(Text),typeof(Outline));label.layer=5;var labelRect=label.GetComponent<RectTransform>();labelRect.SetParent(rect,false);labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
                var text=label.GetComponent<Text>();text.font=font;text.fontSize=64;text.fontStyle=FontStyle.Normal;text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.supportRichText=false;text.raycastTarget=false;
                var outline=label.GetComponent<Outline>();outline.effectColor=new Color(0,.012f,.025f,.9f);outline.effectDistance=new Vector2(1.3f,-1.3f);
                pool[i]=new Number{canvas=canvas,rect=rect,group=group,text=text,plate=plate};canvases.Add(canvas);
            }
            Camera.onPreCull+=FaceCamera;
        }
        public void Emit(StyleAction action,int points,Vector3 point,Color color) {
            if(!isActiveAndEnabled || !Settings.enabled || !Settings.showHud || !Settings.worldPopups)return;
            if(player==null){player=FindAnyObjectByType<PlayerController>();if(player!=null)playerBody=player.GetComponent<Rigidbody>();}
            var view=Camera.main;if(view==null)return;
            Enemy target=null;Transform anchor=player!=null ? player.transform : null;
            bool enemyAction=action==StyleAction.PistolHit || action==StyleAction.PistolKill || action==StyleAction.SwordFinisher;
            if(enemyAction) {
                float best=36;
                foreach(var candidate in Enemy.Instances)if(candidate!=null && candidate.gameObject.activeInHierarchy){float distance=(candidate.transform.position-point).sqrMagnitude;if(distance<best){best=distance;target=candidate;anchor=candidate.transform;}}
            } else if(player!=null && (action==StyleAction.BulletDodge || action==StyleAction.PerfectDodge || action==StyleAction.GapPass)) {
                float side=Mathf.Sign(Vector3.Dot(point-player.transform.position,view.transform.right));if(side==0)side=cursor%2==0 ? 1 : -1;
                point=player.transform.position+view.transform.right*side*2.2f+Vector3.up*2;
            }
            Vector3 velocity=target!=null ? action==StyleAction.SwordFinisher ? target.SwordDefeatVelocity : new Vector3(target.LateralSpeed,0,target.ForwardSpeed) : playerBody!=null ? playerBody.linearVelocity : Vector3.zero;
            var lease=anchor!=null ? anchor.GetComponentInParent<PooledInstance>() : null;uint generation=lease!=null ? lease.Generation : 0;
            foreach(var old in pool)if(old.age<.22f && old.action==action && old.anchor==anchor && old.generation==generation && ((anchor!=null ? anchor.position+old.offset : old.position)-point-Settings.worldNumberOffset).sqrMagnitude<9) {
                old.points+=points;SetNumber(old,old.points);old.age=Mathf.Min(old.age,.16f);return;
            }
            var item=pool[cursor];cursor=(cursor+1)%pool.Length;
            point+=Settings.worldNumberOffset;
            // Keep simultaneous awards around one actor in separate world rows.
            for(int attempt=0;attempt<4;attempt++) {
                bool crowded=false;foreach(var old in pool)if(old!=item && old.age<Duration*.7f && old.anchor==anchor && Mathf.Abs(old.position.y-point.y)<.42f && (old.position-point).sqrMagnitude<9){crowded=true;break;}
                if(!crowded)break;point.y+=.48f;
            }
            item.action=action;item.points=points;item.age=item.riseAge=0;item.anchor=anchor;item.enemy=target;item.body=target==null ? playerBody : null;item.lease=lease;item.generation=generation;
            item.position=point;item.offset=anchor!=null ? point-anchor.position : Vector3.zero;item.velocity=velocity;item.scale=Settings.worldNumberScale*(enemyAction ? 1 : .8f);
            SetNumber(item,points);item.tint=color;item.text.color=Tint(color,0);item.rect.position=point;item.rect.rotation=view.transform.rotation;item.rect.localScale=Vector3.one*item.scale;item.canvas.worldCamera=view;item.canvas.enabled=true;item.group.alpha=0;
        }
        private void LateUpdate() {
            if(owner==null)return;
            var view=Camera.main;if(view==null)return;
            bool paused=owner.State==Gameplay.GameState.Pause;
            Refresh(view,paused ? 0 : Time.unscaledDeltaTime,paused ? 0 : Time.deltaTime);
        }
        private void Refresh(Camera view,float animationDelta,float worldDelta) {
            if(!Settings.enabled || !Settings.showHud || !Settings.worldPopups){Clear();return;}
            foreach(var item in pool) {
                if(item.age>=Duration){item.canvas.enabled=false;continue;}
                item.age+=animationDelta;item.riseAge+=animationDelta;
                bool anchored=item.anchor!=null && item.anchor.gameObject.activeInHierarchy && (item.lease==null || item.lease.IsRented && item.lease.Generation==item.generation);
                if(anchored) {
                    item.position=item.anchor.position+item.offset;
                    if(item.enemy!=null)item.velocity=item.action==StyleAction.SwordFinisher ? item.enemy.SwordDefeatVelocity : new Vector3(item.enemy.LateralSpeed,0,item.enemy.ForwardSpeed);
                    else if(item.body!=null)item.velocity=item.body.linearVelocity;
                }else{item.anchor=null;item.enemy=null;item.body=null;item.position+=item.velocity*worldDelta;}
                float progress=Mathf.Clamp01(item.age/Duration);
                float rise=Motion ? Settings.worldNumberRise*(1-Mathf.Exp(-item.riseAge*3))*Settings.animationIntensity : 0;
                item.rect.position=item.position+Vector3.up*rise;item.rect.rotation=view.transform.rotation;item.text.color=Tint(item.tint,item.age);item.plate.color=new Color(.006f,.014f,.023f,Settings.worldNumberBackgroundOpacity);
                float alpha=Mathf.Clamp01(item.age/.07f)*Mathf.Clamp01((1-progress)/.32f);
                float depth=view.WorldToViewportPoint(item.rect.position).z;
                item.rect.localScale=Vector3.one*(item.scale*Mathf.Clamp(depth/Mathf.Max(6,Settings.worldNumberNearDistance),.08f,1));
                item.group.alpha=depth>view.nearClipPlane+.2f ? alpha*Mathf.Clamp01((depth-1.25f)/1.25f) : 0;
                item.canvas.enabled=item.age<Duration;item.canvas.worldCamera=view;
            }
        }
        private static void SetNumber(Number item,int points){item.text.text="+"+points;item.plate.rectTransform.sizeDelta=new Vector2(Mathf.Max(80,item.text.preferredWidth+22),64);}
        private Color Tint(Color value,float age){
            float settled=Mathf.Clamp01(Settings.worldNumberBrightness);
            float peak=Motion ? Mathf.Max(settled,Mathf.Clamp01(Settings.worldNumberAppearanceBrightness)) : settled;
            float brightness=Mathf.Lerp(peak,settled,Mathf.SmoothStep(0,1,age/Mathf.Max(.05f,Settings.worldNumberGlowSettle)));
            return new Color(value.r*brightness,value.g*brightness,value.b*brightness,value.a);
        }
        private void FaceCamera(Camera view){if(owner==null || view==null || (view.cullingMask & (1<<5))==0)return;foreach(var item in pool)if(item!=null && item.canvas!=null && item.rect!=null && item.age<Duration && item.canvas.enabled)item.rect.rotation=view.transform.rotation;}
        public void Dismiss(){foreach(var item in pool)if(item.age<Duration)item.age=Mathf.Max(item.age,Duration*.78f);}
        private void Clear(){foreach(var item in pool)if(item!=null){item.age=100;if(item.canvas!=null)item.canvas.enabled=false;if(item.group!=null)item.group.alpha=0;}}
        private void OnDisable(){Clear();}
        private void OnDestroy(){Camera.onPreCull-=FaceCamera;if(holder!=null)Destroy(holder.gameObject);}
    }
    // One pooled quad per label, with a slanted silhouette that hugs the digits.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldScorePlate:MaskableGraphic {
        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();var r=GetPixelAdjustedRect();float slant=9;
            mesh.AddVert(new Vector3(r.xMin+slant,r.yMin),color,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin,r.yMax),color,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax-slant,r.yMax),color,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax,r.yMin),color,Vector2.zero);
            mesh.AddTriangle(0,1,2);mesh.AddTriangle(2,3,0);
        }
    }
}
