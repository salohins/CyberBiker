using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI;

public class Obstacle : MonoBehaviour {
    [SerializeField] public int damage;
    [SerializeField] private int speed;
    [SerializeField] private int health;
    [SerializeField] ParticleSystem explosion;
    [SerializeField] public float explosionRadius;

    private BoxCollider bc;
    private AIController aiController;

    public int line;

    private void Awake() {
        bc = GetComponent<BoxCollider>();
        aiController = FindFirstObjectByType<AIController>();

       
    }

    private void Update() {
        //SetLine(Scanner.GetLineNumber(transform.position));
    }

    public void SetLine() {
        foreach (Transform line in Scanner.GetLines(transform.position + Vector3.up)) {
            if (line.transform.position.x == transform.position.x) {
                this.line = line.GetComponent<Line>().number;
            }
        }
    }

    public int GetLine() => line;

    public List<DodgePoint> GetDodgePoints(float passLength) {
        BoxCollider bc = GetComponent<BoxCollider>(); ;
        List<DodgePoint> points = new List<DodgePoint>();        

        Transform[] lines = Scanner.GetLines(transform.position + bc.center);        

        if (lines == null)
            return null;

        float offsetX = Mathf.Abs(lines[1].position.x - lines[0].position.x);
        
        for (int i = -1; i < 2; i += 2) {
            for (int j = -1; j < 2; j++) {

                if ((i == -1 || i == 1) && j == 0)
                    continue;

                if (j == -1 && line == 0 || j == 1 && line == lines.Length - 1)
                    continue;

                Vector3 rayPos = transform.position + bc.center;
                
                rayPos.x = lines[line + j].position.x;
                rayPos += Vector3.forward * (bc.bounds.size.z / 2) * i; // Vertical Offset                                

                Collider[] c = Physics.OverlapBox(rayPos, new Vector3(1, 10f, passLength/2), transform.rotation, LayerMask.GetMask("WorldObject")); 

                if (c.Length == 0 || (c.Length == 1 && c[0].gameObject == gameObject)) { 
                    if (j == 0)
                        rayPos += Vector3.forward * passLength/2 * i;

                    points.Add(new DodgePoint(rayPos, gameObject));                    
                }
            }
        }

        return points;
    }

    public Vector3 GetFront() {
        bc = GetComponent<BoxCollider>();
        return transform.position + bc.center + Vector3.forward * (bc.bounds.size.z / 2);
    }

    public Vector3 GetBack() {
        bc = GetComponent<BoxCollider>();
        return transform.position + bc.center + Vector3.back * (bc.bounds.size.z / 2);
    }

    public Vector3 GetSize() {
        return GetComponent<BoxCollider>().bounds.size;
    }

    private void OnDrawGizmos() {
        BoxCollider bc = GetComponent<BoxCollider>(); ;        

        Transform[] lines = Scanner.GetLines(transform.position + bc.center);

        if (lines == null)
            return;

        float offsetX = Mathf.Abs(lines[1].position.x - lines[0].position.x);

        for (int i = -1; i < 2; i += 2) {
            for (int j = -1; j < 2; j++) {

                Vector3 rayPos = transform.position + bc.center;

                rayPos += Vector3.right * offsetX * j; // Horizontal Offset
                rayPos += Vector3.forward * (bc.bounds.size.z / 2) * i; // Vertical Offset
                              
                Collider[] c = Physics.OverlapBox(rayPos, new Vector3(1, 1, aiController.passLength/2f), transform.rotation, LayerMask.GetMask("WorldObject"));

                foreach (Collider col in c) {
                      if (col.gameObject != gameObject) {
                          Gizmos.color = Color.red;
                         Gizmos.DrawWireCube(rayPos, new Vector3(1, 1, aiController.passLength));
                        Gizmos.DrawWireSphere(col.gameObject.transform.position, .1f);
                    }
                }
            }
        }
    }
}