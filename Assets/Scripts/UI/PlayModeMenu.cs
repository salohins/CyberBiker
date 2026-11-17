using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberBiker.UI {
    public sealed class PlayModeMenu:MonoBehaviour {
        private GameObject overlay;
        private RectTransform safe;
        private Rect lastSafe;
        private Vector2Int lastSize;
        private static readonly Color Cyan=new Color(.18f,.9f,1);
        public bool IsOpen=>overlay!=null && overlay.activeSelf;
        public static void Show(MainMenuManager owner){
            var menu=owner.GetComponent<PlayModeMenu>() ?? owner.gameObject.AddComponent<PlayModeMenu>();
            if(menu.overlay==null)menu.Build(owner);
            menu.overlay.SetActive(true);menu.UpdateSafeArea();ClearSelection();
        }
        public void Close(){if(overlay!=null)overlay.SetActive(false);ClearSelection();}
        private static void ClearSelection(){if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);}
        private void Update(){
            if(!IsOpen)return;
            if(lastSafe!=Screen.safeArea || lastSize!=new Vector2Int(Screen.width,Screen.height))UpdateSafeArea();
            if(Input.GetKeyDown(KeyCode.Escape))Close();
        }
        private void UpdateSafeArea(){
            lastSafe=Screen.safeArea;lastSize=new Vector2Int(Screen.width,Screen.height);
            safe.anchorMin=new Vector2(lastSafe.xMin/Mathf.Max(1,Screen.width),lastSafe.yMin/Mathf.Max(1,Screen.height));
            safe.anchorMax=new Vector2(lastSafe.xMax/Mathf.Max(1,Screen.width),lastSafe.yMax/Mathf.Max(1,Screen.height));
            safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
        private void Build(MainMenuManager owner){
            var root=Node("Play mode selection",transform);overlay=root.gameObject;
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=21000;
            var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            root.gameObject.AddComponent<Image>().color=new Color(.005f,.012f,.035f,.94f);
            safe=Node("Safe area",root);
            var panel=Node("Mode panel",safe);panel.anchorMin=new Vector2(.14f,.13f);panel.anchorMax=new Vector2(.86f,.87f);
            panel.gameObject.AddComponent<Image>().color=new Color(.025f,.045f,.075f,.98f);
            var outline=panel.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.1f,.65f,.8f,.75f);outline.effectDistance=new Vector2(1,-1);
            var slash=Node("Cyan edge",panel);slash.anchorMax=new Vector2(0,1);slash.sizeDelta=new Vector2(4,0);slash.gameObject.AddComponent<Image>().color=Cyan;
            Label("CHOOSE YOUR RIDE",panel,new Vector2(.055f,.80f),new Vector2(.95f,.96f),30,Cyan);
            Label("Two ways to play. Same bike. Same combat.",panel,new Vector2(.055f,.71f),new Vector2(.95f,.82f),18,new Color(.65f,.77f,.84f));
            Choice("Progression","PROGRESSION","Riding breaks and escalating rival encounters",panel,.45f,.68f,owner.StartProgression);
            Choice("Constant Combat","CONSTANT COMBAT","Five rivals. Continuous replacements. No riding breaks.",panel,.17f,.40f,owner.StartConstantCombat);
            var back=Choice("Back","BACK",null,panel,.035f,.13f,Close);
            back.GetComponent<Image>().color=new Color(.055f,.09f,.13f);
        }
        private static Button Choice(string name,string title,string description,Transform parent,float bottom,float top,Action action){
            var rect=Node(name,parent);rect.anchorMin=new Vector2(.055f,bottom);rect.anchorMax=new Vector2(.945f,top);
            var art=rect.gameObject.AddComponent<Image>();art.color=new Color(.055f,.13f,.18f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=art;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>action());
            var colors=button.colors;colors.highlightedColor=new Color(.55f,.95f,1);colors.pressedColor=new Color(.3f,.7f,.8f);button.colors=colors;
            Label(title,rect,new Vector2(.035f,description==null ? 0 : .40f),new Vector2(.91f,1),description==null ? 19 : 25,Cyan);
            if(description!=null)Label(description,rect,new Vector2(.035f,.05f),new Vector2(.94f,.47f),18,new Color(.8f,.89f,.94f));
            Label("›",rect,new Vector2(.93f,0),new Vector2(.985f,1),30,Cyan);
            return button;
        }
        private static void Label(string text,Transform parent,Vector2 min,Vector2 max,int size,Color color){
            var rect=Node(text,parent);rect.anchorMin=min;rect.anchorMax=max;
            var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.text=text;label.font=TMP_Settings.defaultFontAsset;label.fontSize=size;label.color=color;label.raycastTarget=false;label.alignment=TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing=true;label.fontSizeMin=size-4;label.fontSizeMax=size;
        }
        private static RectTransform Node(string name,Transform parent){
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.SetParent(parent,false);rect.gameObject.layer=LayerMask.NameToLayer("UI");rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
    }
}
