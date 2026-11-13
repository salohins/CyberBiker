using System;
using TMPro;
using CyberBiker.Runs;

namespace CyberBiker.UI {
    public sealed class ScorePresenter : IDisposable {
        private readonly RunSession session;
        private readonly TMP_Text[] labels;
        private int displayedScore=int.MinValue;
        public ScorePresenter(RunSession session, TMP_Text[] labels) {
            this.session = session; this.labels = labels;
            session.ScoreChanged += Render;
            Render(session.Score);
        }
        private void Render(float value) {
            if (labels == null) return;
            int score=(int)value;
            if(score==displayedScore)return;
            displayedScore=score;
            string text=score.ToString();
            foreach (var label in labels) if (label != null) label.text = text;
        }
        public void Dispose() { session.ScoreChanged -= Render; }
    }
}
