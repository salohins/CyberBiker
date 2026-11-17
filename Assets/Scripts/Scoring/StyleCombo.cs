using System;
using UnityEngine;

namespace CyberBiker.Scoring {
    // Accounting only. Gameplay owns qualifying actions; presentation subscribes
    // to completed awards. Pending points never reach the run until banked.
    public sealed class StyleCombo {
        public int BasePoints {get;private set;}
        public int Multiplier {get;private set;}=1;
        public int Moves {get;private set;}
        public int Potential=>BasePoints*Multiplier;
        public int BestCombo {get;private set;}
        public int BankedPoints {get;private set;}
        public int BankCount {get;private set;}
        public int BrokenCount {get;private set;}
        public int AwardCount {get;private set;}
        public float Remaining {get;private set;}
        private int steps,repeats;
        private StyleAction last;
        public event Action<int> Banked;
        public event Action<int> Broken;
        public int Add(StyleAction action,int points,StyleScoringSettings settings) {
            if(points<=0)return 0;
            bool variety=Moves>0 && last!=action;
            repeats=Moves>0 && last==action ? repeats+1 : 0;
            int earned=Mathf.Max(Mathf.Max(0,settings.minimumAwardPoints),Mathf.RoundToInt(points*Mathf.Max(Mathf.Clamp01(settings.minimumRepeatValue),Mathf.Pow(Mathf.Clamp(settings.repetitionValue,.1f,1),repeats))));
            if(earned<=0)return 0;
            BasePoints=Mathf.Min(10000000,BasePoints+earned);Moves++;AwardCount++;
            steps+=Mathf.Clamp(variety ? settings.varietySteps : settings.repeatSteps,0,10);
            Reconfigure(settings);
            last=action;Remaining=Mathf.Clamp(settings.comboWindow,.5f,30);return earned;
        }
        public void Reconfigure(StyleScoringSettings settings){Multiplier=Mathf.Clamp(1+steps/Mathf.Max(1,settings.stepsPerMultiplier),1,Mathf.Clamp(settings.maximumMultiplier,1,20));Remaining=Mathf.Min(Remaining,Mathf.Clamp(settings.comboWindow,.5f,30));}
        public void Tick(float deltaTime) {
            if(Moves==0 || deltaTime<=0)return;
            Remaining=Mathf.Max(0,Remaining-deltaTime);if(Remaining==0)Bank();
        }
        public void Bank()=>BankTotal(Potential);
        public void BankAfterImpact(float payout=.5f)=>BankTotal(Mathf.FloorToInt(Potential*Mathf.Clamp01(payout)));
        private void BankTotal(int total) {
            if(Moves==0)return;
            BankedPoints+=total;BestCombo=Mathf.Max(BestCombo,total);BankCount++;Clear();Banked?.Invoke(total);
        }
        public void Break() {
            if(Moves==0)return;
            int lost=Potential;BrokenCount++;Clear();Broken?.Invoke(lost);
        }
        private void Clear(){BasePoints=steps=repeats=Moves=0;Multiplier=1;Remaining=0;}
    }
}
