using System.Collections.Generic;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class TouchInputManager {
    private PlayerController tapPlayer;
    private bool UsesTapControls => gm!=null && gm.PistolInteractionMode==PistolInteraction.TapToFire;
    private struct TapPointer {public bool active,ui;public int id;public Vector2 origin;public float began;public bool dragged;}
    private readonly TapPointer[] tapPointers=new TapPointer[10];
    private readonly List<RaycastResult> pistolUiHits=new List<RaycastResult>(16);
    private PointerEventData pistolPointer;
    private EventSystem pistolEventSystem;
    private readonly List<(Graphic graphic,bool raycast)> desktopSteeringGraphics=new List<(Graphic,bool)>();
    private readonly List<(EventTrigger trigger,bool enabled)> steeringTouchTriggers=new List<(EventTrigger,bool)>();
    private EventTrigger oldAimTrigger;
    private bool triggerWasEnabled,lastTapMode;
    private Vector3 fireButtonScale;
    private TMP_Text fireCaption;
    private RectTransform leftFireButton;
    private TMP_Text leftFireCaption;
    private EventTrigger leftAimTrigger;
    private Button originalFireControl,leftFireControl;
    public RectTransform LeftFireButton => leftFireButton;
    private float tapPress,shotPop;
    public int PistolTapCount {get;private set;}

    private void InitializeTapControls(){
        tapPlayer=GetComponent<PlayerController>();lastTapMode=UsesTapControls;
        if(aimButton==null)return;
        fireButtonScale=aimButton.localScale;oldAimTrigger=aimButton.GetComponent<EventTrigger>();
        if(oldAimTrigger!=null){triggerWasEnabled=oldAimTrigger.enabled;if(UsesTapControls)oldAimTrigger.enabled=false;}
        var caption=new GameObject("Pistol tap caption",typeof(RectTransform),typeof(TextMeshProUGUI));caption.transform.SetParent(aimButton,false);
        var rt=(RectTransform)caption.transform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,0);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-6);rt.sizeDelta=new Vector2(180,34);
        fireCaption=caption.GetComponent<TMP_Text>();fireCaption.text="FIRE";fireCaption.fontSize=22;fireCaption.alignment=TextAlignmentOptions.Center;fireCaption.raycastTarget=false;fireCaption.color=new Color(.85f,.92f,1);
        caption.SetActive(UsesTapControls);
        // Fade the complete control, including its caption, while preserving
        // the original touch area. The mirrored button inherits this opacity.
        if(!aimButton.TryGetComponent<CanvasGroup>(out var opacity))opacity=aimButton.gameObject.AddComponent<CanvasGroup>();
        opacity.alpha=.35f;
        InitializeLeftFireButton();
        InitializeDesktopTapArea();
    }
    private void InitializeLeftFireButton(){
        // Clone the authored button so its appearance and existing firing
        // callback stay identical. Mirror its placement, never its text/image.
        leftFireButton=Instantiate(aimButton,aimButton.parent,false);
        leftFireButton.name="FireButton Left";
        leftFireButton.anchorMin=new Vector2(1-aimButton.anchorMax.x,aimButton.anchorMin.y);
        leftFireButton.anchorMax=new Vector2(1-aimButton.anchorMin.x,aimButton.anchorMax.y);
        leftFireButton.pivot=new Vector2(1-aimButton.pivot.x,aimButton.pivot.y);
        leftFireButton.anchoredPosition=new Vector2(-aimButton.anchoredPosition.x,aimButton.anchoredPosition.y);
        originalFireControl=aimButton.GetComponent<Button>();leftFireControl=leftFireButton.GetComponent<Button>();
        leftAimTrigger=leftFireButton.GetComponent<EventTrigger>();
        leftFireCaption=leftFireButton.GetComponentInChildren<TMP_Text>(true);
        leftFireButton.gameObject.SetActive(aimButton.gameObject.activeInHierarchy);
    }
    private void InitializeDesktopTapArea(){
        foreach(var trigger in FindObjectsByType<EventTrigger>()){
            bool steering=false;foreach(var entry in trigger.triggers)for(int i=0;i<entry.callback.GetPersistentEventCount();i++)
                if(entry.callback.GetPersistentTarget(i)==this && (entry.callback.GetPersistentMethodName(i)=="setLeftPressed" || entry.callback.GetPersistentMethodName(i)=="setRightPressed"))steering=true;
            if(steering){
                steeringTouchTriggers.Add((trigger,trigger.enabled));
                foreach(var graphic in trigger.GetComponentsInChildren<Graphic>(true)){
                    // Keep action buttons clickable even when they are nested
                    // beneath an old full-screen steering touch area.
                    var action=graphic.GetComponentInParent<Button>();if(action!=null && action.transform!=trigger.transform)continue;
                    desktopSteeringGraphics.Add((graphic,graphic.raycastTarget));
                }
            }
        }
        ApplyDesktopTapArea();
    }
    private void ApplyDesktopTapArea(){
        bool ignore=UsesGyroscopeSteering;
#if UNITY_EDITOR || UNITY_STANDALONE
        ignore|=UsesTapControls;
#endif
        foreach(var item in desktopSteeringGraphics)if(item.graphic!=null)item.graphic.raycastTarget=ignore ? false : item.raycast;
        foreach(var item in steeringTouchTriggers)if(item.trigger!=null)item.trigger.enabled=UsesGyroscopeSteering ? false : item.enabled;
    }
    private void UpdateTapControls(){
        if(lastTapMode!=UsesTapControls){
            lastTapMode=UsesTapControls;aimRequested=false;tapPlayer?.CancelPistolTaps();ClearTapPointers();tapPress=shotPop=0;
            if(oldAimTrigger!=null)oldAimTrigger.enabled=UsesTapControls ? false : triggerWasEnabled;
            if(fireCaption!=null)fireCaption.gameObject.SetActive(UsesTapControls);
            if(leftFireCaption!=null)leftFireCaption.gameObject.SetActive(UsesTapControls);
            if(leftAimTrigger!=null)leftAimTrigger.enabled=UsesTapControls ? false : triggerWasEnabled;
            ApplyDesktopTapArea();
        }
        if(aimButton==null)return;
        float dt=Time.unscaledDeltaTime;tapPress=Mathf.MoveTowards(tapPress,0,dt*9);shotPop=Mathf.MoveTowards(shotPop,0,dt*7);
        aimButton.localScale=fireButtonScale*(UsesTapControls ? 1-tapPress*.12f+shotPop*.08f : 1);
        if(fireCaption!=null)fireCaption.color=Color.Lerp(new Color(.85f,.92f,1),new Color(1,.75f,.25f),shotPop);
        if(leftFireButton!=null){
            leftFireButton.gameObject.SetActive(aimButton.gameObject.activeInHierarchy);
            leftFireButton.localScale=aimButton.localScale;
            if(originalFireControl!=null && leftFireControl!=null)leftFireControl.interactable=originalFireControl.interactable;
            if(leftFireCaption!=null && fireCaption!=null)leftFireCaption.color=fireCaption.color;
        }
    }
    public bool TapPistol(){
        if(!UsesTapControls || tapPlayer==null || !tapPlayer.RequestPistolTap())return false;
        PistolTapCount++;tapPress=1;return true;
    }
    public void PistolShotPulse(){tapPress=0;shotPop=1;}
    private bool IsPistolPointerOverUI(Vector2 position){
        var system=EventSystem.current;if(system==null)return false;
        if(pistolPointer==null || pistolEventSystem!=system){pistolEventSystem=system;pistolPointer=new PointerEventData(system);}
        pistolPointer.position=position;pistolUiHits.Clear();system.RaycastAll(pistolPointer,pistolUiHits);
        // Passive full-screen HUD graphics are not controls. Blocking on every
        // raycast result would swallow all gameplay-area clicks/touches.
        foreach(var hit in pistolUiHits){
            if(hit.gameObject.GetComponentInParent<Selectable>()!=null || hit.gameObject.GetComponentInParent<ScrollRect>()!=null)return true;
            var trigger=hit.gameObject.GetComponentInParent<EventTrigger>();if(trigger!=null && trigger.enabled)return true;
        }
        return false;
    }
    private void ClearTapPointers(){for(int i=0;i<tapPointers.Length;i++)tapPointers[i].active=false;}
    private void ProcessPistolTouch(int id,Vector2 position,Vector2 movement,TouchPhase phase,float time){
        int index=-1;
        for(int i=0;i<tapPointers.Length;i++)if(tapPointers[i].active && tapPointers[i].id==id){index=i;break;}
        if(phase==TouchPhase.Began){
            if(index<0)for(int i=0;i<tapPointers.Length;i++)if(!tapPointers[i].active){index=i;break;}
            if(index>=0)tapPointers[index]=new TapPointer{active=true,id=id,origin=position,began=time,ui=IsPistolPointerOverUI(position)};
        }
        if(index<0)return;
        var pointer=tapPointers[index];pointer.dragged|=(position-pointer.origin).sqrMagnitude>24*24;
        if(!pointer.ui){
            if(phase==TouchPhase.Moved && !UsesGyroscopeSteering)drag=Vector2.ClampMagnitude(drag+movement/(2000-100*touchSensitivity),1);
            if(phase==TouchPhase.Ended || phase==TouchPhase.Canceled){
                if(phase==TouchPhase.Ended && !pointer.dragged && time-pointer.began<=.3f && gm!=null && gm.gameState==GameState.Playing)TapPistol();
                if(!UsesGyroscopeSteering && !leftPressed && !rightPressed)drag=Vector2.zero;
            }
        }
        if(phase==TouchPhase.Ended || phase==TouchPhase.Canceled)pointer.active=false;
        tapPointers[index]=pointer;
    }
    private void OnDisable(){SuspendGyroscope();if(leftFireButton!=null)leftFireButton.gameObject.SetActive(false);if(aimButton!=null)aimButton.localScale=fireButtonScale==Vector3.zero ? Vector3.one : fireButtonScale;ClearTapPointers();}
    private void OnDestroy(){ReleaseGyroscope();if(leftFireButton!=null)Destroy(leftFireButton.gameObject);foreach(var item in desktopSteeringGraphics)if(item.graphic!=null)item.graphic.raycastTarget=item.raycast;foreach(var item in steeringTouchTriggers)if(item.trigger!=null)item.trigger.enabled=item.enabled;if(fireCaption!=null)Destroy(fireCaption.gameObject);if(oldAimTrigger!=null)oldAimTrigger.enabled=triggerWasEnabled;}
}
