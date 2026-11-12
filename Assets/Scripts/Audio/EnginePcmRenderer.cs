using System;

namespace CyberBiker.Audio {
    // Each bank is a complete recording at a steady engine speed. Only neighbouring
    // banks are blended; no random fragments or artificial distortion are added.
    public sealed class EnginePcmRenderer {
        internal sealed class Bank { public float[] Samples; public double Rate; public float Rpm, Load; }
        private readonly Bank[] banks;
        private readonly double[] positions;
        private readonly float[] weights;
        private volatile float requestedRpm = 1650;
        private float smoothedRpm = 1650;
        public int RecordingCount => banks.Length;
        public void SetDrive(float rpm, float load) { requestedRpm = rpm; }

        internal EnginePcmRenderer(Bank[] banks, int sampleRate) {
            this.banks = banks;
            positions = new double[banks.Length];
            weights = new float[banks.Length];
        }

        public void Render(float[] data, int channels) {
            if (banks.Length == 0) { Array.Clear(data, 0, data.Length); return; }
            for (int frame = 0; frame < data.Length; frame += channels) {
                smoothedRpm += (requestedRpm - smoothedRpm) * 0.001f;
                int lower = -1, upper = -1;
                for (int i = 0; i < banks.Length; i++) {
                    if (banks[i].Rpm <= smoothedRpm && (lower < 0 || banks[i].Rpm > banks[lower].Rpm)) lower = i;
                    if (banks[i].Rpm >= smoothedRpm && (upper < 0 || banks[i].Rpm < banks[upper].Rpm)) upper = i;
                }
                if (lower < 0) lower = upper;
                if (upper < 0) upper = lower;
                float blend = lower == upper ? 0 : Math.Clamp((smoothedRpm - banks[lower].Rpm) / (banks[upper].Rpm - banks[lower].Rpm), 0, 1);
                double output = 0;
                for (int i = 0; i < banks.Length; i++) {
                    float target = lower == upper ? (i == lower ? 1 : 0) : i == lower ? 1 - blend : i == upper ? blend : 0;
                    weights[i] += (target - weights[i]) * 0.001f;
                    var bank = banks[i];
                    int index = (int)positions[i];
                    int next = index + 1 == bank.Samples.Length ? 0 : index + 1;
                    double frac = positions[i] - index;
                    output += (bank.Samples[index] + (bank.Samples[next] - bank.Samples[index]) * frac) * weights[i];
                    double pitch = Math.Clamp(smoothedRpm / bank.Rpm, 0.7f, 1.4f);
                    positions[i] += bank.Rate * pitch;
                    while (positions[i] >= bank.Samples.Length) positions[i] -= bank.Samples.Length;
                }
                for (int ch = 0; ch < channels; ch++) data[frame + ch] = (float)output;
            }
        }
    }
}
