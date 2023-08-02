using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    private void OnEnable()
    {
        //MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        //CombineInstance[] combine = new CombineInstance[10];

        //int i = 0;
        //while (i < 10)
        //{
        //    combine[i].mesh = meshFilters[i].sharedMesh;
        //    combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
        //    meshFilters[i].gameObject.SetActive(false);

        //    i++;
        //}
        //transform.GetComponent<MeshFilter>().mesh = new Mesh();
        //transform.GetComponent<MeshFilter>().mesh.CombineMeshes(combine);
        //transform.gameObject.SetActive(true);
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
            //print("The transform has changed!");
            transform.hasChanged = false;
        }
    }
}