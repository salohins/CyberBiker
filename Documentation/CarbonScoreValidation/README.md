# Carbon score reference validation

Native Unity 6000.6.0f1 checks passed in the isolated project copy. Result.txt
includes the actual Carbon preset import/playback assertion and the existing
score integration checks.

- Four original-rate 44.1kHz mono PCM cues loaded through the local prototype
  override, preserving pitch 1 for every reward type.
- Eight repeated point calls selected CarbonPoint, not the rejected scratch
  layers or random menu clips. Multiplier, bank and loss selected their own cues.
- Mute and pause suppressed score playback.
- Real projectile damage, kills, sword finishers, dodges, traffic close passes,
  score banking/loss and world-number presentation still passed.
- GainOnlyAudit.json verifies unchanged sample counts and each installed
  waveform equals its archived source times its gain, within PCM rounding.
- Git ignore checks cover the proprietary reference WAVs and folder metadata.
- The validation also exposed a shutdown-order issue in WorldStyleNumbers;
  Clear/FaceCamera now tolerate already-destroyed canvases. The final native
  run exited without that MissingReferenceException.

The listening preview remains local at Logs/nfs_carbon_score/CarbonScorePreview.mp3.
No EA audio is stored in this documentation directory. Source/prototype usage
is documented in Documentation/CarbonScorePrototype.md.

Checks establish import, dispatch, lifecycle and levels; they do not certify
subjective sound quality or Android performance. Existing missing-script and
Visual Scripting warnings are outside this change.
