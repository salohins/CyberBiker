using Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private bool starting;
    public void StartGame() {
        if(!starting)CyberBiker.UI.PlayModeMenu.Show(this);
    }
    public void StartProgression()=>StartMode(RunPlayMode.Progression);
    public void StartConstantCombat()=>StartMode(RunPlayMode.ConstantCombat);
    private void StartMode(RunPlayMode mode){
        if(starting)return;starting=true;
        RunModeSelection.Select(mode);
        Time.timeScale = 1;
        SceneManager.LoadScene(1);
    }
}
