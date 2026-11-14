using UnityEngine;

namespace CyberBiker.Player {
    // Preserve each tap through the initial draw and consume it exactly once.
    public sealed class PistolTapSession {
        public int PendingShots {get;private set;}
        public bool Pending => PendingShots>0;
        public float Remaining {get;private set;}
        public bool Ready => Remaining>0;
        public void Request(float hold){PendingShots++;Remaining=Mathf.Max(.3f,hold);}
        public void Fired(float hold){if(PendingShots>0)PendingShots--;Remaining=Mathf.Max(.3f,hold);}
        public void Tick(float dt,bool drawing){if(drawing && Pending)return;Remaining=Mathf.Max(0,Remaining-Mathf.Max(0,dt));if(Remaining<=0)PendingShots=0;}
        public void Cancel(){PendingShots=0;Remaining=0;}
    }
}
