using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;
using CyberBiker.Runs;
using CyberBiker.UI;

namespace Gameplay {
    public enum GameState { GameOver, Playing, Pause, Hit, Intro }

    public class GameplayManager : MonoBehaviour {
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
        private GameState stateBeforePause;
        public bool IsResuming => resumeRemaining > 0;

        private void Awake() {
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
        }
        private void Update() {
            if (Input.GetKeyDown(KeyCode.Escape)) {
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
            if (difficultyText != null) difficultyText.text = "Difficulty: " + difficulty.ToString("F2");
            if (speedText != null && player != null) speedText.text = "Speed: " + player.zVelocity.ToString("F2");
        }
        public void SwitchGameState(GameState state) {
            if (state == GameState.Pause && gameState != GameState.Pause) stateBeforePause = gameState;
            Session.ChangeState(state);
        }
        private void OnStateChanged(GameState previous, GameState current) {
            resumeRemaining = 0;
            Time.timeScale = current == GameState.Pause ? 0 : 1;
            Show(pauseMenuTimerText, false);
            UpdateScreens();
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
        }
    }
}
