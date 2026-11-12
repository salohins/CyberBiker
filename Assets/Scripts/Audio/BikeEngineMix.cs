using System;
using UnityEngine;

namespace CyberBiker.Audio {
    [Serializable]
    public sealed class EngineTuning {
        [Min(500)] public float idleRpm = 1650;
        [Min(2000)] public float redlineRpm = 15500;
        [Min(2000)] public float upshiftRpm = 13200;
        [Min(1000)] public float downshiftRpm = 4500;
        [Min(0.1f)] public float wheelRadius = 0.31f;
        [Min(0.1f)] public float primaryRatio = 1.955f;
        [Min(0.1f)] public float finalDrive = 2.867f;
        [Tooltip("Virtual road speed at Reference Speed; audio calibration only.")]
        [Min(1)] public float virtualTopSpeed = 70;
        [Range(0.05f, 0.5f)] public float shiftDuration = 0.09f;
        [Range(0, 1)] public float cruiseThrottle = 0.32f;
    }

    // A virtual drivetrain observes motion; it never applies forces or changes actual bike gears.
    public sealed class BikeEngineMix {
        private static readonly float[] Ratios = { 2.6f, 2f, 1.67f, 1.44f, 1.29f, 1.15f };
        private readonly EngineTuning tuning;
        private float previousSpeed, shiftRemaining, gearHold;
        public int Gear { get; private set; } = 1;
        public float EngineRpm { get; private set; }
        public float Throttle { get; private set; }
        public float Load { get; private set; }
        public bool IsShifting => shiftRemaining > 0;
        public float Rpm => Mathf.Clamp01(EngineRpm / Mathf.Max(2000, tuning.redlineRpm));
        public float Pitch => EngineRpm / 8000f;
        public float ShiftGain => IsShifting ? 0.35f : 1;
        public BikeEngineMix() : this(new EngineTuning()) { }
        public BikeEngineMix(EngineTuning tuning) { this.tuning = tuning; EngineRpm = tuning.idleRpm; }

        public void Update(float speed, float referenceSpeed, int gears, float deltaTime, bool driving = true, bool grounded = true) {
            if (deltaTime <= 0) return;
            float dt = Mathf.Min(deltaTime, 0.1f);
            speed = Mathf.Max(0, speed);
            float acceleration = (speed - previousSpeed) / dt;
            previousSpeed = speed;
            float normalized = speed / Mathf.Max(1, referenceSpeed);
            float demand = !driving || acceleration < -2 ? 0 : normalized < 0.96f ? 1 : tuning.cruiseThrottle;
            Throttle = Mathf.MoveTowards(Throttle, demand, dt * 4);
            gearHold += dt;
            shiftRemaining = Mathf.Max(0, shiftRemaining - dt);
            gears = Mathf.Clamp(gears, 1, Ratios.Length);
            Gear = Mathf.Min(Gear, gears);
            float wheelRpm = normalized * tuning.virtualTopSpeed * 60 / (2 * Mathf.PI * Mathf.Max(0.1f, tuning.wheelRadius));
            float coupling = wheelRpm * tuning.primaryRatio * tuning.finalDrive;
            float coupledRpm = coupling * Ratios[Gear - 1];
            if (!IsShifting && gearHold >= 0.3f && grounded) {
                if (coupledRpm > tuning.upshiftRpm && Gear < gears) Shift(Gear + 1);
                else if (coupledRpm < tuning.downshiftRpm && Gear > 1) Shift(Gear - 1);
                coupledRpm = coupling * Ratios[Gear - 1];
            }
            float engagement = Mathf.SmoothStep(0, 1, Mathf.Clamp01(normalized / 0.1f));
            float freeRpm = tuning.idleRpm + Throttle * (grounded ? 3500 : tuning.redlineRpm - tuning.idleRpm);
            float target = grounded ? Mathf.Lerp(freeRpm, coupledRpm, engagement) : Throttle > 0.05f ? Mathf.Max(EngineRpm, tuning.idleRpm + Throttle * (tuning.redlineRpm - tuning.idleRpm) * 3) : tuning.idleRpm;
            target = Mathf.Clamp(target, tuning.idleRpm, tuning.redlineRpm);
            EngineRpm = Mathf.MoveTowards(EngineRpm, target, dt * (target > EngineRpm ? 24000 : 18000));
            float targetLoad = IsShifting ? 0 : grounded ? Throttle : Throttle * 0.25f;
            Load = Mathf.MoveTowards(Load, targetLoad, dt * 6);
        }
        private void Shift(int nextGear) { Gear = nextGear; shiftRemaining = tuning.shiftDuration; gearHold = 0; }
    }
}
