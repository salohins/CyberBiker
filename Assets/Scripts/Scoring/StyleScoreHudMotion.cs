using UnityEngine;
using UnityEngine.UI;

namespace CyberBiker.Scoring {
    public sealed partial class StyleScoreHud {
        private Image rewardFlash,energySweep,timerHead;
        private readonly Image[] speedMarks=new Image[4];
        private readonly RectTransform[] badgeEchoes=new RectTransform[2];
        private readonly CanvasGroup[] badgeEchoAlpha=new CanvasGroup[2];
        private CanvasGroup badgeAlpha;
        private float scoreStretch,scoreVelocity,badgeStretch,badgeVelocity;
        private float sweepAge=10,sweepPower,echoAge=10,flashAge=10;
        private float rewardStrength,visibleCharge,comboEnergy;
        private int lastVisualMultiplier=1,bankedMultiplier=1,bankTarget;
        private float bankStart;

        private void InitializeBoardMotion(){
            // Cached graphics inside the existing plate; no new reward objects.
            var mask=Node("Reward energy mask",root,new Vector2(-4,8),new Vector2(550,126));
            mask.localRotation=Quaternion.Euler(0,0,-2);
            mask.gameObject.AddComponent<RectMask2D>();mask.SetSiblingIndex(1);
            rewardFlash=Image("Reward plate tint",mask,Vector2.zero,new Vector2(550,126),Color.clear);
            energySweep=Image("Energy sweep",mask,new Vector2(-300,0),new Vector2(38,180),Color.clear);
            energySweep.rectTransform.localRotation=Quaternion.Euler(0,0,-16);
            for(int i=0;i<speedMarks.Length;i++)
                speedMarks[i]=Image("Reward speed mark "+i,mask,Vector2.zero,new Vector2(22+i*8,1.5f),Color.clear);
            badgeAlpha=badge.gameObject.AddComponent<CanvasGroup>();
            badgeAlpha.blocksRaycasts=false;badgeAlpha.interactable=false;
            var corners=new[]{new Vector2(-54,-26),new Vector2(-40,-40),new Vector2(40,-40),new Vector2(54,-26),new Vector2(54,26),new Vector2(40,40),new Vector2(-40,40),new Vector2(-54,26)};
            for(int i=0;i<badgeEchoes.Length;i++){
                var echo=Node("Multiplier echo "+i,root,new Vector2(222,16),new Vector2(108,83));
                var alpha=echo.gameObject.AddComponent<CanvasGroup>();alpha.alpha=0;alpha.blocksRaycasts=false;alpha.interactable=false;
                for(int edge=0;edge<corners.Length;edge++)
                    Stroke("Echo edge "+edge,echo,corners[edge],corners[(edge+1)%corners.Length],i==0 ? 2 : 1,new Color(.12f,.9f,1));
                badgeEchoes[i]=echo;badgeEchoAlpha[i]=alpha;
            }
            timerHead=Image("Timer charge head",bar,Vector2.zero,new Vector2(4,10),Color.clear);
            timerHead.rectTransform.anchorMin=timerHead.rectTransform.anchorMax=new Vector2(0,.5f);
        }

