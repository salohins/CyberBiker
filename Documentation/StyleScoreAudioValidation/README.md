# Dedicated style-score sound validation

Replaced score playback of generic menu click/maximize/minimize/confirmation assets with six dedicated downloaded sci-fi effects. The final revision adds short Hove Audio low/mid impact layers in response to the user's request for heavier impact after the first dry preview. Levels retain the same peaks; the first point cue has 11.12 dB more 0–500 Hz RMS energy than the lighter revision. HeavierComparison.json records that measurement.

Sources: Hove Audio's Free SCI-FI UI Sound Effects Pack (48kHz / 24-bit stereo), plus a low-level Eric Berzins Ultra Sci-Fi UI 03 attack on points. Full licenses/provenance and source hashes are in Assets/Audio/CyberBiker/Licenses/StyleScoreSources.json; credits are in Assets/Audio/CyberBiker/CREDITS.md. These are edits/mixes of downloaded effects, not new oscillator or AI-generated sounds.

Native Unity 6000.6.0f1 validation passed in the isolated project copy (Result.txt):

- All six selected assets import as 48kHz stereo PCM, preloaded/decompressed for low latency, and contain valid sample data with peak headroom.
- Points alternate three variants without immediate repeats, and pitch stays capped at 1.12. The composed multiplier/bank/break cues play at original pitch.
- Real pooled playback uses dedicated score assets; score mute and pause suppress playback.
- Existing actual-shot, kill, finisher, dodge, close-pass, banking, loss, HUD/world-number placement and reduced-motion integration checks also passed.

Tune **CyberBikerAudio → Style Score Sounds** for the four event gains and clip assignments. The existing GameplayManager Style Scoring / TUNE volume remains the overall score control. Ordinary ticks retain their rate limit; multiplier and bank sounds retain the existing brief engine/wind duck.

ScoreSoundsPreview.mp3 is the final dry cue sequence at twice production dry gain for comfortable standalone listening. ScoreSoundsInMix.mp3 approximates the current straight/full-speed engine and wind gains, cruise pitch and 5dB reward duck; it is not a Unity output capture and omits spatial attenuation, Unity reverb, gunfire and traffic. The mixed preview peaks at -7.62dBFS, without limiting. PreviewEvents.json records timings and gains. This validates levels and integration; subjective sound quality is for listening to the previews and the game.

Existing missing-script/Visual Scripting warnings remain outside this change. No Android build was requested or made.
