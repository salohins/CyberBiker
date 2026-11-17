using System;
using System.IO;
using System.Reflection;
using CyberBiker.Scoring;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Native UI verification and deterministic animation capture. Runs only in the
// isolated StyleScoreChecks project; never edits the saved gameplay settings.
public static class StyleScoreBoardChecks {
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Field(StyleScoreHud hud,string name)=>typeof(StyleScoreHud).GetField(name,Private).GetValue(hud);
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(StyleScoreController score,GameplayManager manager,PlayerController player){
        var hud=score.Hud;var settings=score.Settings;
        var update=typeof(StyleScoreHud).GetMethod("UpdatePresentation",Private);
        Action<float> step=dt=>update.Invoke(hud,new object[]{dt});
        var headline=(RectTransform)Field(hud,"headline");var badge=(RectTransform)Field(hud,"badge");
        var sweep=(Image)Field(hud,"energySweep");var echoes=(CanvasGroup[])Field(hud,"badgeEchoAlpha");
        var root=(RectTransform)Field(hud,"root");
        settings.reducedMotion=false;settings.animationIntensity=1;score.Combo.Break();
        Action<StyleAction> award=action=>score.Record(action,player.transform.position+Vector3.up*2);
        award(StyleAction.PistolHit);step(.05f);
        Require(headline.localScale.x>1.03f && headline.localScale.x<1.4f,"A real hit needs a visible bounded score punch");
        manager.SwitchGameState(GameState.Pause);var scale=headline.localScale;float age=(float)Field(hud,"age");
        typeof(StyleScoreHud).GetMethod("LateUpdate",Private).Invoke(hud,null);
        Require(headline.localScale==scale && (float)Field(hud,"age")==age,"Pause must freeze the board animation");manager.SwitchGameState(GameState.Playing);
        award(StyleAction.NearMiss);award(StyleAction.SwordFinisher);step(.08f);
        Require(score.Combo.Multiplier>=2 && badge.localScale.x>1.05f && sweep.color.a>0 && echoes[0].alpha>0,"Multiplier gain needs a snap, sweep and fading outline echo");
        int points=score.Combo.Potential,multiplier=score.Combo.Multiplier;score.Combo.Bank();step(.1f);
        Require((int)Field(hud,"bankedMultiplier")==multiplier && (float)Field(hud,"shownPoints")<points,"Bank must consume the earned multiplier and count toward the exact result");
        for(int i=0;i<30;i++)step(1f/60);
        Require(Mathf.RoundToInt((float)Field(hud,"shownPoints"))==points && ((TMP_Text)Field(hud,"pointsText")).text=="+"+points.ToString("N0"),"Banked count-up must settle at the actual credited amount");
        Require(((CanvasGroup)Field(hud,"badgeAlpha")).alpha==0,"Consumed multiplier must finish fading");
        foreach(var cell in (Image[])Field(hud,"timerCells"))Require(cell.rectTransform.localScale.x==0,"Bank must empty every timer segment");
        Require(hud.Canvas.transform.Find("Safe area/Combo display/Ink plate").GetComponent<RectTransform>().sizeDelta==new Vector2(560,133) && badge.sizeDelta==new Vector2(108,83),"Board animation must preserve the compact base layout");
        foreach(var graphic in hud.Canvas.GetComponentsInChildren<Graphic>(true))Require(!graphic.raycastTarget,"Animated art must ignore touch input");
        foreach(bool explicitReduced in new[]{true,false}){
            settings.reducedMotion=explicitReduced;settings.animationIntensity=explicitReduced ? 1 : 0;
            award(StyleAction.GapPass);award(StyleAction.PerfectDodge);step(.06f);
            Require(headline.localScale==Vector3.one && Quaternion.Angle(root.localRotation,Quaternion.identity)<.001f && sweep.color.a==0 && echoes[0].alpha==0,"Reduced motion and intensity zero must suppress all board decorations");
        }
        settings.reducedMotion=false;settings.animationIntensity=1;
        CaptureSequence(score,step,award);
        File.AppendAllText("Logs/StyleScore/Result.txt","PASS native board animation: bounded rapid-hit spring, stronger multiplier sweep/echo, exact bank count-up, compact layout, pause freeze, input pass-through, both reduced-motion settings; 180-frame native preview captured.\n");
    }
    private static void CaptureSequence(StyleScoreController score,Action<float> step,Action<StyleAction> award){
        const int width=960,height=320;
        Directory.CreateDirectory("Logs/StyleScore/BoardFrames");
        var hud=score.Hud;var canvas=hud.Canvas;var scaler=canvas.GetComponent<CanvasScaler>();
        var originalResolution=scaler.referenceResolution;var originalMode=canvas.renderMode;var originalCamera=canvas.worldCamera;float originalDistance=canvas.planeDistance;
        var root=(RectTransform)Field(hud,"root");var oldMin=root.anchorMin;var oldMax=root.anchorMax;var oldPosition=root.anchoredPosition;
        var help=(TMP_Text)Field(hud,"help");bool originalHelp=help.gameObject.activeSelf;
        var nodes=canvas.GetComponentsInChildren<Transform>(true);var layers=new int[nodes.Length];
        var camera=new GameObject("Score board preview camera").AddComponent<Camera>();
        var rt=new RenderTexture(width,height,24);var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var active=RenderTexture.active;
        try{
            for(int i=0;i<nodes.Length;i++){layers[i]=nodes[i].gameObject.layer;nodes[i].gameObject.layer=31;}
            camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.022f,.045f);camera.cullingMask=1<<31;camera.allowHDR=false;camera.orthographic=true;camera.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;scaler.referenceResolution=new Vector2(width,height);
            root.anchorMin=root.anchorMax=new Vector2(.5f,.6f);root.anchoredPosition=Vector2.zero;
            score.Combo.Break();step(3);
            for(int frame=0;frame<180;frame++){
                switch(frame){
                    case 0:case 6:case 12:award(StyleAction.PistolHit);break;
                    case 23:award(StyleAction.NearMiss);break;
                    case 38:award(StyleAction.SwordFinisher);break;
                    case 56:award(StyleAction.BulletDodge);break;
                    case 66:award(StyleAction.PerfectDodge);break;
                    case 84:award(StyleAction.GapPass);break;
                    case 101:award(StyleAction.NearMiss);break;
                    case 119:score.Combo.Bank();break;
                }
                step(1f/30);help.gameObject.SetActive(false);Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/StyleScore/BoardFrames/"+frame.ToString("D4")+".png",pixels.EncodeToPNG());
            }
        }finally{
            canvas.renderMode=originalMode;canvas.worldCamera=originalCamera;canvas.planeDistance=originalDistance;scaler.referenceResolution=originalResolution;
            root.anchorMin=oldMin;root.anchorMax=oldMax;root.anchoredPosition=oldPosition;help.gameObject.SetActive(originalHelp);
            for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];RenderTexture.active=active;
            UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
