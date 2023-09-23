using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TouchInputManager : MonoBehaviour
{
    [Range(0, 19)]
    [SerializeField] private float touchSensitivity = 10f;

    private PlayerController pc;

    public Vector2 drag { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        pc = GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update() {
        if (!UnityEditor.EditorApplication.isRemoteConnected) {
            drag = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
        else { 
        

            foreach (Touch touch in Input.touches) {
                //rag += touch.deltaPosition / (2000 - 100 * touchSensitivity);
                //drag = Vector2.ClampMagnitude(drag, 1);
                drag += touch.deltaPosition / (2000 - 100 * touchSensitivity);
                drag = touch.phase == TouchPhase.Ended && Input.touchCount == 1 ? Vector2.zero : Vector2.ClampMagnitude(drag, 1);
            }
        }

        if (pc.xThrow == 0)
            return;
    }
}
