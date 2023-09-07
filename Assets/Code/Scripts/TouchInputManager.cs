using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TouchInputManager : MonoBehaviour
{
    [Range(0, 19)]
    [SerializeField] private float touchSensitivity = 10f;
    public Vector2 drag { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (Touch touch in Input.touches) {
            drag += touch.deltaPosition / (2000 - 100 * touchSensitivity);
            drag = touch.phase == TouchPhase.Ended && Input.touchCount == 1 ?  Vector2.zero : Vector2.ClampMagnitude(drag, 1);            
        }
    }
}
