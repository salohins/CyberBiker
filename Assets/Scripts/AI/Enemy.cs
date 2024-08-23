using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI;
using System.Linq;
using UnityEditor;




public enum EnemyState {
        Spawn,
        Attack,
        Dead,
    }


public class Enemy : MonoBehaviour {
    [SerializeField] private Transform[] rayCheck;    

    private EnemyState state;        

    private GameObject player;    

    private float speed;
    private int moveDirection;            
        
    public float distanceController;
    private float _playerDistance;
            

    public GameObject motoMesh;
                       
    public float playerDistance {
        get {
            return _playerDistance;
        }

        set {
            if (Vector3.Distance(transform.position, player.transform.position) < value) {
                moveDirection = 1;
            }
            else if (Vector3.Distance(transform.position, player.transform.position) > value) {
                moveDirection = -1;
            }
            else {
                moveDirection = 0;
            }

            _playerDistance = value;
        }
    }
    
    private AIController aiConntroller;
    private int currentWaypointIndex = 1;
    
    Vector3 followTarget;

    DodgePoint path;    

    void Start() {
        player = GameObject.FindGameObjectWithTag("Player");    
        _playerDistance = distanceController;        
        aiConntroller = FindFirstObjectByType<AIController>();
    }
        
    private void Update() {
        var targetRotation = transform.eulerAngles;
        targetRotation.z = -targetRotation.y * 2f;
        targetRotation.x = 0;
        targetRotation.y = 0;

        motoMesh.transform.localRotation = Quaternion.Slerp(
            motoMesh.transform.localRotation,
            Quaternion.Euler(targetRotation),
            Time.deltaTime * 10f
        );          
        if (followTarget != null && followTarget != Vector3.zero) {
            transform.LookAt(followTarget, Vector3.up);
        }
        else {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 10f);
        }
    }

    private void FixedUpdate() {
        if (state == EnemyState.Dead)
            return;

        transform.Translate(Vector3.forward * Time.deltaTime * speed);

        if (Vector3.Distance(transform.position, player.transform.position) < 10) {
            state = EnemyState.Attack;
            moveDirection = 1;
        }

        if (state == EnemyState.Spawn) {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z - 30f, Time.deltaTime);
        }
        else {
            if (player.GetComponent<PlayerController>().wallDirection != 0) {
                speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z - 2f, Time.deltaTime);
            }
            else {
                ProcessSpeed();
            }
        }

        if (path == null) {
                             
                followTarget = new Vector3(rayCheck[1].position.x, transform.position.y, rayCheck[1].position.z) + transform.forward * 10f;
                path = aiConntroller.GetPath(new DodgePoint(transform.position, gameObject));                

                currentWaypointIndex = 1;                            
        }

        if (path != null) {            
                Vector3 currentWaypoint = new Vector3(path.GetPosition().x, transform.position.y, path.GetPosition().z);
                float step = speed * Time.deltaTime;
                followTarget = Vector3.MoveTowards(followTarget, currentWaypoint, step);

                if (followTarget.z >= currentWaypoint.z) {
                    currentWaypointIndex++;
                }

                if (followTarget.z >= path.GetPosition().z) {
                    ClearPath();
                }                        
        }
        
        
    }

    private void ClearPath() {
        path = null;
        currentWaypointIndex = 1;
        followTarget = Vector3.zero;
    }
    
    private void ProcessSpeed() {
        if (moveDirection == 1 && (Vector3.Distance(transform.position, player.transform.position) < _playerDistance)) {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z + 10f, Time.deltaTime * 5f);
        }
        else if (moveDirection == -1 && (Vector3.Distance(transform.position, player.transform.position) > _playerDistance)) {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z - 10f, Time.deltaTime * 5f);
        }
        else {
            speed = Mathf.Lerp(speed, player.GetComponent<Rigidbody>().velocity.z, Time.deltaTime);
        }
    }


    private void OnDrawGizmosSelected() {        
        if (path != null) {

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(path.GetPosition(), 1);
                Gizmos.DrawLine(transform.position, path.GetPosition());
        }
            
                
            
            
                  

        if (followTarget != null && followTarget != Vector3.zero) {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(followTarget, .5f);
        }
    }    
}




