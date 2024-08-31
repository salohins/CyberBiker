using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;
using UnityEngine.UI;
using UnityEngine.Animations.Rigging;
using Unity.VisualScripting;

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

    public bool leftPressed;
    public bool rightPressed;

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

        if (leftPressed && rightPressed) {
            // If both are pressed, prioritize the last pressed button
            drag = new Vector2(lastPressed == -1 ? -1 : 1, drag.y);
        }
        else if (leftPressed) {
            drag = new Vector2(-1, drag.y);
        }
        else if (rightPressed) {
            drag = new Vector2(1, drag.y);
        }
        else {
            drag = new Vector2(0, drag.y);  // No button pressed
        }



        if (!UnityEditor.EditorApplication.isRemoteConnected) {
#if UNITY_EDITOR
            // Code specific to Unity Editor
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
            }
            else {
                delta = Vector2.zero;
            }
#endif
        }

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
            }
            else {
                drag += touch.deltaPosition / (2000 - 100 * touchSensitivity);
                drag = touch.phase == TouchPhase.Ended && Input.touchCount == 1 ? Vector2.zero : Vector2.ClampMagnitude(drag, 1);

                if (touch.phase == TouchPhase.Ended) {
                    drag = Vector2.zero;
                }
            }
        } 

         
        

        if (pc.xThrow == 0)
            return;
        if (gm.gameState == GameState.Hit) {
           
            //SwitchAim(false);
        }
    }

    public void SwitchAim(bool on) {
        aim = !aim;

        
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

    private int lastPressed = 0; // -1 for left, 1 for right, 0 for none

    public void setLeftPressed(bool pressed) {
        Debug.Log(1);
        leftPressed = pressed;
        if (pressed) {
            lastPressed = -1;  // Left was pressed last
        }
    }

    public void setRightPressed(bool pressed) {
        rightPressed = pressed;
        if (pressed) {
            lastPressed = 1;  // Right was pressed last
        }
    }
}
