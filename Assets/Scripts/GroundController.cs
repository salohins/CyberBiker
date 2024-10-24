using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundController : MonoBehaviour
{    
    private Transform rayCheck;    
    private Transform player;

    private void Start() {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        rayCheck = player.GetComponent<PlayerController>().rayCheck[0];
    }

    void FixedUpdate()
    {
        LayerMask mask = LayerMask.GetMask("Water");

        Vector3 targetPos;

        targetPos.x = player.transform.position.x;
        targetPos.z = player.transform.position.z;

        RaycastHit hit;

        if (Physics.Raycast(rayCheck.transform.position, -Vector3.up, out hit, Mathf.Infinity, mask)) {
            targetPos.y = hit.point.y - GetComponent<BoxCollider>().bounds.size.y / 2;            
        } else {
            targetPos.y = player.transform.position.y - 1000f;
        }        

        transform.position = targetPos;
    }
}
