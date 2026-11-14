using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;
using CyberBiker.Runs;
using CyberBiker.UI;

namespace Gameplay {
    public enum GameState { GameOver, Playing, Pause, Hit, Intro }
    public enum PistolInteraction { AimAndAutoFire, TapToFire }

    public class GameplayManager : MonoBehaviour {
        [Header("Enemy Following")]
        [Tooltip("Target distance from the player in world units for every enemy, including newly spawned enemies and during Launch. Changes apply live. Finisher-ready enemies release this gap so they can be caught.")]
        [SerializeField, Min(1)] private float enemyDistanceFromPlayer = 20f;
        public float EnemyDistanceFromPlayer {
            get => Mathf.Max(1, enemyDistanceFromPlayer);
            set => enemyDistanceFromPlayer = Mathf.Max(1, value);
        }

        [Header("Player Pistol")]
        [SerializeField] private PistolInteraction pistolInteraction = PistolInteraction.TapToFire;
        [Tooltip("Real seconds the pistol stays ready after a shot or tap. Pausing cancels the tap session.")]
        [SerializeField, Range(.3f,3)] private float pistolHolsterDelay = 1.2f;
        [Tooltip("In Tap To Fire mode, pistol hits can kill and explode enemies. Disable to keep the sword-finisher health floor.")]
        [SerializeField] private bool tapPistolCanKill = true;
        public bool TapPistolCanKill {get => tapPistolCanKill;set => tapPistolCanKill=value;}
        public PistolInteraction PistolInteractionMode {get => pistolInteraction;set => pistolInteraction=value;}
        public float PistolHolsterDelay {get => Mathf.Max(.3f,pistolHolsterDelay);set => pistolHolsterDelay=Mathf.Max(.3f,value);}
        [Tooltip("Damage dealt by each player pistol hit. Changes apply to the next shot during Play mode.")]
        [SerializeField, Min(0)] private float pistolDamagePerShot = 15f;
        [Tooltip("Gameplay seconds between player pistol shots. Lower values fire faster; 0.45 is about 2.2 shots per second. Changes apply live.")]
        [SerializeField, Min(.01f)] private float secondsBetweenPistolShots = .45f;
        public float PlayerPistolDamage {
            get => Mathf.Max(0, pistolDamagePerShot);
            set => pistolDamagePerShot = Mathf.Max(0, value);
        }
        public float PlayerPistolShotInterval {
            get => Mathf.Max(.01f, secondsBetweenPistolShots);
            set => secondsBetweenPistolShots = Mathf.Max(.01f, value);
        }

        [Header("Run Difficulty")]
        [SerializeField] private AnimationCurve difficultyCurve;
        [SerializeField] private float difficultySpeed = 1;
        [FormerlySerializedAs("gameState")]
        [SerializeField] private GameState initialState;
        public RunSession Session { get; private set; }
        public GameState gameState => Session != null ? Session.State : initialState;
        public float score => Session != null ? Session.Score : 0;
        public float difficulty { get; private set; }
        [SerializeField] private new CameraController camera;
        [SerializeField] private GameObject gameOverScreen;
        [SerializeField] private GameObject pauseScreen;
        [SerializeField] private GameObject menuConfirmation;
        [SerializeField] private GameObject pauseMenuTimerText;
        [SerializeField] private GameObject aimButton;
        [SerializeField] private GameObject canvas;
        [SerializeField] private TMP_Text[] scoreText;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text speedText;
        private PlayerController player;
        private ScorePresenter scorePresenter;
        private float resumeRemaining;
        private int displayedDifficulty=int.MinValue,displayedSpeed=int.MinValue;
        private GameState stateBeforePause;
        public bool IsResuming => resumeRemaining > 0;
        private float combatSlowRemaining,combatTimeScale=1,combatTargetScale=1,normalFixedStep;
        private float combatRecoveryDuration,combatRecoveryElapsed,combatRecoveryStart=1;
        public int CombatSlowMotionCount { get; private set; }
        public float CombatSlowMotionRemaining => combatSlowRemaining+Mathf.Max(0,combatRecoveryDuration-combatRecoveryElapsed);
        public bool IsCombatTimeRecovering => combatSlowRemaining<=0 && combatRecoveryElapsed<combatRecoveryDuration;

