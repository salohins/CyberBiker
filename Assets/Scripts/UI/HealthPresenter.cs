using System;
using TMPro;
using UnityEngine;
using CyberBiker.Combat;

namespace CyberBiker.UI {
    public sealed class HealthPresenter : IDisposable {
        private readonly Health health;
        private readonly TMP_Text label;
        private readonly Material material;
        private readonly float originalCutoff;
        public HealthPresenter(Health health, TMP_Text label, Material material) {
            this.health = health;
            this.label = label;
            // Restore the scene-bound material when the listener is released.
            this.material = material;
            originalCutoff = material != null ? material.GetFloat("_Cutoff") : 0;
            health.Changed += Render;
            Render(health.Current, health.Maximum);
        }
        private void Render(float current, float maximum) {
            if (label != null) label.text = Mathf.CeilToInt(current / maximum * 100) + "%";
            if (material != null) material.SetFloat("_Cutoff", (1 - current / maximum) * 0.39f);
        }
        public void Dispose() {
            health.Changed -= Render;
            if (material != null) material.SetFloat("_Cutoff", originalCutoff);
        }
    }
}
