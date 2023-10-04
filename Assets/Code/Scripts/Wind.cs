using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wind : MonoBehaviour
{
    private ParticleSystem ps;
    private GameObject pc;

    // Start is called before the first frame update
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        pc = GameObject.FindGameObjectWithTag("Player");
    }

    // Update is called once per frame
    void Update() {
        ps.playbackSpeed = pc.GetComponent<Rigidbody>().velocity.z / 100;
        transform.position = new Vector3(Mathf.Lerp(transform.position.x, pc.transform.position.x, Time.deltaTime * 2f),
            pc.transform.position.y,
            pc.transform.position.z + 50f);



        if (pc.GetComponent<Rigidbody>().velocity.z == 0) {
            ps.Stop();
        }
        else {
            ps.Play();
        }
    }
}
