using Gameplay;
using UnityEngine;

public class DifficultyManager : MonoBehaviour {
    [Header("Play modes")]
    [Tooltip("Mode when starting Level directly. A choice made through Play or TUNE takes priority for this play session.")]
    public RunPlayMode startMode=RunPlayMode.Progression;
    [Tooltip("The old five-enemy Difficulty 4 setup. Constant Combat holds this difficulty and replaces defeated riders continuously.")]
    public Difficulty constantCombatDifficulty;
    public RunPlayMode Mode {get;private set;}
    public bool IsConstantCombat=>Mode==RunPlayMode.ConstantCombat;
    [Header("Run progression")]
    [Tooltip("Fallback total duration in decimal minutes for sections whose Duration Seconds is zero. 3.5 means 3 minutes 30 seconds.")]
    [Min(.1f)] public float gameDuration = 3;
    [Tooltip("Played in order. Each difficulty prefab controls traffic, encounters and its own duration.")]
    public Difficulty[] difficultySections;
    [Tooltip("Zero-based section to repeat after the last section. -1 keeps the last difficulty indefinitely.")]
    [Min(-1)] public int loopFromSection = -1;

    [HideInInspector] public Difficulty currentDifficulty;
    public int CurrentSectionIndex => currentDifficultyIndex;
    public int SectionRevision { get; private set; }
    public float SectionElapsed => elapsedTime;
    public float SectionRemaining => Mathf.Max(0, SectionDuration - elapsedTime);
    public float RunElapsed { get; private set; }
    public float SectionDuration {
        get {
            if(IsConstantCombat)return 0;
            if (difficultySections == null || difficultySections.Length == 0) return 0;
            var section = difficultySections[currentDifficultyIndex];
            return section != null && section.durationSeconds > 0
                ? Mathf.Max(.1f, section.durationSeconds)
                : Mathf.Max(.1f, gameDuration * 60 / difficultySections.Length);
        }
    }
    private float elapsedTime;
    private int currentDifficultyIndex;
    private GameplayManager gameplayManager;

    private void Awake() {
        gameplayManager = GetComponent<GameplayManager>();
        Mode=RunModeSelection.HasSelection ? RunModeSelection.Selected : RunModeSelection.Normalize(startMode);
        if(IsConstantCombat && constantCombatDifficulty==null){
            var modes=Resources.Load<RunModeSettings>("RunModes");
            if(modes!=null)constantCombatDifficulty=modes.constantCombatDifficulty;
        }
        if(IsConstantCombat && constantCombatDifficulty!=null){currentDifficulty=constantCombatDifficulty;return;}
        if(IsConstantCombat)Debug.LogError("Constant Combat requires the RunModes combat preset.",this);
        Mode=RunPlayMode.Progression;
        if (difficultySections != null && difficultySections.Length > 0)
            currentDifficulty = difficultySections[0];
    }
    private void Update() {
        if (gameplayManager != null && gameplayManager.gameState != GameState.Playing) return;
        AdvanceTime(Time.deltaTime);
    }
    private void AdvanceTime(float deltaTime) {
        if(IsConstantCombat){if(deltaTime>0)RunElapsed+=deltaTime;return;}
        if (difficultySections == null || difficultySections.Length == 0 || deltaTime <= 0) return;
        RunElapsed += deltaTime;
        elapsedTime += deltaTime;
        while (elapsedTime >= SectionDuration) {
            int next = currentDifficultyIndex + 1;
            if (next >= difficultySections.Length) {
                if (loopFromSection < 0 || loopFromSection >= difficultySections.Length) {
                    elapsedTime = SectionDuration;
                    return;
                }
                next = loopFromSection;
            }
            elapsedTime -= SectionDuration;
            currentDifficultyIndex = next;
            currentDifficulty = difficultySections[next];
            SectionRevision++;
        }
    }
}
