using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletWorld : MonoBehaviour
{

    GameObject player;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindWithTag("Player");
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 targetPosition = player.transform.position;

        targetPosition.x = 0;
        targetPosition.y = 0;

        transform.position = targetPosition; 
    }
}
