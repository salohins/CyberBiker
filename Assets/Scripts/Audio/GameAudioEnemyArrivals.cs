using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace CyberBiker.Audio {
    public sealed partial class GameAudio {
        [Header("Enemy arrivals")]
        [SerializeField] private AudioClip enemyArrivalEngine;
        [SerializeField] private AudioClip enemyOvertakeWhoosh;
        [SerializeField,Range(0,1)] private float enemyArrivalEngineVolume=.7f;
        [SerializeField,Range(0,1)] private float enemyOvertakeWhooshVolume=1;
        [Tooltip("Audible engine range in world units. A custom curve keeps distant riders quiet and close passes clear.")]
        [SerializeField,Range(30,160)] private float enemyArrivalAudibleRange=100;
        [Tooltip("Maximum sideways distance for the one-shot overtake rush.")]
        [SerializeField,Range(3,20)] private float enemyOvertakeWhooshDistance=12;
        [Tooltip("Gentle physical Doppler on the recorded onboard engine. The whoosh keeps its recorded pitch.")]
        [SerializeField,Range(0,1)] private float enemyArrivalDoppler=.35f;
        [SerializeField,Range(0,1)] private float enemyArrivalReverbAmount=.3f;
        [Tooltip("Real seconds to fade out the arrival engine after the rider settles ahead.")]
        [SerializeField,Range(.2f,2)] private float enemyArrivalEngineRelease=1;
        private sealed class ArrivalVoice {
            public Enemy owner;
            public AudioSource engine;
            public AudioLowPassFilter lowPass;
            public AudioReverbFilter reverb;
            public float envelope,previousGap,pitchVariation;
            public bool crossed,held;
        }
        private readonly List<ArrivalVoice> arrivalVoices=new List<ArrivalVoice>(10);
        private bool[] enemyArrivalWhooshVoices;
        private int lastEnemyWhoosh=-1;
        public int EnemyArrivalEngineCount {get;private set;}
        public int EnemyOvertakeWhooshCount {get;private set;}
        public int ActiveEnemyArrivalVoiceCount {get {int count=0;foreach(var voice in arrivalVoices)if(voice.owner!=null)count++;return count;}}
        public AudioSource LastEnemyOvertakeAudio=>lastEnemyWhoosh>=0 ? voices[lastEnemyWhoosh] : null;
        public AudioSource EnemyArrivalSource(Enemy enemy){foreach(var voice in arrivalVoices)if(voice.owner==enemy)return voice.engine;return null;}

        public void BeginEnemyArrival(Enemy enemy,bool fromBehind) {
            if(!fromBehind || enemy==null || enemyArrivalEngine==null)return;
            ArrivalVoice available=null;
            foreach(var voice in arrivalVoices) {
                if(voice.owner==enemy)return;
                if(voice.owner==null && available==null)available=voice;
            }
            // Difficulty caps live riders at ten; reserve two fading tails too.
            if(available==null && arrivalVoices.Count<12) {
                var source=CreateSource("Enemy arrival engine",true);
                source.spatialBlend=1;source.spread=0;source.minDistance=6;source.priority=90;
                source.velocityUpdateMode=AudioVelocityUpdateMode.Dynamic;
                source.rolloffMode=AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,new AnimationCurve(
                    new Keyframe(0,1),new Keyframe(.06f,1),new Keyframe(.2f,.7f),new Keyframe(.45f,.28f),new Keyframe(.8f,.04f),new Keyframe(1,0)));
                available=new ArrivalVoice{engine=source,lowPass=source.gameObject.AddComponent<AudioLowPassFilter>(),reverb=CreateEngineReverb(source)};
                arrivalVoices.Add(available);
            }
            if(available==null)return;
            available.owner=enemy;available.envelope=0;available.crossed=false;available.held=false;
            available.pitchVariation=Range(-.025f,.025f);
            available.previousGap=player!=null ? enemy.transform.position.z-player.transform.position.z : -100;
            available.engine.Stop();available.engine.transform.position=enemy.transform.position+Vector3.up;
            available.engine.clip=enemyArrivalEngine;available.engine.volume=0;available.engine.dopplerLevel=enemyArrivalDoppler;
            available.engine.maxDistance=enemyArrivalAudibleRange;
            // Use a long recording, with different start offsets for simultaneous riders.
            available.engine.time=(arrivalVoices.IndexOf(available)*3.71f)%Mathf.Max(1,enemyArrivalEngine.length-2);
            available.engine.Play();EnemyArrivalEngineCount++;
            if(paused || (manager!=null && manager.gameState!=GameState.Playing)){available.engine.Pause();available.held=true;}
        }
        private void SetEnemyArrivalPaused(bool held) {
            foreach(var voice in arrivalVoices)if(voice.owner!=null && voice.held!=held) {
                voice.held=held;if(held)voice.engine.Pause();else voice.engine.UnPause();
                voice.reverb.enabled=!held && enemyArrivalReverbAmount>.001f;
            }
        }
        private void UpdateEnemyArrivalAudio(float dt) {
            if(arrivalVoices.Count==0)return;
            var view=Camera.main;
            bool playing=!paused && manager!=null && manager.gameState==GameState.Playing;
            SetEnemyArrivalPaused(!playing);
            foreach(var voice in arrivalVoices) {
                if(voice.owner==null) {
                    voice.reverb.enabled=false;
                    if(voice.engine.isPlaying){voice.engine.Stop();voice.engine.volume=0;}
                    continue;
                }
                bool alive=voice.owner.gameObject.activeInHierarchy && (!voice.owner.HasInitialized || voice.owner.IsAlive);
                if(manager!=null && manager.gameState==GameState.GameOver){RetireArrivalVoice(voice);continue;}
                if(!playing)continue;
                voice.engine.transform.position=voice.owner.transform.position+Vector3.up;
                float gap=player!=null ? voice.owner.transform.position.z-player.transform.position.z : voice.previousGap;
                float lateral=player!=null ? Mathf.Abs(voice.owner.transform.position.x-player.transform.position.x) : 100;
                float closing=player!=null ? voice.owner.ForwardSpeed-player.zVelocity : 0;
                // Crossing the rider's plane, not a timer: one cue per arrival.
                if(alive && !voice.crossed && voice.previousGap< -4 && gap>= -4) {
                    voice.crossed=true;
                    if(lateral<=enemyOvertakeWhooshDistance && closing>3 && Mathf.Abs(voice.owner.transform.position.y-player.transform.position.y)<4)
                        PlayEnemyOvertake(voice);
                }
                voice.previousGap=gap;
                bool approaching=alive && voice.owner.IsArriving;
                voice.envelope=Mathf.MoveTowards(voice.envelope,approaching ? 1 : 0,dt/(approaching ? .25f : alive ? Mathf.Max(.2f,enemyArrivalEngineRelease) : .15f));
                voice.engine.volume=masterVolume*enemyArrivalEngineVolume*voice.envelope;
                voice.engine.pitch=Mathf.Lerp(voice.engine.pitch,Mathf.Lerp(1.02f,1.3f,Mathf.Clamp01(closing/60))+voice.pitchVariation,1-Mathf.Exp(-dt*5));
                voice.engine.dopplerLevel=enemyArrivalDoppler;voice.engine.maxDistance=Mathf.Max(30,enemyArrivalAudibleRange);
                if(view!=null) {
                    var camera=view.transform;var direction=(voice.engine.transform.position-camera.position).normalized;
                    float front=(Vector3.Dot(direction,camera.forward)+1)*.5f;
                    voice.lowPass.cutoffFrequency=Mathf.Lerp(4500,16000,front);
                }
                float reverb=Mathf.Clamp01(enemyArrivalReverbAmount);
                voice.reverb.enabled=reverb>.001f;
                if(voice.reverb.enabled) {
                    float level=2000*Mathf.Log10(Mathf.Max(.00001f,reverb));
                    voice.reverb.decayTime=Mathf.Lerp(.25f,.8f,reverb);
                    voice.reverb.reflectionsLevel=Mathf.Clamp(-600+level,-10000,1000);
                    voice.reverb.reverbLevel=Mathf.Clamp(-350+level,-10000,2000);
                }
                if(!approaching && voice.envelope<=0)RetireArrivalVoice(voice);
            }
        }
        private void PlayEnemyOvertake(ArrivalVoice voice) {
            var clip=enemyOvertakeWhoosh!=null ? enemyOvertakeWhoosh : passWhoosh;if(clip==null)return;
            int index=Play(clip,voice.owner.transform.position,enemyOvertakeWhooshVolume,Range(.98f,1.02f),false,voice.owner.transform,whoosh:true,priority:60);
            if(index<0)return;
            voices[index].spatialBlend=1;voices[index].minDistance=8;voices[index].maxDistance=90;voices[index].dopplerLevel=0;
            enemyArrivalWhooshVoices[index]=true;ConfigureWhooshReverb(index);
            lastEnemyWhoosh=index;EnemyOvertakeWhooshCount++;
        }
        private static void RetireArrivalVoice(ArrivalVoice voice) {
            voice.engine.Stop();voice.engine.volume=0;voice.reverb.enabled=false;voice.owner=null;voice.held=false;voice.envelope=0;
        }
    }
}
