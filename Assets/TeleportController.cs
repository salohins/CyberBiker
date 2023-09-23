using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;

public class TeleportController : MonoBehaviour
{
    [SerializeField] private Material[] teleportMaterial;
    [Range(0, 1)]
    public float fadeValue;
    [SerializeField] private Transform ragDollTarget;

    [SerializeField] private float timerValue;    

    private float timerTemp;

    [HideInInspector] public bool fadeStarted;
    private GameObject player;

    private GameplayManager gm;
    
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");   
        timerTemp = timerValue;
        gm = FindFirstObjectByType<GameplayManager>();
    }
    
    void Update()
    {
        foreach (Material m in teleportMaterial) {
            m.SetFloat("_FadeRate", fadeValue);
            m.SetFloat("_ObjectFadeHeight", ragDollTarget.position.y - 99999f);
            m.SetFloat("_ObjectHeight", ragDollTarget.position.y - 99999f);
        }

        if (fadeStarted && timerValue > 0) {
            timerValue -= Time.deltaTime;
        }

        if (fadeStarted && timerValue <= 0) {
            StartCoroutine(Fade(true));
        }        
    }

    public void startFade(bool fadeOut) {
        StopAllCoroutines();
        if (fadeOut) {
            
            fadeValue = 0;
            fadeStarted = true;
        }
        else {            
            fadeValue = 1;
            StartCoroutine(Fade(fadeOut));
        }
        
    }

    IEnumerator Fade(bool fadeOut) {
        float tempFadeValue = fadeValue;
        fadeStarted = false;
        
        if (fadeOut) {
            while (fadeValue < 1.4f) {
                fadeValue += Time.deltaTime;                
                yield return null;
            }
            gm.gameState = GameState.Playing;
            player.GetComponent<PlayerController>().ResetRagDoll();
            timerValue = timerTemp;

        } else {            
            while (fadeValue > .01) {
                fadeValue -= Time.deltaTime * 0.5f;

                if (fadeValue < .5)
                    SendMessage("turnHelmetLight", true);
                yield return new WaitForEndOfFrame();
            }            
        }        
    }
}
