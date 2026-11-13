using System;
using UnityEngine;

namespace CyberBiker.Player {
    // SampleAnimation needs raw transform curves in a player. Generic clips are
    // converted to Mecanim data during builds and sample differently without an Animator.
    [PreferBinarySerialization]
    public sealed class SwordRuntimeClips : ScriptableObject {
        [SerializeField] private AnimationClip[] clips;
        [SerializeField, HideInInspector] private string sourceDigest;
        public string SourceDigest => sourceDigest;
        public AnimationClip Resolve(AnimationClip source) {
            if (source == null || source.legacy) return source;
            if (clips != null) foreach (var clip in clips)
                if (clip != null && clip.legacy && clip.name == source.name) return clip;
            throw new InvalidOperationException("Missing runtime sword curves for " + source.name + ". Rebuild the runtime sword library.");
        }
    }
}
