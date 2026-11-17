using UnityEngine;

namespace CyberBiker.Audio {
    public sealed partial class GameAudio {
        [Header("Style score sounds")]
        [Tooltip("Use the local Carbon reference set if all four clips are present. Takes effect on the next Play; files are excluded from Git.")]
        [SerializeField] private bool useCarbonScorePrototype=true;
        [Tooltip("Short score cue used for pistol hits and driving rewards. Optional alternate clips can be assigned here.")]
        [SerializeField] private AudioClip[] scorePointVariants;
        [SerializeField] private AudioClip scoreMultiplier;
        [SerializeField] private AudioClip scoreBank;
        [SerializeField] private AudioClip scoreBreak;
        [SerializeField,Range(0,1)] private float scorePointVolume=.65f;
        [SerializeField,Range(0,1)] private float scoreMultiplierVolume=.9f;
        [SerializeField,Range(0,1)] private float scoreBankVolume=1;
        [SerializeField,Range(0,1)] private float scoreBreakVolume=.65f;
        [SerializeField,Range(0,1)] private float scoreDrivingBonusVolume=.85f;
        [SerializeField,Range(0,1)] private float scoreCombatBonusVolume=.95f;
        private int lastScorePoint=-1;
        private int lastScoreVoice=-1,lastScoreFrame=-1;
        public int StyleScoreSoundCount {get;private set;}
        public AudioClip LastStyleScoreClip {get;private set;}
        public float LastStyleScorePitch {get;private set;}
        public int LastStyleScoreKind {get;private set;}
        public bool UsingCarbonScorePrototype {get;private set;}
        private void LoadStyleScorePrototype(){
            if(!useCarbonScorePrototype)return;
            var point=Resources.Load<AudioClip>("CarbonScorePrototype/CarbonPoint");
            var multiplier=Resources.Load<AudioClip>("CarbonScorePrototype/CarbonMultiplier");
            var bank=Resources.Load<AudioClip>("CarbonScorePrototype/CarbonBank");
            var lost=Resources.Load<AudioClip>("CarbonScorePrototype/CarbonBreak");
            if(point==null || multiplier==null || bank==null || lost==null)return;
            scorePointVariants=new[]{point};scoreMultiplier=multiplier;scoreBank=bank;scoreBreak=lost;
            UsingCarbonScorePrototype=true;
        }
        private AudioClip NextScorePoint(){
            int count=scorePointVariants!=null ? scorePointVariants.Length : 0;if(count==0)return null;
            int index=count>1 ? (lastScorePoint+1+variation.Next(count-1))%count : 0;
            for(int i=0;i<count;i++){int candidate=(index+i)%count;if(scorePointVariants[candidate]!=null){lastScorePoint=candidate;return scorePointVariants[candidate];}}
            return null;
        }
        // Dedicated rewards through the existing pooled mixer. Carbon recordings
        // retain their original pitch; the fallback has a gentle point pitch rise.
        public static int StyleScoreKind(Scoring.StyleAction action,bool multiplierRaised){
            if(multiplierRaised)return 1;
            switch(action){
                case Scoring.StyleAction.PistolKill:case Scoring.StyleAction.SwordFinisher:case Scoring.StyleAction.PerfectDodge:return 5;
                case Scoring.StyleAction.NearMiss:case Scoring.StyleAction.BulletDodge:case Scoring.StyleAction.GapPass:
                case Scoring.StyleAction.SpeedRun:case Scoring.StyleAction.HardCarve:return 4;
                default:return 0;
            }
        }
        public void PlayStyleScore(int kind,int level,float volume) {
            if(paused || masterVolume<=0 || interfaceVolume<=0 || volume<=0 || kind<0 || kind>5)return;
            AudioClip clip=kind==0 || kind==4 ? NextScorePoint() : kind==1 || kind==5 ? scoreMultiplier : kind==2 ? scoreBank : scoreBreak;
            if(clip==null)return;
            float pitch=(kind==0 || kind==4) && !UsingCarbonScorePrototype ? Mathf.Lerp(1,1.12f,Mathf.Clamp01((level-1)/9f)) : 1;
            float cueVolume=kind==4 ? scoreDrivingBonusVolume : kind==5 ? scoreCombatBonusVolume : kind==0 ? scorePointVolume : kind==1 ? scoreMultiplierVolume : kind==2 ? scoreBankVolume : scoreBreakVolume;
            if(cueVolume<=0)return;
            float gain=Mathf.Clamp01(volume)*Mathf.Sqrt(interfaceVolume)*cueVolume;
            // A kill may follow its final bullet hit in the same frame. Let the
            // larger cue take that slot instead of stacking two score attacks.
            if(kind!=0 && kind!=4 && lastScoreFrame==Time.frameCount && (LastStyleScoreKind==0 || LastStyleScoreKind==4) && lastScoreVoice>=0 && voices[lastScoreVoice].clip==LastStyleScoreClip)
                voices[lastScoreVoice].Stop();
            int voice=Play(clip,Vector3.zero,gain,pitch,true,priority:kind==0 ? 76 : kind==4 ? 65 : 35);
            if(voice<0)return;
            lastScoreVoice=voice;lastScoreFrame=Time.frameCount;
            StyleScoreSoundCount++;LastStyleScoreClip=clip;LastStyleScorePitch=pitch;LastStyleScoreKind=kind;
            if(kind==1 || kind==2 || kind==5){finisherDuckAge=0;UpdateFinisherDucking(0);ApplyRidingLevels();}
        }
    }
}
