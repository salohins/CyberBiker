using UnityEngine;
namespace CyberBiker.Audio {
    public sealed partial class GameAudio {
        private void PlayEvadeFeedback(SoundEvent sound){
            if(paused || masterVolume<=0)return;
            if(sound.Cue==SoundCue.BulletEvade){Play(bulletNearMiss,sound.Position,bulletNearMissVolume,1.08f,true,priority:28);return;}
            if(sound.Cue==SoundCue.PerfectEvade){Play(pickup,Vector3.zero,interfaceVolume*.65f,1.15f,true,priority:26);return;}
            Play(uiClick,sound.Position,interfaceVolume*(sound.Cue==SoundCue.EvadeReady ? .65f : .35f),sound.Cue==SoundCue.EvadeReady ? 1.45f : .8f,sound.Cue==SoundCue.EvadeReady,priority:30);
            finisherDuckAge=0;UpdateFinisherDucking(0);ApplyRidingLevels();
        }
        [Header("Pistol combat feedback")]
        [SerializeField] private AudioClip pistolHitConfirm;
        [SerializeField] private AudioClip playerDamageThud;
        [SerializeField] private AudioClip bulletNearMiss;
        [SerializeField,Range(0,1)] private float hitConfirmVolume=.8f;
        [SerializeField,Range(0,1)] private float playerDamageVolume=.28f;
        [SerializeField,Range(0,1)] private float bulletNearMissVolume=.9f;
        public int BulletNearMissSoundCount {get;private set;}
        public int PistolHitSoundCount {get;private set;}
        private void PlayGunFeedback(SoundEvent sound){
            if(paused || masterVolume<=0)return;
            if(sound.Cue==SoundCue.PistolHitConfirm){if(Play(pistolHitConfirm,sound.Position,hitConfirmVolume*weaponVolume,Range(.97f,1.03f),true,priority:38)>=0)PistolHitSoundCount++;return;}
            if(sound.Cue==SoundCue.PlayerHurt){Play(playerDamageThud,sound.Position,playerDamageVolume,1,true,priority:35);return;}
            int index=Play(bulletNearMiss,sound.Position,bulletNearMissVolume,Range(.98f,1.1f),false,priority:32);
            if(index<0)return;voices[index].spatialBlend=1;voices[index].minDistance=14;voices[index].maxDistance=65;voices[index].dopplerLevel=0;BulletNearMissSoundCount++;
        }
    }
}
