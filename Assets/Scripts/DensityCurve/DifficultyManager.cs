using Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DifficultyManager : MonoBehaviour {
    public float gameDuration = 3.0f; // Game duration in minutes (e.g., 3.0 for 3 minutes, 3.30 for 3 minutes 30 seconds)
    public Difficulty[] difficultySections; // Array of difficulty sections

    [HideInInspector]
    public Difficulty currentDifficulty;

    private float sectionDuration; // Duration of each difficulty section in seconds
    private float elapsedTime = 0f; // Elapsed time in seconds
    private int currentDifficultyIndex = 0; // Index of the current difficulty

    private GameplayManager gameplayManager;

    // Start is called before the first frame update
    void Start() {
        gameplayManager = GetComponent<GameplayManager>();
        float gameDurationInSeconds = ConvertMinutesToSeconds(gameDuration); // Convert game duration to seconds

        if (difficultySections != null && difficultySections.Length > 0) {
            currentDifficulty = difficultySections[0];
            sectionDuration = gameDurationInSeconds / difficultySections.Length; // Calculate section duration in seconds
            //Debug.Log($"Game started. Each section duration: {sectionDuration:F2} seconds.");
           // Debug.Log($"Initial difficulty index: {currentDifficultyIndex}");
        }
        else {
            //Debug.LogError("Difficulty sections array is empty or null.");
        }
    }

    // Update is called once per frame
    void Update() {
        if (difficultySections == null || difficultySections.Length == 0) return;

        // Pause the timer if the game state is not Playing
        if (gameplayManager != null && gameplayManager.gameState != GameState.Playing) {
            Debug.Log("Game paused or not in Playing state. Timer paused.");
            return;
        }

        elapsedTime += Time.deltaTime; // Track elapsed time in seconds

        // Log the elapsed time and current difficulty index
        //Debug.Log($"Elapsed Time: {elapsedTime:F2} seconds, Current Difficulty Index: {currentDifficultyIndex}");

        // Check if it's time to move to the next difficulty section
        if (elapsedTime >= sectionDuration) {
            ProgressToNextDifficulty();
        }
    }

    void ProgressToNextDifficulty() {
        if (currentDifficultyIndex < difficultySections.Length - 1) {
            currentDifficultyIndex++;
            currentDifficulty = difficultySections[currentDifficultyIndex];
            elapsedTime = 0f; // Reset elapsed time for the next section

            //Debug.Log($"Difficulty changed! New Difficulty Index: {currentDifficultyIndex}");
        }
        else {
            //Debug.Log("Max difficulty level reached. No more changes.");
        }
    }

    // Convert minutes (and fractions of minutes) to seconds
    float ConvertMinutesToSeconds(float minutes) {
        int wholeMinutes = Mathf.FloorToInt(minutes); // Get the whole number of minutes
        float fractionalMinutes = minutes - wholeMinutes; // Get the fractional part (e.g., 0.30 for 3.30)
        float secondsFromFraction = fractionalMinutes * 60; // Convert the fractional part to seconds

        return (wholeMinutes * 60) + secondsFromFraction; // Total time in seconds
    }
}
