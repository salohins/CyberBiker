using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Car : MonoBehaviour
{
    [SerializeField] private float speed = 10f;

    // Start is called before the first frame update
    void Start()
    {
        transform.SetParent(null);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.Translate(-transform.forward * speed * Time.deltaTime);

        if (!Physics.Raycast(transform.position, -transform.up)) {
            Destroy(this.gameObject);
        }
    }
}
