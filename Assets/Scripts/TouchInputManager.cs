using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;
using UnityEngine.UI;

public class TouchInputManager : MonoBehaviour
{
    [Range(0, 19)]
    [SerializeField] private float touchSensitivity = 10f;
    public bool aim { get; private set; }
    public Vector2 delta { get; private set; }

    private PlayerController pc;

    [SerializeField] public RectTransform aimButton;

    private GameplayManager gm;

    Touch aimTouch;

    public Vector2 drag { get; private set; }
    
    // Start is called before the first frame update
    void Start()
    {
        pc = GetComponent<PlayerController>();
        //aim = true;
        //Cursor.visible = false;
        //Cursor.lockState = CursorLockMode.Locked;

        gm = FindFirstObjectByType<GameplayManager>();
    }

    // Update is called once per frame
    void Update() {
        
        if (!UnityEditor.EditorApplication.isRemoteConnected) {
            drag = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            delta = new Vector2(Input.GetAxis("Mouse X") * 5f, Input.GetAxis("Mouse Y") * 5f);
            if (Input.GetMouseButtonDown(1)) {
                SwitchAim(true);
            }

            if (Input.GetMouseButtonUp(1)) {
                SwitchAim(false);
            }

            if (aim) {
                //drag = Vector2.zero;
            } else {
                delta = Vector2.zero;
            }
        }
        else {     
            foreach (Touch touch in Input.touches) {

                
                    if (aim && touch.fingerId == aimTouch.fingerId) {                    

                        if (touch.phase == TouchPhase.Moved) {
                            delta = touch.deltaPosition;
                        }
                        if (touch.phase == TouchPhase.Ended) {
                            delta = Vector2.zero;
                        }

                        if (touch.phase == TouchPhase.Stationary) {
                            delta = Vector2.zero;
                        }
                        //aimTouch = touch;
                    } else {
                        drag += touch.deltaPosition / (2000 - 100 * touchSensitivity);
                        drag = touch.phase == TouchPhase.Ended && Input.touchCount == 1 ? Vector2.zero : Vector2.ClampMagnitude(drag, 1);

                        if (touch.phase == TouchPhase.Ended) {
                            drag = Vector2.zero;
                        }
                    }                            
            }
        }

        if (pc.xThrow == 0)
            return;
        if (gm.gameState == GameState.Hit) {
           
            SwitchAim(false);
        }
    }

    public void SwitchAim(bool on) {
        aim = on;

        
        if (gm.gameState == GameState.Playing)
            if (Input.touchCount > 0 && aim) {
                aimTouch = Input.touches[0];

                foreach (Touch touch in Input.touches) {
                    if (Vector2.Distance(aimButton.transform.position, touch.position) < Vector2.Distance(aimButton.transform.position, aimTouch.position)) {
                        aimTouch = touch;
                    }
                }
            }

        
    }
}
