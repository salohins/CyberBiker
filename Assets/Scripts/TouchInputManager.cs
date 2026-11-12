using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;
using UnityEngine.UI;
using UnityEngine.Animations.Rigging;

public class TouchInputManager : MonoBehaviour, CyberBiker.Player.IPlayerInput
{
    [Range(0, 19)]
    [SerializeField] private float touchSensitivity = 10f;
    public Vector2 Steering => drag;
    public Vector2 AimDelta => delta;
    public bool IsAiming => aim;
    public bool aim { get; private set; }
    public Vector2 delta { get; private set; }



    [SerializeField] public RectTransform aimButton;

    private GameplayManager gm;

    Touch aimTouch;

    public bool leftPressed;
    public bool rightPressed;

    public Vector2 drag { get; private set; }

    void Start()
    {


        gm = FindAnyObjectByType<GameplayManager>();
    }

    void Update() {
        if (gm != null && gm.gameState != GameState.Playing) { aim = false; delta = Vector2.zero; drag = Vector2.zero; return; }
        delta = Vector2.zero;

        if (leftPressed && rightPressed) {

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

#if UNITY_EDITOR || UNITY_STANDALONE

        if (Input.touchCount == 0) {

            float horizontal = Input.GetAxisRaw("Horizontal");
            if (horizontal != 0) drag = new Vector2(horizontal, 0);
            delta = new Vector2(Input.GetAxis("Mouse X") * 5f, Input.GetAxis("Mouse Y") * 5f);

            if (Input.GetMouseButtonDown(1)) {
                SetAim(true);
            }

            if (Input.GetMouseButtonUp(1)) {
                SetAim(false);
            }

            if (!aim) delta = Vector2.zero;

        }
#endif
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

            }
            else {
                drag += touch.deltaPosition / (2000 - 100 * touchSensitivity);
                drag = touch.phase == TouchPhase.Ended && Input.touchCount == 1 ? Vector2.zero : Vector2.ClampMagnitude(drag, 1);

                if (touch.phase == TouchPhase.Ended) {
                    drag = Vector2.zero;
                }
            }
        }

    }

    // Existing scene buttons use this callback as a toggle, including a false argument.
    public void SwitchAim(bool unused) {
        SetAim(!aim);
        if (gm != null && gm.gameState == GameState.Playing)
            CyberBiker.Audio.SoundEvents.Emit(CyberBiker.Audio.SoundCue.UiClick, transform.position);
    }

    public void SetAim(bool on) {
        aim = on && gm != null && gm.gameState == GameState.Playing;

        if (gm != null && gm.gameState == GameState.Playing)
            if (Input.touchCount > 0 && aim && aimButton != null) {
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
