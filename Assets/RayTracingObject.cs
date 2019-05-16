using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    private void OnEnable()
    {
        RayTracingMaster.RegisterObject(this);
    }

    private void OnDisable()
    {
        RayTracingMaster.UnregisterObject(this);
    }

    void Update()
    {
        if (transform.hasChanged)
        {
            RayTracingMaster.UpdateObject(transform);
            print("The transform has changed!");
            transform.hasChanged = false;
        }
    }
}