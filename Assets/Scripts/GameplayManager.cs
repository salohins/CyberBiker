using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Gameplay {
    public enum GameState {
        GameOver,
        Playing,
        Pause,
        Hit,
        Intro
    }

    public class GameplayManager : MonoBehaviour {                
        [Header("Difficulty Settings")]
        [SerializeField] private AnimationCurve difficultyCurve;
        [Tooltip("Speed multiplier for difficultyCurve period")]
        [SerializeField] private float difficultySpeed = 1;

        private float difficultyPeriod;        

        public float difficulty { get; private set; }
        public float score { get; private set; }

        public GameState gameState;

        [Header("Obj Refs")]
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
        
        private GameObject player;
        private float totalCalculatedDifficulty;
        private bool showMenuConfirmation = false;
        private float pauseTimerValue = 4f;
        private bool isTimerRunning = false;
        private int periodCount = 0;

        private void Start() {
            player = GameObject.FindGameObjectWithTag("Player");

            Application.targetFrameRate = 60;
            isTimerRunning = false;

            if (gameOverScreen != null)
                gameOverScreen.SetActive(false);

            if (pauseScreen != null)
                pauseScreen.SetActive(false);

            if (menuConfirmation != null)
                menuConfirmation.SetActive(false);

            pauseMenuTimerText.SetActive(false);

            camera.SetTarget(player.GetComponent<PlayerController>().cameraTarget.transform);
        }

        private void Update() {           
            ProcessPauseTimer();
            ProcessScore();
            ProcessDifficulty();
            ProcessDebugValues();         
            
            
            aimButton.SetActive(!(gameState == GameState.Hit));
            
        }

        private void ProcessDebugValues() {
            difficultyText.text = "Difficulty: " + difficulty.ToString("F2");
            speedText.text = "Speed: " + player.GetComponent<PlayerController>().zVelocity.ToString("F2");
        }
        

        private void ProcessDifficulty() {
            difficultyPeriod += Time.deltaTime / 50f * difficultySpeed;    // DifficultySpeed
            
            if (difficultyPeriod > difficultyCurve.Evaluate(1)) {
                difficultyPeriod = 0;
                periodCount++;
                totalCalculatedDifficulty = periodCount * (difficultyCurve.Evaluate(1) - difficultyCurve.Evaluate(0));      
            }

            difficulty = periodCount + difficultyCurve.Evaluate(difficultyPeriod);
        }

        private void ProcessScore() {
            if (gameState == GameState.Playing)
                score += Time.deltaTime * player.GetComponent<PlayerController>().zVelocity / 100;

            foreach (TMP_Text text in scoreText) {
                text.text = ((int)score).ToString();
            }    

        }

        private void ProcessPauseTimer() {
            if(isTimerRunning) {
                if (pauseTimerValue > 1.1f) {
                    pauseTimerValue -= Time.unscaledDeltaTime;
                    pauseMenuTimerText.gameObject.SetActive(true);
                    if (pauseTimerValue != 0)
                        pauseMenuTimerText.GetComponent<TMP_Text>().text = ((int)pauseTimerValue).ToString();
                }
                else {
                    isTimerRunning = false;
                    pauseMenuTimerText.SetActive(false);
                    Time.timeScale = 1;

                }
            }
        }
       
        public void LoadMainMenu() => SceneManager.LoadScene(0);
        public void OpenPauseMenu() => SwitchGameState(GameState.Pause);
        public void ResumeGame() => SwitchGameState(GameState.Playing);

        public void ToggleMenuConfirmation() {
            showMenuConfirmation = !showMenuConfirmation;
            menuConfirmation.SetActive(showMenuConfirmation);
        }                    

        public void RestartLevel() {
            SceneManager.LoadScene(1);
            gameOverScreen.SetActive(false);
            
        }       

        public void SwitchGameState(GameState state) {
            gameState = state;

            if (state == GameState.GameOver) {
                gameOverScreen.SetActive(true);                
            } else if (state == GameState.Pause) {
                Time.timeScale = 0;
                pauseTimerValue = 4f;
                pauseScreen.SetActive(true);
            } else if (state == GameState.Playing) {                                
                pauseScreen.SetActive(false);
                
                isTimerRunning = true;
            }
        }
    }
}
