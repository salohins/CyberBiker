using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.Scoring {
    // Combo presentation stays on the overlay. Actual 3D score numbers are
    // owned separately by WorldStyleNumbers with a fixed-size pool.
    public sealed partial class StyleScoreHud:MonoBehaviour {
        private sealed class Shard {public RectTransform rect;public Image image;public Vector2 origin,velocity;public float age=10;}
        private readonly Shard[] shards=new Shard[12];
        private readonly TMP_Text[] feed=new TMP_Text[3];
        private readonly float[] feedAge=new float[3];
        private StyleScoreController owner;
        private Canvas canvas;
        private RectTransform canvasRect,safe,root,headline,badge,bar,chevrons;
        private CanvasGroup group;
        private TMP_Text title,pointsText,multiplier,best,help;
        private readonly Image[] timerCells=new Image[16];
        private float shownPoints,age=10;
        private int feedPoints,displayed=-1,lastBest=-1;
        private StyleAction lastAction;
        private bool banking,broken;
        private Color accent;
        private Rect lastSafe;
        public Canvas Canvas=>canvas;
        private WorldStyleNumbers worldNumbers;
        public WorldStyleNumbers WorldNumbers=>worldNumbers;
        public void RefreshBalance(){lastVisualMultiplier=owner.Combo.Multiplier;if(!banking && !broken){accent=Tier(owner.Combo.Multiplier);title.text=Grade(owner.Combo.Multiplier);}}
        public int LivePopups=>worldNumbers!=null ? worldNumbers.ActiveCount : 0;
        private StyleScoringSettings Settings=>owner.Settings;
        private bool Motion=>!Settings.reducedMotion && Settings.animationIntensity>.001f;
        public void Initialize(StyleScoreController controller) {
            owner=controller;
            var go=new GameObject("Style combo HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvas=go.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;canvasRect=go.GetComponent<RectTransform>();
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            safe=Node("Safe area",canvasRect,Vector2.zero,Vector2.zero);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;
            root=Node("Combo display",safe,Vector2.zero,new Vector2(650,190));group=root.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            var plate=Image("Ink plate",root,new Vector2(0,8),new Vector2(560,133),new Color(.014f,.019f,.04f,.82f));plate.rectTransform.localRotation=Quaternion.Euler(0,0,-2);
            var cyan=new Color(.12f,.9f,1);
            var stripe=Image("Cyan slash",root,new Vector2(-285,20),new Vector2(5,94),cyan);stripe.rectTransform.localRotation=Quaternion.Euler(0,0,-9);
            var lower=Image("Cyan edge break",root,new Vector2(-296,-48),new Vector2(5,25),cyan);lower.rectTransform.localRotation=Quaternion.Euler(0,0,-9);
            chevrons=Node("Combo chevrons",root,new Vector2(-240,16),new Vector2(40,46));
            for(int i=0;i<2;i++){
                var tint=new Color(cyan.r,cyan.g,cyan.b,i==0 ? .35f : .9f);float x=i*12-12;
                Stroke("Chevron upper",chevrons,new Vector2(x-12,15),new Vector2(x,0),2,tint);
                Stroke("Chevron lower",chevrons,new Vector2(x,0),new Vector2(x-12,-15),2,tint);
            }
            Stroke("End bracket upper",root,new Vector2(282,34),new Vector2(296,16),2,cyan);
            Stroke("End bracket lower",root,new Vector2(296,16),new Vector2(282,-2),2,cyan);
            title=Text("Combo title",root,new Vector2(-25,72),new Vector2(470,25),18,TextAlignmentOptions.Left);title.text="STYLE COMBO";title.characterSpacing=5;
            headline=Node("Score impact",root,new Vector2(-38,16),new Vector2(450,76));
            pointsText=Text("Pending points",headline,Vector2.zero,new Vector2(450,80),64,TextAlignmentOptions.Center);pointsText.fontStyle=FontStyles.Bold|FontStyles.Italic;
            badge=Node("Multiplier",root,new Vector2(222,16),new Vector2(108,83));
            Image("Hologram backing",badge,Vector2.zero,new Vector2(96,64),new Color(.01f,.055f,.075f,.9f));
            var corners=new[]{new Vector2(-54,-26),new Vector2(-40,-40),new Vector2(40,-40),new Vector2(54,-26),new Vector2(54,26),new Vector2(40,40),new Vector2(-40,40),new Vector2(-54,26)};
            for(int i=0;i<corners.Length;i++)Stroke("Multiplier frame "+i,badge,corners[i],corners[(i+1)%corners.Length],1.5f,new Color(cyan.r,cyan.g,cyan.b,.65f));
            Stroke("Multiplier bright corner",badge,new Vector2(-54,26),new Vector2(-40,40),3,cyan);
            Stroke("Multiplier bright corner",badge,new Vector2(40,-40),new Vector2(54,-26),3,cyan);
            multiplier=Text("Multiplier text",badge,Vector2.zero,new Vector2(105,85),49,TextAlignmentOptions.Center);multiplier.fontStyle=FontStyles.Bold|FontStyles.Italic;multiplier.color=cyan;
            bar=Node("Segmented combo timer",root,new Vector2(-267,-40),new Vector2(534,12));bar.pivot=new Vector2(0,.5f);
            for(int i=0;i<timerCells.Length;i++){
                float x=(i+.5f)*534/timerCells.Length;
                var empty=Image("Empty timer cell "+i,bar,new Vector2(x,0),new Vector2(534f/timerCells.Length-4,6),new Color(.04f,.19f,.23f,.7f));empty.rectTransform.anchorMin=empty.rectTransform.anchorMax=new Vector2(0,.5f);
                var cell=Image("Timer charge "+i,bar,new Vector2(x,0),empty.rectTransform.sizeDelta,cyan);cell.rectTransform.anchorMin=cell.rectTransform.anchorMax=new Vector2(0,.5f);cell.rectTransform.pivot=new Vector2(0,.5f);cell.rectTransform.anchoredPosition=new Vector2(i*534f/timerCells.Length+2,0);timerCells[i]=cell;
            }
            var tracer=new Color(cyan.r,cyan.g,cyan.b,.5f);
            Stroke("Timer top edge",bar,new Vector2(0,6),new Vector2(534,6),1,tracer);
            Stroke("Timer bottom edge",bar,new Vector2(0,-6),new Vector2(534,-6),1,tracer);
            Stroke("Timer left chevron",bar,new Vector2(0,6),new Vector2(-6,0),1.5f,cyan);Stroke("Timer left chevron",bar,new Vector2(-6,0),new Vector2(0,-6),1.5f,cyan);
            Stroke("Timer right chevron",bar,new Vector2(534,6),new Vector2(540,0),1.5f,cyan);Stroke("Timer right chevron",bar,new Vector2(540,0),new Vector2(534,-6),1.5f,cyan);
            foreach(Transform piece in bar){var rect=(RectTransform)piece;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);}
            if(Settings.displayFont!=null){pointsText.font=multiplier.font=title.font=Settings.displayFont;}
            for(int i=0;i<feed.Length;i++){feed[i]=Text("Move "+i,root,new Vector2(0,-64-i*25),new Vector2(700,28),22-i*2,TextAlignmentOptions.Center);feedAge[i]=10;}
            best=Text("Best combo",root,new Vector2(175,72),new Vector2(200,25),15,TextAlignmentOptions.Right);
            help=Text("Combo hint",safe,new Vector2(0,-15),new Vector2(700,24),16,TextAlignmentOptions.Center);help.text="CHAIN MOVES  /  RIDE CLEAN  /  BANK THE COMBO";help.color=new Color(.7f,.8f,.9f,.5f);
            worldNumbers=gameObject.AddComponent<WorldStyleNumbers>();worldNumbers.Initialize(owner);
            for(int i=0;i<shards.Length;i++){var image=Image("Bank shard "+i,root,Vector2.zero,new Vector2(5,18),Color.clear);shards[i]=new Shard{rect=image.rectTransform,image=image};}
            InitializeBoardMotion();
            UpdateSafeArea();
        }
        public void Move(StyleAction action,string label,int points,Vector3 point,bool levelUp) {
            bool repeat=age<.18f && lastAction==action && !banking && !broken;
            if(banking || broken || owner.Combo.Moves==1)shownPoints=0;
            displayed=-1;banking=broken=false;age=0;accent=Tier(owner.Combo.Multiplier);
            title.text=levelUp ? "MULTIPLIER UP!" : Grade(owner.Combo.Multiplier);
            if(repeat){feedPoints+=points;feed[0].text=label+"  +"+feedPoints;feedAge[0]=0;}
            else {
                for(int i=feed.Length-1;i>0;i--){feed[i].text=feed[i-1].text;feedAge[i]=feedAge[i-1]+.12f;}
                feedPoints=points;feed[0].text=label+"  +"+points;feedAge[0]=0;
            }
            lastAction=action;badge.gameObject.SetActive(true);
            BeginBoardReward(action,points,levelUp);
            if(Settings.worldPopups && action!=StyleAction.SpeedRun && action!=StyleAction.HardCarve)worldNumbers.Emit(action,points,point,accent);
            if(levelUp)Burst(6);
        }
        public void Bank(int points) {
            BeginBoardBank(points);
            banking=true;broken=false;age=0;displayed=-1;
            title.text="COMBO BANKED";accent=new Color(1,.8f,.2f);badge.gameObject.SetActive(Motion);Burst(12);
        }
        public void Break(int lost) {
            broken=true;banking=false;age=0;shownPoints=lost;displayed=-1;
            title.text="COMBO LOST";accent=new Color(1,.25f,.36f);badge.gameObject.SetActive(false);
            worldNumbers.Dismiss();
            BeginBoardLoss();
        }
        private void UpdateSafeArea() {
            var area=Screen.safeArea;if(area==lastSafe && safe!=null)return;lastSafe=area;
            safe.anchorMin=new Vector2(area.x/Mathf.Max(1,Screen.width),area.y/Mathf.Max(1,Screen.height));safe.anchorMax=new Vector2(area.xMax/Mathf.Max(1,Screen.width),area.yMax/Mathf.Max(1,Screen.height));
        }
        private void LateUpdate() {
            if(owner==null || canvas==null)return;
            canvas.enabled=Settings.enabled && Settings.showHud;
            if(!canvas.enabled)return;
            UpdateSafeArea();root.anchorMin=root.anchorMax=Settings.hudPosition;root.anchoredPosition=Vector2.zero;
            help.rectTransform.anchorMin=help.rectTransform.anchorMax=Settings.hudPosition;help.rectTransform.anchoredPosition=new Vector2(0,-60);
            if(owner.State==Gameplay.GameState.Pause)return;
            UpdatePresentation(Time.unscaledDeltaTime);
        }
        private void UpdatePresentation(float dt) {
            age+=dt;
            bool live=owner.Combo.Moves>0;
            float target=live ? owner.Combo.BasePoints : shownPoints;
            shownPoints=banking ? BankedDisplayValue() : Motion && live ? Mathf.Lerp(shownPoints,target,1-Mathf.Exp(-dt*26)) : target;
            int number=Mathf.RoundToInt(shownPoints);if(number!=displayed){displayed=number;pointsText.text=(banking ? "+" : broken ? "-" : "")+number.ToString("N0");}
            multiplier.SetText("×{0}",banking ? bankedMultiplier : owner.Combo.Multiplier);
            float exit=live ? 1 : 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(broken ? 1 : 1.5f,broken ? 1.6f : 2.3f,age));
            if(!live && !banking && !broken)exit=0;
            group.alpha=exit;help.gameObject.SetActive(!live && age>2.4f);
            float amount=Motion ? Settings.animationIntensity : 0;
            root.localScale=Vector3.one*Settings.hudScale;
            root.localRotation=Quaternion.identity;
            title.color=accent;
            float charge=live ? Mathf.Clamp01(owner.Combo.Remaining/Mathf.Max(.5f,Settings.comboWindow)) : 0;
            var timerColor=live && owner.Combo.Remaining<1.2f ? new Color(1,.45f,.2f) : new Color(.12f,.9f,1);
            UpdateBoardMotion(dt,live,charge,timerColor,amount);
            if(owner.Combo.BestCombo!=lastBest){lastBest=owner.Combo.BestCombo;best.text=lastBest>0 ? "BEST "+lastBest.ToString("N0") : "";}
            for(int i=0;i<feed.Length;i++){feedAge[i]+=dt;float alpha=Mathf.Clamp01(1-(feedAge[i]-.8f)/2.4f)*(1-i*.22f);var tint=i==0 ? Color.Lerp(Color.white,accent,.42f) : new Color(.91f,.96f,1);tint.a=alpha;feed[i].color=tint;feed[i].rectTransform.anchoredPosition=new Vector2(0,-64-i*25+(Motion ? Mathf.Exp(-feedAge[i]*15)*-6 : 0));}
            foreach(var shard in shards) {
                shard.age+=dt;float t=shard.age/.65f;if(t>=1 || !Motion){shard.image.color=Color.clear;continue;}
                shard.rect.anchoredPosition=shard.origin+shard.velocity*t+Vector2.down*(t*t*25);
                shard.image.color=new Color(accent.r,accent.g,accent.b,1-t);
            }
        }
        private void Burst(int count) {
            if(!Motion)return;
            for(int i=0;i<count;i++){var s=shards[i];s.age=0;s.origin=new Vector2(248,12+(i%3-1)*10);s.velocity=new Vector2(18+i*3,(i-(count-1)*.5f)*6);s.rect.localRotation=Quaternion.Euler(0,0,-25+i*7);}
        }
        private static Color Tier(int multiplier)=>multiplier>=7 ? new Color(1,.8f,.2f) : multiplier>=4 ? new Color(1,.42f,.68f) : multiplier>=2 ? new Color(.66f,.42f,1) : new Color(.18f,.94f,1);
        private static string Grade(int multiplier)=>multiplier>=7 ? "UNSTOPPABLE" : multiplier>=4 ? "ABSOLUTE MAYHEM" : multiplier>=2 ? "FLOW STATE" : "STYLE COMBO";
        private static RectTransform Node(string name,Transform parent,Vector2 position,Vector2 size){var go=new GameObject(name,typeof(RectTransform));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
        private static Image Stroke(string name,Transform parent,Vector2 from,Vector2 to,float width,Color color){var delta=to-from;var image=Image(name,parent,(from+to)*.5f,new Vector2(delta.magnitude,width),color);image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);return image;}
        private static Image Image(string name,Transform parent,Vector2 position,Vector2 size,Color color){var rt=Node(name,parent,position,size);var image=rt.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private static TMP_Text Text(string name,Transform parent,Vector2 position,Vector2 size,int fontSize,TextAlignmentOptions alignment){var rt=Node(name,parent,position,size);var text=rt.gameObject.AddComponent<TextMeshProUGUI>();text.fontSize=fontSize;text.alignment=alignment;text.raycastTarget=false;text.text="";text.color=Color.white;text.outlineWidth=.15f;text.outlineColor=new Color32(2,4,12,240);text.textWrappingMode=TextWrappingModes.NoWrap;return text;}
        private void OnDestroy(){if(canvas!=null)Destroy(canvas.gameObject);}
    }
}
