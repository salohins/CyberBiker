using System;
using System.Reflection;
using CyberBiker.Player;
using CyberBiker.Scoring;
using Gameplay;
using UnityEngine;
using UnityEngine.Scripting;

namespace CyberBiker.Balance {
    [AttributeUsage(AttributeTargets.Field),Preserve]
    public sealed class BalanceControlAttribute:Attribute {
        public readonly string label,section;
        public readonly float min,max,step;
        public BalanceControlAttribute(string section,string label,float min=0,float max=1,float step=1){this.section=section;this.label=label;this.min=min;this.max=max;this.step=step;}
    }
    public static class BalanceControls {
        public static void Normalize(object settings){
            var defaults=Activator.CreateInstance(settings.GetType());
            foreach(var field in settings.GetType().GetFields()){
                var control=field.GetCustomAttribute<BalanceControlAttribute>();if(control==null)continue;
                if(field.FieldType==typeof(int))field.SetValue(settings,Mathf.Clamp((int)field.GetValue(settings),(int)control.min,(int)control.max));
                else if(field.FieldType==typeof(float)){
                    float value=(float)field.GetValue(settings);
                    if(float.IsNaN(value)||float.IsInfinity(value))value=(float)field.GetValue(defaults);
                    field.SetValue(settings,Mathf.Clamp(value,control.min,control.max));
                }
            }
        }
    }
    // Portable values only: no scene objects, fonts, materials or asset instance IDs.
    [Serializable,Preserve]
    public sealed class ScoreBalanceTuning {
        [BalanceControl("POINTS PER MOVE","Style scoring enabled")] public bool enabled=true;
        [BalanceControl("POINTS PER MOVE","Distance points per world unit",0,1,.001f)] public float distancePointsPerUnit=.01f;
        [BalanceControl("POINTS PER MOVE","Pistol hit",0,10000)] public int pistolHit=40;
        [BalanceControl("POINTS PER MOVE","Pistol kill",0,10000)] public int pistolKill=350;
        [BalanceControl("POINTS PER MOVE","Sword finisher",0,10000)] public int swordFinisher=800;
        [BalanceControl("POINTS PER MOVE","Bullet dodge",0,10000)] public int bulletDodge=150;
        [BalanceControl("POINTS PER MOVE","Perfect dodge bonus",0,10000)] public int perfectDodge=350;
        [BalanceControl("POINTS PER MOVE","Car close shave",0,10000)] public int nearMiss=120;
        [BalanceControl("POINTS PER MOVE","Gap pass",0,10000)] public int gapPass=300;
        [BalanceControl("POINTS PER MOVE","Full throttle",0,10000)] public int speedRun=100;
        [BalanceControl("POINTS PER MOVE","Hard carve",0,10000)] public int hardCarve=75;
        [BalanceControl("COMBO RULES","Combo window (game seconds)",.5f,30,.1f)] public float comboWindow=5;
        [BalanceControl("COMBO RULES","Maximum multiplier",1,20)] public int maximumMultiplier=10;
        [BalanceControl("COMBO RULES","Steps per multiplier",1,20)] public int stepsPerMultiplier=4;
        [BalanceControl("COMBO RULES","Steps for a different move",0,10)] public int varietySteps=2;
        [BalanceControl("COMBO RULES","Steps for a repeated move",0,10)] public int repeatSteps=1;
        [BalanceControl("COMBO RULES","Repeated move value (1 = full)",.1f,1,.01f)] public float repetitionValue=.72f;
        [BalanceControl("COMBO RULES","Minimum repeat value (fraction)",0,1,.01f)] public float minimumRepeatValue=.25f;
        [BalanceControl("COMBO RULES","Minimum points per rewarded move",0,100)] public int minimumAwardPoints=1;
        [BalanceControl("COMBO RULES","Hit payout (0.5 = keep half)",0,1,.01f)] public float impactPayoutFraction=.5f;
        [BalanceControl("COMBO RULES","Hold combo timer during finishers")] public bool holdComboDuringFinisher=true;
        [BalanceControl("DRIVING REWARDS","Minimum driving speed",0,100,1)] public float minimumDrivingSpeed=25;
        [BalanceControl("DRIVING REWARDS","Carve steering threshold",.1f,1,.01f)] public float carveSteering=.75f;
        [BalanceControl("DRIVING REWARDS","Carve hold (game seconds)",.05f,3,.05f)] public float carveHold=.45f;
        [BalanceControl("DRIVING REWARDS","Carve cooldown (game seconds)",0,10,.1f)] public float carveCooldown=2;
        [BalanceControl("DRIVING REWARDS","Alternate sides for carve rewards")] public bool alternateCarveSides=true;
        [BalanceControl("DRIVING REWARDS","Speed threshold (0.9 = 90%)",.1f,1,.01f)] public float speedThreshold=.9f;
        [BalanceControl("DRIVING REWARDS","Speed reward interval (seconds)",.5f,60,.1f)] public float cleanSpeedInterval=8;
        [BalanceControl("DRIVING REWARDS","Prevent passive speed combos")] public bool preventPassiveSpeedChains=true;
        [BalanceControl("DRIVING REWARDS","Speed interval beyond combo (s)",.1f,10,.1f)] public float passiveSpeedGap=1;
        [BalanceControl("DRIVING REWARDS","Driving rewards lockout after hit (s)",0,10,.1f)] public float postHitDrivingCooldown=2;
        [BalanceControl("CAR CLOSE SHAVES","Maximum side clearance (units)",.1f,6,.05f)] public float nearMissClearance=1.2f;
        [BalanceControl("CAR CLOSE SHAVES","Minimum safe clearance (units)",0,1,.01f)] public float minimumNearMissClearance=.03f;
        [BalanceControl("CAR CLOSE SHAVES","Razor-close clearance (units)",0,3,.01f)] public float razorClearance=.35f;
        [BalanceControl("CAR CLOSE SHAVES","Razor-close points multiplier",1,10,.1f)] public float razorMultiplier=2;
        public ScoreBalanceTuning Copy()=>(ScoreBalanceTuning)MemberwiseClone();
        public void Normalize(){BalanceControls.Normalize(this);minimumNearMissClearance=Mathf.Min(minimumNearMissClearance,nearMissClearance);razorClearance=Mathf.Min(razorClearance,nearMissClearance);}
        public static ScoreBalanceTuning Read(StyleScoringSettings source)=>JsonUtility.FromJson<ScoreBalanceTuning>(JsonUtility.ToJson(source));
        public void Apply(StyleScoringSettings target){Normalize();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this),target);}
    }
    [Serializable,Preserve]
    public sealed class TriggerBalanceTuning {
        [BalanceControl("BULLET DODGE SCORE","Bullet scoring radius (units)",.5f,6,.05f)] public float bulletRadius=3.2f;
        [BalanceControl("BULLET DODGE SCORE","Bullet scoring centre height",.5f,4,.05f)] public float bulletCenterHeight=1.6f;
        [BalanceControl("CLOSE-DODGE CINEMATIC","Close-dodge cinematic enabled")] public bool closeDodgeEnabled=true;
        [BalanceControl("CLOSE-DODGE CINEMATIC","Cinematic pass radius (units)",.1f,6,.05f)] public float cinematicRadius=1.8f;
        [BalanceControl("CLOSE-DODGE CINEMATIC","Cinematic centre height",.5f,4,.05f)] public float cinematicCenterHeight=1.6f;
        [BalanceControl("CLOSE-DODGE CINEMATIC","Cinematic cooldown (real s)",0,5,.1f)] public float cinematicCooldown=1.2f;
        [BalanceControl("CLOSE-DODGE CINEMATIC","Cinematic start delay (real s)",0,.5f,.01f)] public float cinematicDelay=.02f;
        [BalanceControl("PERFECT DODGE TIMING","Timed evade enabled")] public bool timedEvade=true;
        [BalanceControl("PERFECT DODGE TIMING","Threat prediction horizon (real s)",.4f,2,.05f)] public float warningHorizon=1.1f;
        [BalanceControl("PERFECT DODGE TIMING","Evade window (real seconds)",.1f,.6f,.01f)] public float evadeWindow=.3f;
        [BalanceControl("PERFECT DODGE TIMING","Perfect window (real seconds)",.04f,.25f,.01f)] public float perfectWindow=.12f;
        [BalanceControl("PERFECT DODGE TIMING","Evade recovery (real seconds)",.15f,1,.01f)] public float evadeRecovery=.42f;
        [BalanceControl("PERFECT DODGE TIMING","Perfect reward cooldown (real s)",0,5,.1f)] public float perfectRewardCooldown=1.2f;
        [BalanceControl("PERFECT DODGE TIMING","Steering-dodge grace (real s)",0,.3f,.01f)] public float steeringDodgeGrace=.05f;
        [BalanceControl("ENEMY SHOT WARNING","Warning before enemy shot (s)",0,1.5f,.05f)] public float firingWarning=.6f;
        [BalanceControl("ENEMY SHOT WARNING","Enemy shot recovery (s)",0,.8f,.05f)] public float enemyShotRecovery=.2f;
        [BalanceControl("ENEMY SHOT WARNING","Helmet warning padding (units)",0,.6f,.01f)] public float helmetSensePadding=.18f;
        [BalanceControl("ENEMY SHOT WARNING","Helmet pulse urgency distance",5,80,1)] public float helmetSenseUrgencyDistance=30;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Gap assist enabled")] public bool gapEnabled=true;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Sideways catch radius (units)",.2f,8,.1f)] public float gapSnapRadius=5;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Predictive side entry")] public bool gapPredictiveSideEntry=true;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Maximum forward catch distance",3,100,1)] public float gapTriggerDistance=65;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Approach lookahead (game s)",.08f,1.5f,.01f)] public float gapApproachTime=.85f;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Minimum side clearance (units)",.02f,.3f,.01f)] public float gapSideClearance=.04f;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Maximum gap width (units)",1,6,.1f)] public float gapMaximumWidth=3;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Minimum gap-assist speed",10,80,1)] public float gapMinimumSpeed=25;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Gap cooldown (game seconds)",.1f,4,.1f)] public float gapCooldown=1.2f;
        [BalanceControl("GAP ASSIST — LAUNCH ONLY","Clearance beyond cars (units)",.2f,5,.1f)] public float gapExitClearance=1.4f;
        [BalanceControl("SWORD FINISHER TARGETING","Full dash / target range (units)",5,300,1)] public float swordTargetRange=150;
        [BalanceControl("SWORD FINISHER TARGETING","Nearby target priority range",4,25,.1f)] public float swordPickupRange=14;
        [BalanceControl("SWORD FINISHER TARGETING","Nearby target priority side reach",2,14,.1f)] public float swordSideRange=9;
        [BalanceControl("SWORD FINISHER TARGETING","Vertical reach (units)",.2f,10,.1f)] public float swordVerticalReach=3;
        [BalanceControl("SWORD FINISHER TARGETING","Dash stop distance (units)",1.5f,4,.1f)] public float swordStopDistance=2.8f;
        [BalanceControl("SWORD FINISHER TARGETING","Right swing side threshold",0,2,.05f)] public float swordRightThreshold=.5f;
        [BalanceControl("SWORD FINISHER TARGETING","Launch cooldown (game s)",0,5,.1f)] public float swordCooldown=.6f;
        [BalanceControl("SWORD FINISHER TARGETING","Delay between finishers (s)",0,.5f,.01f)] public float swordChainDelay=.12f;
        [BalanceControl("SWORD FINISHER TARGETING","Traffic recovery grace (real s)",0,3,.05f)] public float swordTrafficGrace=.8f;
        public TriggerBalanceTuning Copy()=>(TriggerBalanceTuning)MemberwiseClone();
        public void Normalize(){BalanceControls.Normalize(this);perfectWindow=Mathf.Min(perfectWindow,evadeWindow);swordPickupRange=Mathf.Min(swordPickupRange,swordTargetRange);}
        public static TriggerBalanceTuning Read(PlayerController player,GameplayManager game,BikeSwordCombat sword){
            var value=new TriggerBalanceTuning();var feedback=player.GetComponent<ShootingFeedback>();
            if(feedback!=null){value.bulletRadius=feedback.NearMissRadius;value.bulletCenterHeight=feedback.NearMissCenterHeight;}
            var dodge=game.CloseDodge;value.closeDodgeEnabled=dodge.enabled;value.cinematicRadius=dodge.passRadius;value.cinematicCenterHeight=dodge.centerHeight;value.cinematicCooldown=dodge.cooldown;value.cinematicDelay=dodge.delay;
            var shots=game.ProjectileCombat;value.timedEvade=shots.timedEvade;value.warningHorizon=shots.warningHorizon;value.evadeWindow=shots.evadeWindow;value.perfectWindow=shots.perfectWindow;value.evadeRecovery=shots.recovery;value.perfectRewardCooldown=shots.rewardCooldown;value.steeringDodgeGrace=shots.steeringDodgeGrace;
            value.firingWarning=shots.firingWarning;value.enemyShotRecovery=shots.enemyShotRecovery;value.helmetSensePadding=shots.helmetSensePadding;value.helmetSenseUrgencyDistance=shots.helmetSenseUrgencyDistance;
            var gap=game.GapDash;value.gapEnabled=gap.enabled;value.gapSnapRadius=gap.snapRadius;value.gapPredictiveSideEntry=gap.predictiveSideEntry;value.gapTriggerDistance=gap.maximumTriggerDistance;value.gapApproachTime=gap.approachTime;value.gapSideClearance=gap.sideClearance;value.gapMaximumWidth=gap.maximumGapWidth;value.gapMinimumSpeed=gap.minimumSpeed;value.gapCooldown=gap.cooldown;value.gapExitClearance=gap.exitClearance;
            if(sword!=null)sword.ReadBalanceTriggers(value);
            return value;
        }
        public void Apply(PlayerController player,GameplayManager game,BikeSwordCombat sword){
            Normalize();var feedback=player.GetComponent<ShootingFeedback>();
            if(feedback!=null){feedback.NearMissRadius=bulletRadius;feedback.NearMissCenterHeight=bulletCenterHeight;}
            var dodge=game.CloseDodge.Copy();dodge.enabled=closeDodgeEnabled;dodge.passRadius=cinematicRadius;dodge.centerHeight=cinematicCenterHeight;dodge.cooldown=cinematicCooldown;dodge.delay=cinematicDelay;game.SetRuntimeCloseDodge(dodge);
            var shots=game.ProjectileCombat;shots.timedEvade=timedEvade;shots.warningHorizon=warningHorizon;shots.evadeWindow=evadeWindow;shots.perfectWindow=perfectWindow;shots.recovery=evadeRecovery;shots.rewardCooldown=perfectRewardCooldown;shots.steeringDodgeGrace=steeringDodgeGrace;
            shots.firingWarning=firingWarning;shots.enemyShotRecovery=enemyShotRecovery;shots.helmetSensePadding=helmetSensePadding;shots.helmetSenseUrgencyDistance=helmetSenseUrgencyDistance;
            var gap=game.GapDash.Copy();gap.enabled=gapEnabled;gap.snapRadius=gapSnapRadius;gap.predictiveSideEntry=gapPredictiveSideEntry;gap.maximumTriggerDistance=gapTriggerDistance;gap.approachTime=gapApproachTime;gap.sideClearance=gapSideClearance;gap.maximumGapWidth=gapMaximumWidth;gap.minimumSpeed=gapMinimumSpeed;gap.cooldown=gapCooldown;gap.exitClearance=gapExitClearance;game.SetRuntimeGapDash(gap);
            if(sword!=null)sword.ApplyBalanceTriggers(this);
        }
    }
}
