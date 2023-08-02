using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightMover : MonoBehaviour
{
    public float multiplier=1f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(transform.root.up,Time.deltaTime* multiplier, Space.World);
        transform.Rotate(transform.root.forward,Time.deltaTime* multiplier/5, Space.World);
    }
}
