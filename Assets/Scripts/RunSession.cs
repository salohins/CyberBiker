using System;
using Gameplay;

namespace CyberBiker.Runs {
    public readonly struct RunSample {
        public float Elapsed { get; }
        public float Score { get; }
        public float Speed { get; }
        public RunSample(float elapsed, float score, float speed) { Elapsed = elapsed; Score = score; Speed = speed; }
    }

    public sealed class RunSession {
        public GameState State { get; private set; }
        public float Score { get; private set; }
        public float Elapsed { get; private set; }
        private float distanceScoreRate=.01f;
        public float DistanceScoreRate {get=>distanceScoreRate;set=>distanceScoreRate=float.IsNaN(value)||float.IsInfinity(value) ? .01f : Math.Max(0,Math.Min(1,value));}
        public event Action<GameState, GameState> StateChanged;
        public event Action<float> ScoreChanged;
        // Fresh distance/direct awards only. Combo actions grant their Launch
        // energy when performed, so later bank transfers must not grant it again.
        public event Action<float> NonComboScoreEarned;
        // A bounded array stores live run history without allocating every frame.
        private readonly RunSample[] samples;
        private int nextSample;
        public int SampleCount { get; private set; }
        public RunSession(GameState initialState, int historyCapacity = 256) {
            State = initialState;
            samples = new RunSample[Math.Max(1, historyCapacity)];
        }
        public bool ChangeState(GameState state) {
            if (state == State || State == GameState.GameOver) return false;
            var previous = State; State = state;
            StateChanged?.Invoke(previous, state);
            return true;
        }
        public void Tick(float deltaTime, float speed) {
            if (State != GameState.Playing || deltaTime <= 0) return;
            Elapsed += deltaTime;
            float earned=Math.Max(0, speed) * deltaTime * distanceScoreRate;
            Score += earned;
            if(earned>0)NonComboScoreEarned?.Invoke(earned);
            samples[nextSample] = new RunSample(Elapsed, Score, speed);
            nextSample = (nextSample + 1) % samples.Length;
            SampleCount = Math.Min(SampleCount + 1, samples.Length);
            ScoreChanged?.Invoke(Score);
        }
        public void AddScore(float points) {
            if(State!=GameState.Playing || points<=0 || float.IsNaN(points) || float.IsInfinity(points))return;
            Score+=points;NonComboScoreEarned?.Invoke(points);ScoreChanged?.Invoke(Score);
        }
        // Combo.Bank clears its chain before invoking the credit callback.
        // State-change callbacks run after State becomes Hit/GameOver, so those
        // transitions must still accept this already-earned, exact-once credit.
        public void AddBankedCombo(int points) {
            if(points<=0 || (State!=GameState.Playing && State!=GameState.Hit && State!=GameState.GameOver))return;
            Score+=points;ScoreChanged?.Invoke(Score);
        }
        public RunSample GetSample(int chronologicalIndex) {
            if (chronologicalIndex < 0 || chronologicalIndex >= SampleCount) throw new ArgumentOutOfRangeException(nameof(chronologicalIndex));
            int oldest = SampleCount == samples.Length ? nextSample : 0;
            return samples[(oldest + chronologicalIndex) % samples.Length];
        }
    }
}
