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
        Intro
    }
    public class GameplayManager : MonoBehaviour {
        public GameState gameState;
        public float difficulty;
        public float score;

        private float pauseTimerValue = 4f;
        private bool isTimerRunning = false;

        [SerializeField] private GameObject player;

        [SerializeField] private GameObject gameOverScreen;
        [SerializeField] private GameObject pauseScreen;
        [SerializeField] private GameObject menuConfirmation;


        [SerializeField] private TMP_Text[] scoreText;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private GameObject pauseMenuTimerText;

        private bool showMenuConfirmation = false;        

        private void Start() {
            Application.targetFrameRate = 60;
            isTimerRunning = false;

            if (gameOverScreen != null)
                gameOverScreen.SetActive(false);

            if (pauseScreen != null)
                pauseScreen.SetActive(false);

            if (menuConfirmation != null)
                menuConfirmation.SetActive(false);

            pauseMenuTimerText.SetActive(false);
        }

        private void Update() {
            ProcessPauseTimer();
            ProcessScore();
            ProcessDifficulty();
            ProcessDebugValues();
        }

        private void ProcessDebugValues() {
            difficultyText.text = "Difficulty: " + ((int)difficulty).ToString();
            speedText.text = "Speed: " + player.GetComponent<PlayerController>().zVelocity.ToString();
        }

        private void ProcessDifficulty() {
            difficulty += Time.deltaTime / 10f;            
        }

        private void ProcessScore() {
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
