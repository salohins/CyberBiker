# Carbon score-sound experiment

Requested reference: dark, digital, atmospheric racing feedback, specifically
Need for Speed: Carbon. The earlier Hove/Eric scratch layers and the bright
arcade candidate are not used by this active local experiment.

Four original PC interface/drift cues were downloaded through the normal
browser controls from The Sounds Resource, uploaded by WF4123:

- https://sounds.spriters-resource.com/pc_computer/needforspeedcarbon/asset/441631/
- https://sounds.spriters-resource.com/pc_computer/needforspeedcarbon/asset/441632/

| CyberBiker role | Archive entry | Gain |
| --- | --- | ---: |
| Points | GLOBAL/fe_common_drift_mb [4].wav | 0.7 |
| Multiplier increase | GLOBAL/fe_common_drift_mb [3].wav | 0.8 |
| Combo bank | GLOBAL/fe_common_drift_mb [1].wav | 0.8 |
| Combo loss | FE/fe_mb [3].wav | 0.9 |

These are proposed roles for the experiment, not claimed original Carbon event
names. All four retain their full sample counts, original pitch, and
44.1kHz / 16-bit mono format. Processing is gain only, with no added layers.
An offline PCM comparison verifies each output sample matches its original
sample times the stated gain within rounding.

Restart Play. The runtime CyberBikerAudio object shows the Carbon clips under
Style Score Sounds. Individual cue gains and the overall Style Score volume
still work. Prototype point sounds stay at pitch 1 even at the multiplier cap.
Set Use Carbon Score Prototype off before the next Play to use the assigned
licensed fallback. Loading is atomic: a partial/missing local set never
replaces only some of the four cues.

The short point recording also handles driving rewards, at a higher individual
gain. Kills, sword finishers and perfect dodges use the longer multiplier
recording as their larger reward. Multiplier gains still take precedence when
the same move raises a tier. Major rewards bypass the routine tick throttle;
their arrival stops a routine score tick started earlier in the same frame.
Driving Bonus Volume and Combat Bonus Volume are individually exposed on the
audio rig, and larger rewards briefly duck the engine/wind mix. All Carbon
playback remains at pitch 1, without added sound layers.

The reference WAVs are in Assets/Resources/CarbonScorePrototype, whose folder
and metadata are ignored by Git. They are EA / EA Black Box assets, not CC0;
the downloads do not grant a release license. Remove this local reference
folder before a distributable build. No commit, push or Android build was made.

The source archives, source WAVs, gain-only waveform audit and listening preview
remain local under Logs/nfs_carbon_score. The preview sequence is points,
multiplier, banking, then combo loss, at twice normal dry gain for standalone
listening. It is not an engine/wind mix or a Unity output recording.
