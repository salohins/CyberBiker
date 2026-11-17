using System;
using System.IO;
using System.Reflection;
using CyberBiker.Audio;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.Runs;
using CyberBiker.Scoring;
using Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StyleScoreChecks {
    private const string Key="CyberBiker.StyleScoreChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;
    private static GameplayManager manager;
    private static StyleScoreController score;
    private static Enemy enemy;
    private static int stage,lastFrame=-1,awards;
    private static float stageAt;
    private static double began;
    private static bool done;
    private sealed class NeutralInput:IPlayerInput {public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    static StyleScoreChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Report(string text)=>File.AppendAllText("Logs/StyleScore/Result.txt","PASS "+text+"\n");
    public static void Run(){
        Directory.CreateDirectory("Logs/StyleScore");File.WriteAllText("Logs/StyleScore/Result.txt","RUNNING\n");
        SessionState.SetBool(Key+"HadPrefs",PlayerPrefs.HasKey(CyberBiker.UI.TestingSettings.PreferenceKey));SessionState.SetString(Key+"Prefs",PlayerPrefs.GetString(CyberBiker.UI.TestingSettings.PreferenceKey,""));PlayerPrefs.DeleteKey(CyberBiker.UI.TestingSettings.PreferenceKey);
        try{
            var settings=new StyleScoringSettings();var model=new StyleCombo();int banked=0,broken=0;model.Banked+=v=>banked+=v;model.Broken+=v=>broken+=v;
            Require(model.Add(StyleAction.PistolHit,40,settings)==40,"First hit points incorrect");model.Add(StyleAction.NearMiss,120,settings);model.Add(StyleAction.SwordFinisher,800,settings);
            Require(model.BasePoints==960 && model.Multiplier==2 && model.Potential==1920,"Mixed-action arithmetic or multiplier incorrect");
            model.Tick(4.99f);Require(banked==0,"Combo banked too early");model.Tick(.02f);model.Bank();Require(banked==1920 && model.BankCount==1 && model.Moves==0,"Combo must bank exactly once");
            int first=model.Add(StyleAction.PistolHit,40,settings),repeat=model.Add(StyleAction.PistolHit,40,settings);Require(repeat<first,"Repeated moves must lose value");int lost=model.Potential;model.Break();Require(broken==lost && banked==1920 && model.Moves==0,"Breaking may not credit pending points");
            for(int i=0;i<100;i++)model.Add(i%2==0 ? StyleAction.NearMiss : StyleAction.PistolHit,40,settings);Require(model.Multiplier==10,"Multiplier must cap at configured maximum");
            var session=new RunSession(GameState.Playing);int changes=0;session.ScoreChanged+=v=>changes++;session.AddScore(1920);session.ChangeState(GameState.Pause);session.AddScore(100);session.ChangeState(GameState.Playing);session.AddScore(float.NaN);session.AddScore(-1);Require(session.Score==1920 && changes==1,"Run score must reject invalid or paused credits");
            Report("combo arithmetic, variety multiplier, diminishing repetition, multiplier cap, exact-once banking, lost unbanked points and valid-only run score credits.");
            var impactModel=new StyleCombo();impactModel.Add(StyleAction.NearMiss,5,settings);int impactCredit=0;impactModel.Banked+=v=>impactCredit+=v;impactModel.BankAfterImpact();impactModel.BankAfterImpact();
            Require(impactCredit==2 && impactModel.BankCount==1 && impactModel.Moves==0 && impactModel.Multiplier==1,"Impact must bank half rounded down, reset the chain and credit once");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Next(){stage++;stageAt=Time.time;File.AppendAllText("Logs/StyleScore/Progress.txt",stage+"\n");}
    private static void Tick(){
        if(done || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try{
            Require(EditorApplication.timeSinceStartup-began<100,"Style scoring timed out at "+stage);
            if(stage==0){
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.5f)return;
                manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();manager.SwitchGameState(GameState.Playing);manager.PlayerPistolDamage=15;manager.TapPistolCanKill=true;manager.StyleScoring.enabled=true;manager.StyleScoring.comboWindow=12;manager.StyleScoring.animationIntensity=1;manager.StyleScoring.soundVolume=1;
                score=manager.StyleScore;Require(score!=null && score.Hud!=null,"Scoring runtime and HUD were not installed");
                foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>())director.enabled=false;
                foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>())spawner.enabled=false;
                foreach(var old in UnityEngine.Object.FindObjectsByType<Enemy>())old.gameObject.SetActive(false);
                foreach(var car in UnityEngine.Object.FindObjectsByType<Car>())car.gameObject.SetActive(false);
                var body=player.GetComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeAll;body.linearVelocity=Vector3.zero;
                player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
                Time.captureDeltaTime=.05f;FinisherClock.EditorCaptureStep=.05f;
                enemy=SpawnEnemy();Next();
            }else if(stage==1 && enemy.HasInitialized){
                enemy.enabled=false;enemy.StopAllCoroutines();awards=score.Combo.AwardCount;
                Require(player.RequestPistolTap(),"Pistol tap did not queue");Next();
            }else if(stage==2 && score.Combo.AwardCount>awards){
                Require(score.Combo.AwardCount==awards+1 && score.Combo.BasePoints==manager.StyleScoring.pistolHit && enemy.CurrentHealth<100,"Actual pistol hit must award exactly once");
                Require(score.Hud.LivePopups>0 && GameAudio.Active.StyleScoreSoundCount>0,"A hit must produce a pooled popup and mixed sound");
                Require(score.Hud.Canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length==0,"Score numbers must no longer live on the screen overlay");
                var numbers=score.Hud.WorldNumbers;Require(numbers!=null && numbers.Canvases.Count==8,"World number pool missing");
                foreach(var worldCanvas in numbers.Canvases){
                    Require(worldCanvas.renderMode==RenderMode.WorldSpace,"Every number must render in actual 3D space");Require(worldCanvas.GetComponent<GraphicRaycaster>()==null,"World score must not intercept input");
                    var label=worldCanvas.GetComponentInChildren<UnityEngine.UI.Text>();Require(!label.raycastTarget,"Floating numbers must not steal touches");Require(label.font==manager.StyleScoring.popupTypeface,"Floating numbers must use the separate cyberpunk typeface");
                    if(!string.IsNullOrEmpty(label.text))Require(System.Text.RegularExpressions.Regex.IsMatch(label.text,@"^\+\d+$"),"Floating feedback must contain only a +number");
                    if(!string.IsNullOrEmpty(label.text))Require(Mathf.Max(label.color.r,Mathf.Max(label.color.g,label.color.b))<=Mathf.Max(manager.StyleScoring.worldNumberBrightness,manager.StyleScoring.worldNumberAppearanceBrightness)+.001f,"World numbers must respect their configured brightness limits");
                }
                TestWorldPlacement();TestStyleAudio();
                var cells=(Image[])typeof(StyleScoreHud).GetField("timerCells",Private).GetValue(score.Hud);Require(cells.Length==16,"Timer segment count changed");
                Require(cells[0].rectTransform.localScale.x>0 && cells[0].color.g>.8f,"Active timer must show cyan charge");
                var timer=(RectTransform)typeof(StyleScoreHud).GetField("bar",Private).GetValue(score.Hud);var corners=new Vector3[4];
                foreach(RectTransform piece in timer){piece.GetWorldCorners(corners);foreach(var corner in corners){float x=timer.InverseTransformPoint(corner).x;Require(x>=-8 && x<=542,"Timer decoration escaped its original width");}}
                var badge=(RectTransform)typeof(StyleScoreHud).GetField("badge",Private).GetValue(score.Hud);Require(badge.sizeDelta==new Vector2(108,83),"Multiplier styling must retain its compact size");
                foreach(var text in score.Hud.Canvas.GetComponentsInChildren<TMP_Text>(true))Require(!text.raycastTarget,"Score labels must not steal touches");
                foreach(var image in score.Hud.Canvas.GetComponentsInChildren<Image>(true))Require(!image.raycastTarget,"Score art must not steal touches");
                Report("real pistol projectile awards confirmed damage once, creates floating score text and plays an existing mixed UI recording; all HUD graphics ignore input.");
                var feedback=player.GetComponent<ShootingFeedback>();var center=feedback.NearMissCenter;int before=score.Combo.AwardCount;
                Require(feedback.CheckNearMiss(center+new Vector3(.9f,0,2),center+new Vector3(.9f,0,-2),.02f),"Passing projectile geometry did not register");
                Require(score.Combo.AwardCount==before+1,"Bullet dodge hook did not reward its real close-pass event");
                typeof(PlayerProjectileEvade).GetMethod("RewardPerfect",Private).Invoke(player.GetComponent<PlayerProjectileEvade>(),null);
                Require(score.Combo.AwardCount==before+2,"Perfect dodge must add its bonus");
                enemy.TakeDamage(new DamageInfo(10000,player.gameObject,enemy.transform.position,Vector3.back,Vector3.forward));
                Require(!enemy.IsAlive && score.Combo.AwardCount==before+3,"Actual pistol takedown must award one kill bonus");
                enemy=SpawnEnemy();Next();
            }else if(stage==3 && enemy.HasInitialized){
                enemy.enabled=false;enemy.StopAllCoroutines();manager.TapPistolCanKill=false;
                enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.IsFinisherReady,"Could not prepare sword finisher");int before=score.Combo.AwardCount;
                Require(enemy.DefeatFromSword(player.gameObject,0,.05f),"Real sword death path rejected finisher");
                Require(score.Combo.AwardCount==before+1 && !enemy.DefeatFromSword(player.gameObject),"Sword finisher must score exactly once");
                Report("real bullet near miss, perfect-dodge bonus, pistol kill and sword-finisher death paths award points without repeat death rewards.");
                TestTraffic();
                float remaining=score.Combo.Remaining;manager.SwitchGameState(GameState.Pause);typeof(StyleScoreController).GetMethod("Update",Private).Invoke(score,null);
                Require(score.Combo.Remaining==remaining,"Pause drained combo timer");manager.SwitchGameState(GameState.Playing);
                Next();
            }else if(stage==4 && Time.time-stageAt>.2f){
                Capture("Combo");int pending=score.Combo.Potential;float total=manager.Session.Score;score.Combo.Bank();
                Require(Mathf.Abs(manager.Session.Score-total-pending)<.1f && score.Combo.Moves==0,"Banked combo did not reach existing total/game-over score");
                score.Record(StyleAction.NearMiss,player.transform.position+Vector3.up*2);score.Record(StyleAction.PistolHit,player.transform.position+Vector3.up*2);score.Record(StyleAction.GapPass,player.transform.position+Vector3.up*2);
                int banks=score.Combo.BankCount,broken=score.Combo.BrokenCount,earned=score.Combo.Potential/2;float beforeDamage=manager.Session.Score;
                Require(score.Combo.Multiplier>1,"Damage test must include the multiplier");player.TakeDamage(new DamageInfo(1));
                Require(score.Combo.Moves==0 && score.Combo.Multiplier==1 && score.Combo.BankCount==banks+1 && score.Combo.BrokenCount==broken && Mathf.Abs(manager.Session.Score-beforeDamage-earned)<.1f,"Incoming damage must bank half the multiplied total and reset the chain without touching existing score");
                score.Crash();Require(score.Combo.BankCount==banks+1 && Mathf.Abs(manager.Session.Score-beforeDamage-earned)<.1f,"Repeated hit/kick callbacks must not bank the same points twice");
                score.Record(StyleAction.SwordFinisher,player.transform.position+Vector3.up*2);earned=score.Combo.Potential/2;beforeDamage=manager.Session.Score;
                manager.SwitchGameState(GameState.Hit);
                Require(score.Combo.Moves==0 && score.Combo.Multiplier==1 && Mathf.Abs(manager.Session.Score-beforeDamage-earned)<.1f,"Entering collision Hit state must bank half the pending points");manager.SwitchGameState(GameState.Playing);
                score.Record(StyleAction.SwordFinisher,player.transform.position+Vector3.up*2);score.Record(StyleAction.GapPass,player.transform.position+Vector3.up*2);score.Record(StyleAction.NearMiss,player.transform.position+Vector3.up*2);
                score.Combo.Bank();Capture("Banked");
                var emptyCells=(Image[])typeof(StyleScoreHud).GetField("timerCells",Private).GetValue(score.Hud);foreach(var cell in emptyCells)Require(cell.rectTransform.localScale.x==0,"Banked timer must visibly empty every charge cell");
                manager.StyleScoring.reducedMotion=true;score.Record(StyleAction.BulletDodge,player.transform.position+Vector3.up*2);
                typeof(StyleScoreHud).GetMethod("LateUpdate",Private).Invoke(score.Hud,null);
                var headline=(RectTransform)typeof(StyleScoreHud).GetField("headline",Private).GetValue(score.Hud);
                Require(headline.localScale==Vector3.one && Quaternion.Angle(headline.localRotation,Quaternion.identity)<.01f,"Reduced motion must remove score bounce/rotation");
                Report("pause freezes combo; actual player damage and collision Hit state bank half the multiplied points, reset the chain and multiplier, preserve existing score and never double-credit; normal banking still pays the full amount; reduced motion removes decorative transforms.");
                StyleScoreBoardChecks.Run(score,manager,player);
                score.Record(StyleAction.SwordFinisher,player.transform.position+Vector3.up*2);earned=score.Combo.Potential/2;beforeDamage=manager.Session.Score;banks=score.Combo.BankCount;
                Require(earned>0,"Game-over test needs pending points");manager.SwitchGameState(GameState.GameOver);score.Crash();
                Require(score.Combo.Moves==0 && score.Combo.Multiplier==1 && score.Combo.BankCount==banks+1 && Mathf.Abs(manager.Session.Score-beforeDamage-earned)<.1f,"Game over must bank half the pending points exactly once, including after State has changed");
                Report("game over also banks half the pending combo and credits the final score exactly once; previously banked points stay intact.");
                Finish(true,"PASS native style-scoring integration checks.");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static Enemy SpawnEnemy()=>UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(3,0,20),Quaternion.identity).GetComponent<Enemy>();
    private static void TestStyleAudio(){
        var rig=GameAudio.Active;Require(rig!=null,"Audio rig missing");
        bool carbon=rig.UsingCarbonScorePrototype;
        var assets=AssetDatabase.FindAssets("t:AudioClip",new[]{carbon ? "Assets/Resources/CarbonScorePrototype" : "Assets/Audio/CyberBiker/CleanArcadeScore"});Require(assets.Length==4,"Active score library should contain four selected clips");
        foreach(var guid in assets){
            string path=AssetDatabase.GUIDToAssetPath(guid);var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);clip.LoadAudioData();
            Require(clip.frequency==(carbon ? 44100 : 48000) && clip.channels==(carbon ? 1 : 2) && clip.length>=.19f && clip.length<(carbon ? 1.6f : 1.1f),"Score clips must preserve their intended format and reward tails");
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);Require(importer.defaultSampleSettings.compressionFormat==AudioCompressionFormat.PCM && importer.defaultSampleSettings.loadType==AudioClipLoadType.DecompressOnLoad,"Short score sounds need low-latency PCM import");
            var samples=new float[clip.samples*clip.channels];Require(clip.GetData(samples,0),"Score PCM data unavailable");float peak=0;foreach(float sample in samples){Require(!float.IsNaN(sample),"Score sample is invalid");peak=Mathf.Max(peak,Mathf.Abs(sample));}Require(peak>.5f && peak<.9f,"Score clip must be audible and retain peak headroom");
        }
        for(int i=0;i<8;i++){rig.PlayStyleScore(0,10,.75f);Require(rig.LastStyleScoreClip.name==(carbon ? "CarbonPoint" : "ArcadePoint"),"Points must use the consistent active cue rather than old scratch layers");Require(Mathf.Abs(rig.LastStyleScorePitch-(carbon ? 1 : 1.12f))<.001f,"Point pitch must preserve the active preset");}
        string[] names=carbon ? new[]{"CarbonMultiplier","CarbonBank","CarbonBreak"} : new[]{"ArcadeMultiplier","ArcadeBank","ArcadeBreak"};for(int kind=1;kind<=3;kind++){rig.PlayStyleScore(kind,10,.75f);Require(rig.LastStyleScoreClip.name==names[kind-1] && rig.LastStyleScorePitch==1,"Reward cues must use their dedicated recording at original pitch");}
        Require(GameAudio.StyleScoreKind(StyleAction.PistolHit,false)==0 && GameAudio.StyleScoreKind(StyleAction.NearMiss,false)==4 && GameAudio.StyleScoreKind(StyleAction.SwordFinisher,false)==5 && GameAudio.StyleScoreKind(StyleAction.PistolHit,true)==1,"Score audio hierarchy must distinguish taps, driving, combat and multiplier gains");
        rig.PlayStyleScore(4,10,.75f);Require(rig.LastStyleScoreClip.name==(carbon ? "CarbonPoint" : "ArcadePoint"),"Driving bonuses must preserve the approved short cue");
        rig.PlayStyleScore(5,10,.75f);Require(rig.LastStyleScoreClip.name==names[0] && rig.LastStyleScorePitch==1,"Combat rewards must use the stronger original-pitch cue");
        int count=rig.StyleScoreSoundCount;rig.PlayStyleScore(0,1,0);Require(rig.StyleScoreSoundCount==count,"Muted score setting must remain silent");
        manager.SwitchGameState(GameState.Pause);rig.PlayStyleScore(1,3,1);Require(rig.StyleScoreSoundCount==count,"Paused game must not play score sounds");manager.SwitchGameState(GameState.Playing);
        Report((carbon ? "local Carbon prototype: four original-rate mono UI/drift cues" : "four single-source stereo arcade rewards")+" import as PCM with peak headroom; real pooled playback uses the active point cue, preserves reward pitches and respects score mute and pause.");
    }
    private static void TestWorldPlacement(){
        var world=score.Hud.WorldNumbers;var refresh=typeof(WorldStyleNumbers).GetMethod("Refresh",Private);var view=Camera.main;
        refresh.Invoke(world,new object[]{view,0f,0f});var label=world.FirstActive;Require(label!=null,"No active 3D number");var position=label.position;var scale=label.localScale;var actor=enemy.transform.position;
        enemy.transform.position+=Vector3.right*2;refresh.Invoke(world,new object[]{view,0f,0f});
        Require(Vector3.Distance(label.position,position+Vector3.right*2)<.02f,"3D hit number must follow the actual enemy");
        enemy.transform.position=actor;refresh.Invoke(world,new object[]{view,0f,0f});
        var cameraPosition=view.transform.position;var before=view.WorldToScreenPoint(label.position);view.transform.position+=Vector3.right*2;refresh.Invoke(world,new object[]{view,0f,0f});
        Require(Vector3.Distance(label.position,position)<.02f && Mathf.Abs(view.WorldToScreenPoint(label.position).x-before.x)>10,"Camera movement must produce real parallax without moving the number in world space");
        view.transform.position=label.position-view.transform.forward*4;refresh.Invoke(world,new object[]{view,0f,0f});Require(label.localScale.x<scale.x*.5f,"Close 3D numbers must shrink rather than fill the view");
        view.transform.position=cameraPosition;refresh.Invoke(world,new object[]{view,.1f,0f});var movement=label.position-position;
        Require(Mathf.Abs(movement.x)<.001f && Mathf.Abs(movement.z)<.001f && movement.y>=0 && movement.y<=manager.StyleScoring.worldNumberRise+.01f,"Number motion must only rise smoothly in world units");
        Require(label.localScale==scale && Quaternion.Angle(label.rotation,view.transform.rotation)<.01f,"Number must stay billboarded without bounce or independent rotation");
        Capture("WorldAnchor");
        var plate=label.GetComponentInChildren<WorldScorePlate>();Require(plate!=null && plate.GetComponent<CanvasRenderer>()!=null,"Dark backing needs an actual CanvasRenderer");
        var mesh=plate.canvasRenderer.GetMesh();Require(mesh!=null && mesh.vertexCount==4 && mesh.triangles.Length==6,"Dark backing did not submit its slanted quad");
        var digits=label.GetComponentInChildren<UnityEngine.UI.Text>();Require(Mathf.Abs(plate.rectTransform.rect.width-digits.preferredWidth-22)<.1f && plate.rectTransform.rect.height==64,"Backing must fit tightly around the digits");
        Require(plate.color.a>=.95f && !plate.raycastTarget,"Backing should be dark, visible and ignore touches");
        float backingOpacity=manager.StyleScoring.worldNumberBackgroundOpacity;manager.StyleScoring.worldNumberBackgroundOpacity=0;Capture("NoBacking");manager.StyleScoring.worldNumberBackgroundOpacity=backingOpacity;
        float initialBrightness=digits.color.maxColorComponent;refresh.Invoke(world,new object[]{view,.25f,0f});
        Require(digits.color.maxColorComponent<initialBrightness && digits.color.maxColorComponent<=manager.StyleScoring.worldNumberBrightness+.001f,"Appearance glow must settle smoothly to the normal brightness");Capture("Settled");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="World number occlusion fixture";wall.GetComponent<Collider>().enabled=false;wall.transform.position=Vector3.Lerp(view.transform.position,label.position,.5f);wall.transform.rotation=view.transform.rotation;wall.transform.localScale=new Vector3(4,2.5f,.3f);
        var material=new Material(Shader.Find("Unlit/Color"));material.color=new Color(.07f,.09f,.12f);wall.GetComponent<Renderer>().sharedMaterial=material;
        Capture("Occluded");UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(material);
        Report("numbers use a fixed world-space pool, follow actual enemy translation, show camera parallax, face the camera and only rise/fade without spin or bounce; depth-occlusion preview captured.");
    }
    private static void TestTraffic(){
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_04.prefab");var car=UnityEngine.Object.Instantiate(prefab,player.transform.position,Quaternion.identity).GetComponent<Car>();car.OnPoolRent(prefab);car.enabled=false;
        car.TryGetTrafficShape(0,out var bounds,out _);var bike=player.GetComponent<BoxCollider>().bounds;
        car.transform.position+=new Vector3(bike.center.x+bike.extents.x+bounds.extents.x+.5f-bounds.center.x,0,bike.center.z+2-bounds.center.z);Physics.SyncTransforms();
        var scan=typeof(StyleScoreController).GetMethod("ScanTraffic",Private);scan.Invoke(score,new object[]{60f});int before=score.NearMissCount;
        var clock=typeof(StyleScoreController).GetField("clock",Private);clock.SetValue(score,(float)clock.GetValue(score)+.05f);
        car.transform.position+=Vector3.back*4;Physics.SyncTransforms();scan.Invoke(score,new object[]{60f});scan.Invoke(score,new object[]{60f});
        Require(score.NearMissCount==before+1,"Close pass must use real car/bike bounds and score only once");
        car.TryGetTrafficShape(0,out bounds,out _);float left=bike.max.x,right=bounds.min.x;bool placed=false;
        foreach(var canvas in score.Hud.WorldNumbers.Canvases){var digits=canvas.GetComponentInChildren<UnityEngine.UI.Text>();if(canvas.enabled && digits.text=="+"+manager.StyleScoring.nearMiss){float x=canvas.transform.position.x;Require(x>left && x<right,"Car dodge number must appear in the physical gap between bike and car");Require(canvas.transform.position.y>Mathf.Max(bike.max.y,bounds.max.y),"Car dodge number needs clearance above the vehicles to stay readable");placed=true;}}
        Require(placed,"Close-pass number missing");typeof(WorldStyleNumbers).GetMethod("Refresh",Private).Invoke(score.Hud.WorldNumbers,new object[]{Camera.main,.08f,0f});Capture("CarPass");UnityEngine.Object.Destroy(car.gameObject);
        Report("real traffic-car bounds detect a 0.5-unit close pass once, place its number between the bike and car surfaces, with no duplicate award after passing.");
    }
    private static void Capture(string name){
        var view=Camera.main;var oldTarget=view.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var hud=score.Hud.Canvas;var mode=hud.renderMode;var oldCamera=hud.worldCamera;float distance=hud.planeDistance;var nodes=hud.GetComponentsInChildren<Transform>(true);var layers=new int[nodes.Length];var overlay=new GameObject("Scoring capture camera").AddComponent<Camera>();
        int mask=view.cullingMask;
        try{
            for(int i=0;i<nodes.Length;i++){layers[i]=nodes[i].gameObject.layer;nodes[i].gameObject.layer=31;}
            view.targetTexture=rt;view.cullingMask=mask & ~(1<<31);
            typeof(WorldStyleNumbers).GetMethod("Refresh",Private).Invoke(score.Hud.WorldNumbers,new object[]{view,0f,0f});Canvas.ForceUpdateCanvases();view.Render();
            overlay.CopyFrom(view);overlay.clearFlags=CameraClearFlags.Depth;overlay.cullingMask=1<<31;overlay.allowHDR=false;overlay.enabled=false;overlay.targetTexture=rt;
            hud.renderMode=RenderMode.ScreenSpaceCamera;hud.worldCamera=overlay;hud.planeDistance=1;Canvas.ForceUpdateCanvases();typeof(StyleScoreHud).GetMethod("LateUpdate",Private).Invoke(score.Hud,null);Canvas.ForceUpdateCanvases();overlay.Render();
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/StyleScore/"+name+".png",image.EncodeToPNG());
        }finally{hud.renderMode=mode;hud.worldCamera=oldCamera;hud.planeDistance=distance;for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];view.targetTexture=oldTarget;view.cullingMask=mask;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(overlay.gameObject);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool success,string text){if(done)return;done=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;if(SessionState.GetBool(Key+"HadPrefs",false))PlayerPrefs.SetString(CyberBiker.UI.TestingSettings.PreferenceKey,SessionState.GetString(Key+"Prefs",""));else PlayerPrefs.DeleteKey(CyberBiker.UI.TestingSettings.PreferenceKey);PlayerPrefs.Save();File.AppendAllText("Logs/StyleScore/Result.txt",text+"\n");Debug.Log(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
