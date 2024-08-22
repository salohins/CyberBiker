using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gameplay;
using AI;

public class Car : MonoBehaviour
{
    [SerializeField] public float speed = 10f;
    [SerializeField] public int whichWall;

    [SerializeField] private int health;
    [SerializeField] ParticleSystem explosion;

    private bool dead;
    private bool hit;

    private GameObject player;
    private GameplayManager gm;

    private AIController aiController;

    public int lineNumber;

    private void Awake() {
        /*foreach (Transform line in Scanner.GetLines(transform.position + Vector3.up)) {
            if (line.transform.position.x == transform.position.x) {
                lineNumber = line.GetComponent<Line>().number;
            }
        }*/
    }

    // Start is called before the first frame update
    void Start()
    {
        health = 5;
        transform.SetParent(null);
        player = GameObject.FindGameObjectWithTag("Player");
        gm = FindFirstObjectByType<GameplayManager>();
        aiController = FindFirstObjectByType<AIController>();
    }

    public void GetDamage() {        
        health--;
    }

    public Vector3 GetFrontPosition() {
        return transform.position + transform.forward * (GetComponent<BoxCollider>().bounds.size.z / 2);
    }

    public Vector3 GetBackPosition() {
        return transform.position - transform.forward * (GetComponent<BoxCollider>().bounds.size.z / 2);
    }

    private void Update() {
        if (health == 0 && dead == false) {
            dead = true;
            Instantiate(explosion, transform);
            GetComponent<BoxCollider>().enabled = false;
            GetComponent<MeshRenderer>().enabled = false;
        }        

        if (hit) {
            speed = Mathf.Lerp(speed, 0, Time.deltaTime * 10f);
            
        }
    }
    // Update is called once per frame

    private void OnCollisionEnter(Collision collision) {        

        if (collision.gameObject.tag == "Player") {
            hit = true;
            GetComponent<BoxCollider>().enabled = false;
        }
    }

    void FixedUpdate()
    {    
        /*if (player.transform.position.z > transform.position.z + 20f) {
            Destroy(gameObject);
        }*/

        LayerMask wallMask = LayerMask.GetMask("WorldObject");

        RaycastHit hit;

        if (!Physics.Raycast(new Ray(transform.position, transform.right * whichWall), out hit, 100f, wallMask)) {


            /*List<DodgePoint> pointsToRemove = new List<DodgePoint>();

            foreach (DodgePoint point in aiController.dodgePoints.Where(obj => obj.offsetObject == gameObject)) {
                pointsToRemove.Add(point);
            }

            foreach (DodgePoint point in pointsToRemove) {
                aiController.dodgePoints.Remove(point);
            }*/


            //aiController.activeCars.Remove(gameObject);
            //Destroy(gameObject);            
            

            return;
        }      

        if (player.transform.position.y - 15f > transform.position.y)
            return;


        if (transform.position.z + GetComponent<BoxCollider>().bounds.size.z / 2 < player.transform.position.z - player.GetComponent<BoxCollider>().bounds.size.z / 2) {            
            return;
        }

        if (health <= 0)
            return;
        

        Vector3 perp = Vector3.Cross(hit.normal, Vector3.up);
        Vector3 targetDir = Vector3.Project(transform.forward, perp).normalized;

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
