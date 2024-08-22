using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MuzzleFlash : MonoBehaviour
{
    private float frameTimer = 3;
    [SerializeField] private GameObject muzzleFlash;
    private bool active;

    private GameObject _muzzleFlash;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (active) {
            frameTimer -= 1;
        }
        

        if (frameTimer <= 0) {
            Destroy(_muzzleFlash);
            frameTimer = 3;
            active = false;
        }

        //Debug.Log(frameTimer);
    }

    public void Spawn() {
        _muzzleFlash = Instantiate(muzzleFlash, transform);
        _muzzleFlash.transform.localEulerAngles = new Vector3(Random.Range(0, 360), 0, 0);
        active = true;
    }
}
