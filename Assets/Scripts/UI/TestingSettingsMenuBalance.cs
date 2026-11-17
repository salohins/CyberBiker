using System;
using System.Reflection;
using System.Collections.Generic;
using CyberBiker.Balance;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.UI {
    public sealed partial class TestingSettingsMenu {
        private readonly RectTransform[] balancePages=new RectTransform[3];
        private readonly Button[] tabButtons=new Button[3];
        private readonly float[] tabScroll={1,1,1};
        private ScrollRect settingsScroll;
        private int currentTab;
        private sealed class BalanceGroup {public string name;public GameObject body;public Button header;}
        private readonly List<BalanceGroup> balanceGroups=new List<BalanceGroup>();
        public int CurrentTab=>currentTab;
        private static RectTransform CreateSettingsPage(string name,Transform viewport){
            var content=Node(name,viewport);Place(content,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(16,16,6,16);layout.spacing=5;layout.childControlWidth=layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;return content;
        }
        private void BuildBalanceTabs(Transform panel,Transform viewport,ScrollRect scroll,RectTransform ride){
            settingsScroll=scroll;balancePages[0]=ride;
            balancePages[1]=CreateSettingsPage("Score settings",viewport);
            BalanceFields(balancePages[1],typeof(ScoreBalanceTuning),()=>draft.scoreBalance,c=>c.section=="POINTS PER MOVE" || c.section=="COMBO RULES");
            Section("SCORE FEEDBACK",balancePages[1]);
            Field("Score animation intensity (0 = reduced motion)","Score motion",balancePages[1],0,2,.1f,()=>draft.scoreAnimationIntensity,v=>draft.scoreAnimationIntensity=v);
            Field("Combo sound volume","Score sounds",balancePages[1],0,1,.05f,()=>draft.scoreSoundVolume,v=>draft.scoreSoundVolume=v);
            balancePages[2]=CreateSettingsPage("Trigger settings",viewport);
            BalanceFields(balancePages[2],typeof(ScoreBalanceTuning),()=>draft.scoreBalance,c=>c.section!="POINTS PER MOVE" && c.section!="COMBO RULES");
            BalanceFields(balancePages[2],typeof(TriggerBalanceTuning),()=>draft.triggerBalance);
            var tabs=Node("Settings tabs",panel);Place(tabs,new Vector2(0,1),Vector2.one,new Vector2(24,-143),new Vector2(-24,-99));
            string[] names={"RIDE","SCORE","TRIGGERS","PRESETS"};
            for(int i=0;i<names.Length;i++){
                int selected=i;var button=MakeButton(names[i],tabs,new Color(.15f,.22f,.3f),()=>{if(selected==3)OpenPresets();else SelectTab(selected);});
                Place((RectTransform)button.transform,new Vector2(i*.25f,0),new Vector2((i+1)*.25f,1),new Vector2(3,0),new Vector2(-3,0));
                if(i<3)tabButtons[i]=button;
            }
            SelectTab(0);
        }
        public void SelectTab(int index){
            if(settingsScroll==null || index<0 || index>=balancePages.Length)return;
            tabScroll[currentTab]=settingsScroll.verticalNormalizedPosition;
            currentTab=index;
            for(int i=0;i<balancePages.Length;i++){
                balancePages[i].gameObject.SetActive(i==index);
                tabButtons[i].GetComponent<Image>().color=i==index ? Cyan : new Color(.15f,.22f,.3f);
                tabButtons[i].GetComponentInChildren<TMP_Text>().color=i==index ? Ink : Color.white;
            }
            settingsScroll.StopMovement();settingsScroll.content=balancePages[index];Canvas.ForceUpdateCanvases();settingsScroll.verticalNormalizedPosition=tabScroll[index];
        }
        public void OpenBalanceSection(string name){foreach(var group in balanceGroups)if(group.name==name)SetGroup(group,true);}
        private static void SetGroup(BalanceGroup group,bool open){group.body.SetActive(open);group.header.GetComponentInChildren<TMP_Text>().text=(open ? "-  " : "+  ")+group.name;}
        private Transform CreateBalanceGroup(Transform parent,string name){
            var container=Node(name,parent);var layout=container.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=5;layout.childControlWidth=layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var group=new BalanceGroup{name=name};
            group.header=MakeButton(name,container,new Color(.075f,.14f,.19f),()=>SetGroup(group,!group.body.activeSelf));group.header.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
            var label=group.header.GetComponentInChildren<TMP_Text>();label.alignment=TextAlignmentOptions.MidlineLeft;label.color=Cyan;Place(label.rectTransform,Vector2.zero,Vector2.one,new Vector2(12,0),new Vector2(-8,0));
            var body=Node("Section controls",container);var bodyLayout=body.gameObject.AddComponent<VerticalLayoutGroup>();bodyLayout.spacing=5;bodyLayout.childControlWidth=bodyLayout.childForceExpandWidth=true;bodyLayout.childControlHeight=true;bodyLayout.childForceExpandHeight=false;group.body=body.gameObject;
            balanceGroups.Add(group);SetGroup(group,name=="POINTS PER MOVE" || name=="BULLET DODGE SCORE");return body;
        }
        private void BalanceFields(Transform parent,Type type,Func<object> value,Func<BalanceControlAttribute,bool> include=null){
            string section=null;
            Transform sectionBody=parent;
            foreach(var member in type.GetFields()){
                var control=member.GetCustomAttribute<BalanceControlAttribute>();if(control==null || include!=null && !include(control))continue;
                if(section!=control.section){section=control.section;sectionBody=CreateBalanceGroup(parent,section);}
                if(member.FieldType==typeof(bool)){
                    var row=Node(member.Name,sectionBody);row.gameObject.AddComponent<LayoutElement>().preferredHeight=72;row.gameObject.AddComponent<Image>().color=new Color(.06f,.085f,.125f);
                    var label=Label(control.label,row,21,Color.white);Place(label.rectTransform,Vector2.zero,new Vector2(.72f,1),new Vector2(12,0),new Vector2(-8,0));label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=21;
                    var button=MakeButton("",row,Ink,()=>{member.SetValue(value(),!(bool)member.GetValue(value()));resetRequested=false;foreach(var refresh in refreshFields)refresh();});
                    Place((RectTransform)button.transform,new Vector2(.74f,.15f),new Vector2(.98f,.85f));
                    refreshFields.Add(()=>{bool on=(bool)member.GetValue(value());button.GetComponent<Image>().color=on ? Cyan : new Color(.15f,.22f,.3f);var text=button.GetComponentInChildren<TMP_Text>();text.text=on ? "ON" : "OFF";text.color=on ? Ink : Color.white;});
                }else Field(control.label,member.Name,sectionBody,control.min,control.max,control.step,()=>Convert.ToSingle(member.GetValue(value())),v=>{if(member.FieldType==typeof(int))member.SetValue(value(),Mathf.RoundToInt(v));else member.SetValue(value(),v);});
            }
            foreach(var refresh in refreshFields)refresh();
        }
    }
}
