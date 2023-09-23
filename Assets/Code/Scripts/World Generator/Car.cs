using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay;

public class Car : MonoBehaviour
{
    [SerializeField] public float speed = 10f;
    [SerializeField] public int whichWall;

    private GameObject player;
    private GameplayManager gm;

    // Start is called before the first frame update
    void Start()
    {
        transform.SetParent(null);
        player = GameObject.FindGameObjectWithTag("Player");
        gm = FindFirstObjectByType<GameplayManager>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (player.transform.position.y - 15f > transform.position.y)
            return;


        if (transform.position.z + GetComponent<BoxCollider>().bounds.size.z / 2 < player.transform.position.z - player.GetComponent<BoxCollider>().bounds.size.z / 2) {            
            return;
        }

        LayerMask wallMask = LayerMask.GetMask("WorldObject");

        RaycastHit hit;

        if (!Physics.Raycast(new Ray(transform.position, transform.right * whichWall), out hit, 100f, wallMask)) {
            Destroy(gameObject);
            return;
        }
                
        Vector3 perp = Vector3.Cross(hit.normal, Vector3.up);
        Vector3 targetDir = Vector3.Project(transform.forward, perp).normalized;
        Vector3 currentDir = transform.TransformPoint(Vector3.forward) - transform.position;

        RaycastHit hit2;

        if (Physics.Raycast(new Ray(transform.position, -hit.normal), out hit2, 100f, wallMask)) {
            Vector3 predictPos = transform.position + targetDir;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(predictPos - transform.position),
                5f * Time.deltaTime
            );

            Debug.DrawLine(transform.position, hit2.point);

            transform.position = Vector3.MoveTowards(transform.position, predictPos, speed * Time.deltaTime);
        }
    }
}
