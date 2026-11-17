using System.Collections.Generic;
using CyberBiker.Player;
using CyberBiker.Pooling;
using Gameplay;
using UnityEngine;

namespace CyberBiker.Scoring {
    [DisallowMultipleComponent]
    public sealed class StyleScoreController:MonoBehaviour {
        private struct PassSample {public float z,x,time;public uint generation;}
        private readonly Dictionary<Car,PassSample> passes=new Dictionary<Car,PassSample>(64);
        private readonly List<Car> stale=new List<Car>(64);
        private GameplayManager manager;
        private PlayerController player;
        private Rigidbody body;
        private BoxCollider bikeShape;
        private BikeImpactResponse impact;
        private BikeSwordCombat sword;
        private GapDashAssist gapAssist;
        private StyleScoreHud hud;
        private float clock,nextScan,speedTime,carveTime,nextCarve,dirtyUntil,nextSound;
        private int lastCarveSide,heldCarveSide,kicks;
        public static StyleScoreController Active {get;private set;}
        public StyleCombo Combo {get;private set;}
        public StyleScoringSettings Settings=>manager.StyleScoring;
        public StyleScoreHud Hud=>hud;
        public GameState State=>manager.gameState;
        public int NearMissCount {get;private set;}
        private bool Playing=>isActiveAndEnabled && manager!=null && manager.gameState==GameState.Playing && Settings.enabled;
        private void Awake() {
            manager=GetComponent<GameplayManager>();Active=this;Combo=new StyleCombo();
            Combo.Banked+=Banked;Combo.Broken+=Broken;manager.Session.StateChanged+=StateChanged;
            hud=gameObject.AddComponent<StyleScoreHud>();hud.Initialize(this);
        }
        private void Start(){BindPlayer();}
        private void BindPlayer(){player=FindAnyObjectByType<PlayerController>();if(player==null)return;body=player.GetComponent<Rigidbody>();bikeShape=player.GetComponent<BoxCollider>();impact=player.GetComponent<BikeImpactResponse>();sword=player.GetComponent<BikeSwordCombat>();gapAssist=player.GetComponent<GapDashAssist>();kicks=impact!=null ? impact.KickCount : 0;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatic(){Active=null;}
        public static void Award(StyleAction action,Vector3 point){if(Active!=null)Active.Record(action,point);}
        public void Record(StyleAction action,Vector3 point,int bonus=0,string label=null) {
            if(!Playing || player==null || !player.IsAlive)return;
            int before=Combo.Multiplier;int points=Combo.Add(action,bonus>0 ? bonus : Settings.Points(action),Settings);if(points==0)return;
            // These actions still score, but must not pay for more Launch.
            // The two close-shave rewards inside an assisted gap are part of
            // that same pass, rather than separate energy rewards.
            if(gapAssist==null)gapAssist=player.GetComponent<GapDashAssist>();
            bool refill=action!=StyleAction.PistolHit && action!=StyleAction.PistolKill && action!=StyleAction.GapPass &&
                (action!=StyleAction.NearMiss || gapAssist==null || !gapAssist.IsActive);
            if(refill)sword?.GainLaunchEnergy(points*Combo.Multiplier);
            hud.Move(action,label ?? StyleScoringSettings.Label(action),points,point,Combo.Multiplier>before);
            int sound=Audio.GameAudio.StyleScoreKind(action,Combo.Multiplier>before);
            if(sound==1 || sound==5 || clock>=nextSound) {
                Audio.GameAudio.Active?.PlayStyleScore(sound,Combo.Multiplier,Settings.soundVolume);
                nextSound=clock+.12f;
            }
        }
        public static void BankOnImpact(){if(Active!=null)Active.Crash();}
        // Impacts cash in the configured share of the multiplied value.
        // Duplicate damage/state/kick notifications safely bank an empty chain.
        public void Crash(){dirtyUntil=clock+Settings.postHitDrivingCooldown;speedTime=carveTime=0;Combo.BankAfterImpact(Settings.impactPayoutFraction);}
        public void RefreshBalance(){manager.Session.DistanceScoreRate=Settings.distancePointsPerUnit;Combo.Reconfigure(Settings);hud.RefreshBalance();}
        private void StateChanged(GameState previous,GameState current){if(current==GameState.Hit || current==GameState.GameOver)Crash();}
        private void Update() {
            if(!Playing || player==null || !player.IsAlive)return;
            float dt=Time.deltaTime;if(dt<=0)return;clock+=dt;
            if(impact!=null && impact.KickCount!=kicks){kicks=impact.KickCount;Crash();}
            // Pending chains survive the long sword/close-dodge cinematics.
            if(!Settings.holdComboDuringFinisher || sword==null || !sword.TrafficProtected)Combo.Tick(dt);
            float speed=body.linearVelocity.z;
            if(clock>=nextScan){nextScan=clock+.05f;ScanTraffic(speed);}
            if(clock<dirtyUntil || speed<Settings.minimumDrivingSpeed || !player.grounded){speedTime=carveTime=0;return;}
            speedTime=speed>=player.NormalSpeedLimit*Settings.speedThreshold ? speedTime+dt : 0;
            float interval=Settings.preventPassiveSpeedChains ? Mathf.Max(Settings.comboWindow+Settings.passiveSpeedGap,Settings.cleanSpeedInterval) : Settings.cleanSpeedInterval;
            if(speedTime>=interval) {speedTime=0;Record(StyleAction.SpeedRun,player.transform.position+Vector3.up*2);}
            int side=Mathf.Abs(player.xThrow)>=Settings.carveSteering ? (int)Mathf.Sign(player.xThrow) : 0;
            if(side!=heldCarveSide)carveTime=0;heldCarveSide=side;
            carveTime=side!=0 ? carveTime+dt : 0;
            if(side!=0 && (!Settings.alternateCarveSides || side!=lastCarveSide) && carveTime>=Settings.carveHold && clock>=nextCarve && (sword==null || !sword.TrafficProtected)) {
                lastCarveSide=side;carveTime=0;nextCarve=clock+Settings.carveCooldown;
                Record(StyleAction.HardCarve,player.transform.position+Vector3.up*2);
            }
        }
        private void ScanTraffic(float speed) {
            if(bikeShape==null)return;
            var bike=bikeShape.bounds;var cars=Car.Instances;
            for(int i=0;i<cars.Count;i++) {
                var car=cars[i];if(car==null || !car.TryGetTrafficShape(0,out var bounds,out _))continue;
                float z=bounds.center.z-bike.center.z,x=bounds.center.x-bike.center.x;
                if(Mathf.Abs(z)>45 || Mathf.Abs(x)>15)continue;
                var lease=car.GetComponent<PooledInstance>();uint generation=lease!=null ? lease.Generation : 0;
                if(passes.TryGetValue(car,out var previous) && previous.generation==generation && clock-previous.time<.3f && previous.z>0 && z<=0 && speed>=Settings.minimumDrivingSpeed && clock>=dirtyUntil) {
                    float t=previous.z/Mathf.Max(.001f,previous.z-z);
                    float clearance=Mathf.Abs(Mathf.Lerp(previous.x,x,t))-bounds.extents.x-bike.extents.x;
                    if(clearance>Settings.minimumNearMissClearance && clearance<=Settings.nearMissClearance && (sword==null || !sword.TrafficProtected)) {
                        // Put the reward in the physical gap, rather than on the car's center.
                        var carEdge=bounds.ClosestPoint(bike.center);var bikeEdge=bike.ClosestPoint(carEdge);
                        var rewardPoint=(carEdge+bikeEdge)*.5f;rewardPoint.y=Mathf.Max(bike.max.y,bounds.max.y)+.15f;
                        NearMissCount++;Record(StyleAction.NearMiss,rewardPoint,clearance<Settings.razorClearance ? Mathf.RoundToInt(Settings.nearMiss*Settings.razorMultiplier) : 0,clearance<Settings.razorClearance ? "RAZOR CLOSE" : null);
                    }
                }
                passes[car]=new PassSample{z=z,x=x,time=clock,generation=generation};
            }
            stale.Clear();foreach(var entry in passes)if(entry.Key==null || clock-entry.Value.time>.5f)stale.Add(entry.Key);
            foreach(var car in stale)passes.Remove(car);
        }
        private void Banked(int points){manager.Session.AddBankedCombo(points);hud.Bank(points);Audio.GameAudio.Active?.PlayStyleScore(2,Combo.BankCount,Settings.soundVolume);}
        private void Broken(int points){hud.Break(points);Audio.GameAudio.Active?.PlayStyleScore(3,1,Settings.soundVolume);}
        private void OnDestroy(){if(Active==this)Active=null;if(manager!=null && manager.Session!=null)manager.Session.StateChanged-=StateChanged;if(Combo!=null){Combo.Banked-=Banked;Combo.Broken-=Broken;}}
    }
}
