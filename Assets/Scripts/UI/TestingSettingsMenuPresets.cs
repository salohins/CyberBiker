using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.UI {
    public sealed partial class TestingSettingsMenu {
        private GameObject presetOverlay;
        private TMP_InputField presetText;
        private TMP_Text presetStatus;
        public bool PresetsOpen=>presetOverlay!=null && presetOverlay.activeSelf;
        private bool ClosePresetsIfOpen(){if(!PresetsOpen)return false;presetOverlay.SetActive(false);return true;}
        public void OpenPresets(){
            if(!IsOpen)return;
            if(presetOverlay==null)BuildPresets();
            presetText.SetTextWithoutNotify(BalancePresetIO.ExportJson(draft));presetStatus.text="Exports include Ride, Score and Triggers. Import changes the draft; APPLY saves it on this device.";presetOverlay.SetActive(true);
        }
        public string ExportPresetJson()=>BalancePresetIO.ExportJson(draft);
        public bool ImportPresetJson(string json){
            if(!BalancePresetIO.TryImport(json,draft,out var imported,out string error)){if(presetStatus!=null)presetStatus.text=error;return false;}
            draft=imported;resetRequested=false;calibrateRequested=false;foreach(var refresh in refreshFields)refresh();
            if(presetStatus!=null)presetStatus.text="Preset loaded into the draft. Close this panel, review the tabs and press APPLY when ready.";return true;
        }
        public void ExportPresetFile(){
            try{string json=ExportPresetJson();string path=BalancePresetIO.ExportFile(json);presetText.SetTextWithoutNotify(json);presetStatus.text="Saved: "+path;}
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is AndroidJavaException){presetStatus.text="Could not finish file export: "+error.Message+" Use COPY JSON as an alternative.";}
        }
        private void BuildPresets(){
            var shade=Node("Balance presets overlay",modal.transform);shade.gameObject.AddComponent<Image>().color=new Color(0,0,.02f,.95f);presetOverlay=shade.gameObject;
            var panel=Node("Preset panel",shade);Place(panel,new Vector2(.075f,.08f),new Vector2(.925f,.92f));panel.gameObject.AddComponent<Image>().color=Ink;
            var title=Label("BALANCE PRESETS",panel,28,Cyan);Place(title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(20,-55),new Vector2(-20,-10));
            presetStatus=Label("",panel,18,new Color(.8f,.9f,.95f));presetStatus.textWrappingMode=TextWrappingModes.Normal;Place(presetStatus.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(20,-134),new Vector2(-20,-58));
            var editor=Node("Preset JSON",panel);Place(editor,Vector2.zero,Vector2.one,new Vector2(20,134),new Vector2(-20,-145));var art=editor.gameObject.AddComponent<Image>();art.color=new Color(.015f,.025f,.045f);
            presetText=editor.gameObject.AddComponent<TMP_InputField>();presetText.targetGraphic=art;presetText.lineType=TMP_InputField.LineType.MultiLineNewline;presetText.characterLimit=BalancePresetIO.MaximumCharacters;presetText.navigation=new Navigation{mode=Navigation.Mode.None};
            var viewport=Node("Text viewport",editor);Place(viewport,Vector2.zero,Vector2.one,new Vector2(10,10),new Vector2(-10,-10));viewport.gameObject.AddComponent<RectMask2D>();
            var text=Label("",viewport,17,new Color(.85f,.94f,1));text.alignment=TextAlignmentOptions.TopLeft;text.textWrappingMode=TextWrappingModes.Normal;presetText.textViewport=viewport;presetText.textComponent=text;
            var row=Node("Preset actions",panel);Place(row,Vector2.zero,new Vector2(1,0),new Vector2(16,72),new Vector2(-16,122));
            var export=MakeButton("EXPORT FILE",row,Cyan,ExportPresetFile);
            var copy=MakeButton("COPY JSON",row,new Color(.15f,.22f,.3f),()=>{GUIUtility.systemCopyBuffer=ExportPresetJson();presetStatus.text="Current draft copied as JSON.";});
            var paste=MakeButton("PASTE",row,new Color(.15f,.22f,.3f),()=>{presetText.text=GUIUtility.systemCopyBuffer;presetStatus.text="Clipboard pasted. Press LOAD DRAFT to import.";});
            var load=MakeButton("LOAD DRAFT",row,Cyan,()=>ImportPresetJson(presetText.text));
            var actions=new[]{export,copy,paste,load};for(int i=0;i<actions.Length;i++)Place((RectTransform)actions[i].transform,new Vector2(i*.25f,0),new Vector2((i+1)*.25f,1),new Vector2(3,0),new Vector2(-3,0));
            var bottom=Node("Preset close row",panel);Place(bottom,Vector2.zero,new Vector2(1,0),new Vector2(16,16),new Vector2(-16,62));
            var close=MakeButton("BACK TO TUNE",bottom,new Color(.15f,.22f,.3f),()=>ClosePresetsIfOpen());Place((RectTransform)close.transform,new Vector2(.5f,0),Vector2.one,new Vector2(3,0),new Vector2(-3,0));
#if UNITY_STANDALONE || UNITY_EDITOR
            var folder=MakeButton("OPEN EXPORT FOLDER",bottom,new Color(.15f,.22f,.3f),OpenExportFolder);Place((RectTransform)folder.transform,Vector2.zero,new Vector2(.5f,1),new Vector2(3,0),new Vector2(-3,0));
#endif
        }
        private void OpenExportFolder(){
            Directory.CreateDirectory(BalancePresetIO.ExportFolder);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(BalancePresetIO.ExportFolder);
#else
            Application.OpenURL(new Uri(BalancePresetIO.ExportFolder+Path.DirectorySeparatorChar).AbsoluteUri);
#endif
        }
    }
}
