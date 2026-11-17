using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.Player {
    // Lives directly above the existing Launch button, using its Canvas and
    // safe-area placement. It never intercepts touches or changes button size.
    public sealed class LaunchEnergyHud : MonoBehaviour {
        private BikeSwordCombat owner;
        private readonly Image[] cells=new Image[12];
        private TMP_Text caption;
        private float displayed,pulse;
        private int percent=-1;
        private bool lastReady,lastRunning;
        public float DisplayedCharge=>displayed;
        public static LaunchEnergyHud Create(BikeSwordCombat source,Transform button){
            var go=new GameObject("Launch energy",typeof(RectTransform),typeof(CanvasGroup));
            var root=go.GetComponent<RectTransform>();root.SetParent(button,false);
            root.anchorMin=new Vector2(0,1);root.anchorMax=Vector2.one;root.offsetMin=new Vector2(0,5);root.offsetMax=new Vector2(0,45);
            var group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var hud=go.AddComponent<LaunchEnergyHud>();hud.owner=source;hud.displayed=source.LaunchCharge;
            Plate("Backing",root,Vector2.zero,Vector2.one,new Color(.012f,.025f,.045f,.92f));
            Plate("Cyan upper edge",root,new Vector2(0,.97f),Vector2.one,new Color(.1f,.8f,.9f,.6f));
            var text=new GameObject("Charge label",typeof(RectTransform),typeof(TextMeshProUGUI));var textRect=text.GetComponent<RectTransform>();textRect.SetParent(root,false);
            textRect.anchorMin=new Vector2(.025f,.45f);textRect.anchorMax=new Vector2(.975f,.97f);textRect.offsetMin=textRect.offsetMax=Vector2.zero;
            hud.caption=text.GetComponent<TMP_Text>();hud.caption.fontSize=16;hud.caption.alignment=TextAlignmentOptions.MidlineLeft;hud.caption.raycastTarget=false;hud.caption.color=new Color(.65f,.95f,1);hud.caption.characterSpacing=2;
            var original=button.GetComponentInChildren<TMP_Text>();if(original!=null)hud.caption.font=original.font;
            for(int i=0;i<hud.cells.Length;i++){
                var min=new Vector2(.025f+i*.95f/hud.cells.Length,.13f);var max=new Vector2(.025f+(i+1)*.95f/hud.cells.Length,.4f);
                var track=Plate("Cell "+i,root,min,max,new Color(.045f,.16f,.2f));track.rectTransform.offsetMin=new Vector2(1,0);track.rectTransform.offsetMax=new Vector2(-1,0);
                hud.cells[i]=Plate("Charge",track.transform,Vector2.zero,Vector2.one,new Color(.1f,.85f,1));hud.cells[i].rectTransform.pivot=new Vector2(0,.5f);hud.cells[i].rectTransform.offsetMin=hud.cells[i].rectTransform.offsetMax=Vector2.zero;
            }
            hud.Refresh(0);return hud;
        }
        private static Image Plate(string name,Transform parent,Vector2 min,Vector2 max,Color tint){
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.raycastTarget=false;image.color=tint;return image;
        }
        public void Refilled(){pulse=1;}
        private void LateUpdate(){if(owner!=null)Refresh(Time.unscaledDeltaTime);}
        private void Refresh(float dt){
            displayed=Mathf.MoveTowards(displayed,owner.LaunchCharge,dt*2.5f);
            pulse=Mathf.MoveTowards(pulse,0,dt*3);
            int next=Mathf.RoundToInt(owner.LaunchCharge*100);bool ready=owner.CanStartLaunch,running=owner.IsPursuing;
            if(next!=percent || ready!=lastReady || running!=lastRunning){
                percent=next;lastReady=ready;lastRunning=running;
                caption.SetText(ready || running ? "LAUNCH  {0:0}%" : "EARN SCORE  {0:0}%",next);
            }
            var tint=ready || running ? new Color(.1f,.85f,1) : new Color(.4f,.55f,.6f);
            tint=Color.Lerp(tint,new Color(.7f,1,1),pulse*.45f);
            for(int i=0;i<cells.Length;i++){cells[i].rectTransform.localScale=new Vector3(Mathf.Clamp01(displayed*cells.Length-i),1,1);cells[i].color=tint;}
        }
    }
}
