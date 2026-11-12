using System;
using UnityEngine;
namespace CyberBiker.Audio {
    [Serializable]
    public struct EngineRecording {
        public AudioClip clip;
        [Tooltip("Estimated native RPM used for audio calibration; editable, not vehicle telemetry.")]
        public float baseRpm;
        [Range(0, 1)] public float load;
    }
    [RequireComponent(typeof(AudioSource))]
    public sealed class RecordedEngineVoice : MonoBehaviour {
        private volatile EnginePcmRenderer renderer;
        public int RecordingCount => renderer != null ? renderer.RecordingCount : 0;
        public void SetDrive(float rpm, float load) { renderer?.SetDrive(rpm, load); }
        public static EnginePcmRenderer CreateRenderer(EngineRecording[] recordings, int sampleRate) {
            if (recordings == null || recordings.Length == 0) return null;
            var banks = new EnginePcmRenderer.Bank[recordings.Length];
            for (int i = 0; i < recordings.Length; i++) {
                var recording = recordings[i];
                if (recording.clip == null || recording.clip.length < 1) return null;
                var pcm = new float[recording.clip.samples * recording.clip.channels];
                if (!recording.clip.GetData(pcm, 0)) return null;
                var mono = new float[recording.clip.samples];
                for (int frame = 0; frame < mono.Length; frame++) {
                    float value = 0;
                    for (int ch = 0; ch < recording.clip.channels; ch++) value += pcm[frame * recording.clip.channels + ch];
                    mono[frame] = value / recording.clip.channels;
                }
                banks[i] = new EnginePcmRenderer.Bank { Samples = mono, Rate = (double)recording.clip.frequency / sampleRate,
                    Rpm = Mathf.Max(500, recording.baseRpm), Load = recording.load };
            }
            return new EnginePcmRenderer(banks, sampleRate);
        }
        public bool Initialize(EngineRecording[] recordings) {
            renderer = CreateRenderer(recordings, AudioSettings.outputSampleRate);
            if (renderer == null) return false;
            var source = GetComponent<AudioSource>();
            source.clip = recordings[0].clip; source.loop = true; source.pitch = 1; source.Play();
            return true;
        }
        private void OnAudioFilterRead(float[] data, int channels) {
            if (renderer != null) renderer.Render(data, channels);
            else Array.Clear(data, 0, data.Length);
        }
    }
}