        private void Awake() {
            normalFixedStep=Time.fixedDeltaTime;
            Session = new RunSession(initialState);
            Session.StateChanged += OnStateChanged;
            scorePresenter = new ScorePresenter(Session, scoreText);
            CyberBiker.Audio.GameAudio.EnsurePresent();
        }
        private void Start() {
            player = FindAnyObjectByType<PlayerController>();
            Application.targetFrameRate = 60;
            Time.timeScale = gameState == GameState.Pause ? 0 : 1;
            Show(menuConfirmation, false);
            Show(pauseMenuTimerText, false);
            UpdateScreens();
            if (camera != null && player != null) camera.SetTarget(player.cameraTarget.transform);
            TestingSettingsMenu.Install(this);
        }
        private void Update() {
            if(gameState==GameState.Playing && CombatSlowMotionRemaining>0) {
                float dt=CyberBiker.Player.FinisherClock.UnscaledDeltaTime;
                if(combatSlowRemaining>0) {
                    float held=Mathf.Min(dt,combatSlowRemaining);dt-=held;
                    // Stronger contact slows immediately; a weaker explosion phase
                    // blends up instead of jumping the clock to another speed.
                    combatTimeScale=Mathf.Lerp(combatTimeScale,combatTargetScale,1-Mathf.Exp(-held*12));
                    combatSlowRemaining=Mathf.Max(0,combatSlowRemaining-held);
                    if(combatSlowRemaining==0)combatRecoveryStart=combatTimeScale;
                }
                if(combatSlowRemaining==0) {
                    combatRecoveryElapsed=Mathf.Min(combatRecoveryDuration,combatRecoveryElapsed+dt);
                    float t=combatRecoveryDuration>0 ? combatRecoveryElapsed/combatRecoveryDuration : 1;
                    combatTimeScale=Mathf.Lerp(combatRecoveryStart,1,Mathf.SmoothStep(0,1,t));
                }
                ApplyTimeState();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) {
                if (TestingSettingsMenu.CloseOnBack(this)) return;
                if (gameState == GameState.Playing || gameState == GameState.Hit) OpenPauseMenu();
                else if (gameState == GameState.Pause && !IsResuming) ResumeGame();
            }
            if (IsResuming) {
                resumeRemaining -= Time.unscaledDeltaTime;
                var label = pauseMenuTimerText != null ? pauseMenuTimerText.GetComponent<TMP_Text>() : null;
                if (label != null) label.text = Mathf.CeilToInt(Mathf.Max(0, resumeRemaining)).ToString();
                if (!IsResuming) { Show(pauseMenuTimerText, false); Session.ChangeState(stateBeforePause); }
            }
            Session.Tick(Time.deltaTime, player != null ? player.zVelocity : 0);
            float period = Session.Elapsed * Mathf.Max(0, difficultySpeed) / 50f;
            difficulty = Mathf.Floor(period) + (difficultyCurve != null ? difficultyCurve.Evaluate(Mathf.Repeat(period, 1)) : 0);
            if (difficultyText != null) {
                int value=Mathf.RoundToInt(difficulty*100);
                if(value!=displayedDifficulty){difficultyText.SetText("Difficulty: {0:2}",value*.01f);displayedDifficulty=value;}
            }
            if (speedText != null && player != null) {
                int value=Mathf.RoundToInt(player.zVelocity*100);
                if(value!=displayedSpeed){speedText.SetText("Speed: {0:2}",value*.01f);displayedSpeed=value;}
            }
        }
        public void SwitchGameState(GameState state) {
            if (state == GameState.Pause && gameState != GameState.Pause) stateBeforePause = gameState;
            Session.ChangeState(state);
        }
        private void OnStateChanged(GameState previous, GameState current) {
            resumeRemaining = 0;
            if(current!=GameState.Playing && current!=GameState.Pause){combatSlowRemaining=0;combatRecoveryDuration=combatRecoveryElapsed=0;combatTimeScale=1;}
            ApplyTimeState();
            Show(pauseMenuTimerText, false);
            UpdateScreens();
        }
        public void BeginCombatSlowMotion(float scale,float realSeconds,float recoverySeconds=.2f) {
            if(gameState!=GameState.Playing || realSeconds<=0)return;
            CombatSlowMotionCount++;
            bool active=CombatSlowMotionRemaining>0;
            combatTargetScale=Mathf.Clamp(scale,.1f,1);
            combatTimeScale=Mathf.Min(active ? combatTimeScale : 1,combatTargetScale);
            combatSlowRemaining=Mathf.Max(combatSlowRemaining,realSeconds);
            combatRecoveryDuration=active ? Mathf.Max(combatRecoveryDuration,recoverySeconds) : Mathf.Max(0,recoverySeconds);
            combatRecoveryElapsed=0;combatRecoveryStart=combatTimeScale;ApplyTimeState();
        }
        public void EndCombatSlowMotion(){
            if(combatSlowRemaining>0){combatSlowRemaining=0;combatRecoveryElapsed=0;combatRecoveryStart=combatTimeScale;}
            ApplyTimeState();
        }
        public void CancelCombatSlowMotion() {
            combatSlowRemaining=0;combatRecoveryDuration=combatRecoveryElapsed=0;
            combatTimeScale=combatTargetScale=combatRecoveryStart=1;ApplyTimeState();
        }
        private void ApplyTimeState() {
            Time.timeScale=gameState==GameState.Pause ? 0 : CombatSlowMotionRemaining>0 && gameState==GameState.Playing ? combatTimeScale : 1;
            Time.fixedDeltaTime=normalFixedStep*(Time.timeScale>0 ? Time.timeScale : 1);
        }
        private void UpdateScreens() {
            Show(gameOverScreen, gameState == GameState.GameOver);
            Show(pauseScreen, gameState == GameState.Pause);
            Show(aimButton, gameState == GameState.Playing);
        }
        private static void Show(GameObject target, bool visible) { if (target != null) target.SetActive(visible); }
        public void OpenPauseMenu() { if (gameState == GameState.Playing || gameState == GameState.Hit) SwitchGameState(GameState.Pause); }
        public void ResumeGame() {
            if (gameState != GameState.Pause || IsResuming) return;
            resumeRemaining = 3;
            Show(pauseScreen, false);
            Show(pauseMenuTimerText, true);
        }
        public void ToggleMenuConfirmation() { CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.UiClick, Vector3.zero); if (menuConfirmation != null) menuConfirmation.SetActive(!menuConfirmation.activeSelf); }
        public void LoadMainMenu() { Time.timeScale = 1; SceneManager.LoadScene(0); }
        public void RestartLevel() { Time.timeScale = 1; SceneManager.LoadScene(1); }
        private void OnDestroy() {
            scorePresenter?.Dispose();
            if (Session != null) Session.StateChanged -= OnStateChanged;
            Time.timeScale = 1;
            Time.fixedDeltaTime=normalFixedStep;
        }
    }
}