        private static float RewardStrength(StyleAction action){
            switch(action){
                case StyleAction.SwordFinisher:return 1;
                case StyleAction.PerfectDodge:return .95f;
                case StyleAction.PistolKill:return .85f;
                case StyleAction.GapPass:return .8f;
                case StyleAction.NearMiss:case StyleAction.BulletDodge:return .65f;
                case StyleAction.SpeedRun:case StyleAction.HardCarve:return .5f;
                default:return .32f;
            }
        }
        private void BeginBoardReward(StyleAction action,int points,bool levelUp){
            lastVisualMultiplier=owner.Combo.Multiplier;
            badgeAlpha.alpha=1;badge.anchoredPosition=new Vector2(222,16);
            rewardStrength=levelUp ? 1 : RewardStrength(action);flashAge=0;
            if(!Motion){ResetBoardSprings();return;}
            // Add velocity rather than restart a tween, so rapid taps flow together.
            scoreVelocity=Mathf.Clamp(scoreVelocity+Mathf.Lerp(2.7f,5.4f,rewardStrength),-8,8);
            if(levelUp){
                badgeVelocity=Mathf.Clamp(badgeVelocity+8,-10,10);echoAge=0;
            }
            if(levelUp || rewardStrength>=.65f){sweepAge=0;sweepPower=rewardStrength;}
        }
        private void BeginBoardBank(int points){
            bankStart=shownPoints;bankTarget=points;bankedMultiplier=lastVisualMultiplier;
            rewardStrength=1;sweepAge=echoAge=flashAge=0;sweepPower=1;
            if(Motion){scoreVelocity=8;badgeVelocity=7;}
            else ResetBoardSprings();
        }
        private void BeginBoardLoss(){
            rewardStrength=.35f;flashAge=0;sweepAge=echoAge=10;
            ResetBoardSprings();badgeAlpha.alpha=1;
            foreach(var shard in shards){shard.age=10;shard.image.color=Color.clear;}
        }
        private float BankedDisplayValue(){
            if(!Motion)return bankTarget;
            float t=Mathf.Clamp01((age-.035f)/Mathf.Max(.15f,Settings.boardBankCountSeconds));
            return Mathf.Lerp(bankStart,bankTarget,1-Mathf.Pow(1-t,3));
        }
        private void ResetBoardSprings(){scoreStretch=scoreVelocity=badgeStretch=badgeVelocity=0;}
        private static void StepSpring(ref float value,ref float velocity,float dt){
            float left=Mathf.Min(.06f,Mathf.Max(0,dt));
            while(left>0){
                float step=Mathf.Min(left,.016f);left-=step;
                velocity+=(-235*value-24*velocity)*step;value+=velocity*step;
            }
            value=Mathf.Clamp(value,-.055f,.3f);
        }
        private void UpdateBoardMotion(float dt,bool live,float charge,Color timerColor,float amount){
            sweepAge+=dt;echoAge+=dt;flashAge+=dt;
            float targetEnergy=live ? Mathf.Clamp01((owner.Combo.Multiplier-1)/9f)*Settings.boardComboEnergy : 0;
            comboEnergy=Motion ? Mathf.Lerp(comboEnergy,targetEnergy,1-Mathf.Exp(-dt*5)) : targetEnergy;
            if(Motion){
                StepSpring(ref scoreStretch,ref scoreVelocity,dt);StepSpring(ref badgeStretch,ref badgeVelocity,dt);
            }else ResetBoardSprings();
            float scoreKick=scoreStretch*(Settings.boardScorePunch/.18f)*amount;
            float multiplierKick=badgeStretch*(Settings.boardMultiplierPunch/.26f)*amount;
            headline.localScale=new Vector3(1+scoreKick,1+scoreKick*.72f,1);
            headline.localRotation=Quaternion.Euler(0,0,-scoreKick*3);
            headline.anchoredPosition=new Vector2(-38,16+scoreKick*10);
            badge.localScale=Vector3.one*(1+multiplierKick);
            badge.localRotation=Quaternion.Euler(0,0,-multiplierKick*4);
            badge.anchoredPosition=new Vector2(222,16);badgeAlpha.alpha=1;
            if(banking){
                // Consume the last multiplier into the banked total, then settle.
                float consume=Motion ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.4f,age)) : 1;
                badge.anchoredPosition=Vector2.Lerp(new Vector2(222,16),new Vector2(95,24),consume);
                badge.localScale*=Mathf.Lerp(1,.72f,consume);badgeAlpha.alpha=1-consume;
                badge.gameObject.SetActive(Motion && consume<1);
                if(Motion)root.localRotation=Quaternion.Euler(0,0,Mathf.Sin(age*25)*Mathf.Exp(-age*10)*1.2f*amount);
            }
            chevrons.anchoredPosition=new Vector2(-240+scoreKick*16,16);
            float gainFlash=Motion ? Mathf.Exp(-flashAge*18)*rewardStrength : 0;
            float sweep=Motion ? Mathf.Clamp01(sweepAge/.42f) : 1;
            energySweep.rectTransform.anchoredPosition=new Vector2(Mathf.Lerp(-310,310,1-Mathf.Pow(1-sweep,2)),0);
            var tint=Color.Lerp(new Color(.12f,.9f,1),accent,.35f);
            energySweep.color=new Color(tint.r,tint.g,tint.b,Motion ? Mathf.Sin(sweep*Mathf.PI)*Settings.boardSweepStrength*sweepPower*amount : 0);
            rewardFlash.color=new Color(tint.r,tint.g,tint.b,Mathf.Min(.16f,gainFlash*.11f*amount));
            for(int i=0;i<speedMarks.Length;i++){
                float t=Motion ? Mathf.Clamp01((sweepAge-i*.035f)/.34f) : 1;
                speedMarks[i].rectTransform.anchoredPosition=new Vector2(Mathf.Lerp(-300,300,1-Mathf.Pow(1-t,2)),-43+i*27);
                speedMarks[i].color=new Color(tint.r,tint.g,tint.b,Motion ? Mathf.Sin(t*Mathf.PI)*(.35f+.3f*comboEnergy)*sweepPower*amount : 0);
            }
            for(int i=0;i<badgeEchoes.Length;i++){
                float t=Motion ? Mathf.Clamp01((echoAge-i*.06f)/.42f) : 1;
                badgeEchoes[i].localScale=Vector3.one*(1+t*.25f*amount);
                badgeEchoAlpha[i].alpha=Motion && echoAge>=i*.06f ? (1-t)*(1-t)*.65f*amount : 0;
            }
            visibleCharge=live ? Motion ? Mathf.Lerp(visibleCharge,charge,1-Mathf.Exp(-dt*(charge>visibleCharge ? 32 : 20))) : charge : 0;
            for(int i=0;i<timerCells.Length;i++){
                float fill=Mathf.Clamp01(visibleCharge*timerCells.Length-i);
                timerCells[i].rectTransform.localScale=new Vector3(fill,1,1);
                float head=1-Mathf.Clamp01(Mathf.Abs(i+.5f-visibleCharge*timerCells.Length));
                var color=Color.Lerp(timerColor,Color.white,head*.7f+gainFlash*.25f);
                color.a=Mathf.Clamp01(.83f+comboEnergy*.17f);timerCells[i].color=color;
            }
            timerHead.rectTransform.anchoredPosition=new Vector2(visibleCharge*534,0);
            timerHead.color=live ? Color.Lerp(timerColor,Color.white,.6f) : Color.clear;
            pointsText.color=Color.Lerp(Color.white,accent,gainFlash*.25f);
        }
    }
}
