using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DelayedFollow : MonoBehaviour
{
    [SerializeField] private Camera _target;
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private int _framesOfDelay = 1;
    
    private Queue<(Vector3, Quaternion)> _poseHistory  = new Queue<(Vector3, Quaternion)>();
    private float _currentFOV = 0f;

    public float offset = 5f;
    public float wOffset = 0.95f;
    public float hOffset = 0.95f;
    public float xPosOffset = 0.1f;
    public float yPosOffset = 0.1f;

    void Start()
     {
         StartCoroutine(LateStart());
     }
 
     IEnumerator LateStart()
     {
        yield return new WaitForSeconds(0.05f);
        // yield return null;
        var camera = _target;
        if(_target.stereoEnabled){
            bool left = _target.stereoTargetEye == StereoTargetEyeMask.Left;

            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, 
            left?Camera.MonoOrStereoscopicEye.Left:Camera.MonoOrStereoscopicEye.Right, 
            corners);

            // for (int i = 0; i < 4; i++)
            // {
            //     var worldSpaceCorner = camera.transform.TransformVector(corners[i]);
            //     Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.green);
            // }

            var worldSpaceCorner = camera.transform.TransformVector(corners[0]);
            Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.black);
            worldSpaceCorner = camera.transform.TransformVector(corners[1]);
            Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.yellow);
            worldSpaceCorner = camera.transform.TransformVector(corners[2]);
            Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.red);
            worldSpaceCorner = camera.transform.TransformVector(corners[3]);
            Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.green);

      
        }else
        {
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, corners);

            for (int i = 0; i < 4; i++)
            {
                var worldSpaceCorner = camera.transform.TransformVector(corners[i]);
                Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.blue);
            }
        }


        Vector3[] vertices = new Vector3[4]
        {
            corners[0].normalized*10-0.25f*camera.transform.forward,
            corners[3].normalized*10-0.25f*camera.transform.forward,
            corners[1].normalized*10-0.25f*camera.transform.forward,
            corners[2].normalized*10-0.25f*camera.transform.forward,

            // camera.transform.InverseTransformPoint((camera.transform.TransformVector(corners[0])).normalized*5-0.1f*camera.transform.forward),
            // camera.transform.InverseTransformPoint((camera.transform.TransformVector(corners[3]) ).normalized*5-0.1f*camera.transform.forward),           
            // camera.transform.InverseTransformPoint((camera.transform.TransformVector(corners[1])).normalized*5-0.1f*camera.transform.forward),
            // camera.transform.InverseTransformPoint((camera.transform.TransformVector(corners[2]) ).normalized*5-0.1f*camera.transform.forward),           

        };
        // for (int i = 2; i < 6; i++)
        // {
        //     var worldSpaceCorner = camera.transform.TransformVector(corners[i%4]);
        //     vertices[i%4] = (worldSpaceCorner - camera.transform.position).normalized + camera.transform.position;
        // }

        _meshFilter.mesh.vertices = vertices;
        _meshFilter.mesh.bounds = new Bounds(camera.transform.position+camera.transform.forward*10f, Vector3.one*10f);
        
        
    }

    // void ResetScaleAndOffset(){
    //     _currentFOV = _target.fieldOfView;
       
    //     var frustumHeight = 2.0f * transform.localScale.z * Mathf.Tan(_currentFOV * 0.5f * Mathf.Deg2Rad);
    //     var frustumWidth = frustumHeight * _target.aspect; 
    //     transform.localScale = new Vector3(frustumWidth, frustumHeight, transform.localScale.z);
    //     if(_target.stereoEnabled){
    //         // bool left = _target.stereoTargetEye == StereoTargetEyeMask.Left;
    //         // var matrix = _target.projectionMatrix.inverse;//_target.GetStereoViewMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right);
    //         // frustumHeight = (2.0f * transform.localScale.z) /matrix[1,1];
    //         transform.localScale = new Vector3(frustumWidth*wOffset, frustumHeight*hOffset, transform.localScale.z);
    //     }        
    // }

    private Vector3[] corners = new Vector3[4];

    // Update is called once per frame
    void Update()
    {
        if(RayTracingMaster.resetPos){
            RayTracingMaster.resetPos = false;
        }else{
            if(_poseHistory.Count>0){
                var temp = _poseHistory.Peek();
                transform.position = temp.Item1;
                transform.rotation = temp.Item2;
            }
            return;
        }
        Camera currentCamera = Camera.main;
        // Matrix4x4 matrixCameraToWorld = currentCamera.cameraToWorldMatrix;
        // Matrix4x4 matrixProjectionInverse = GL.GetGPUProjectionMatrix(currentCamera.projectionMatrix, false).inverse;
        // Matrix4x4 matrixHClipToWorld = matrixCameraToWorld * matrixProjectionInverse;

        // Shader.SetGlobalMatrix("_MatrixHClipToWorld", matrixHClipToWorld);
        if(!RayTracingMaster.RenderPathtracingStatic){return;}
        // Awake();
        // transform.position = _target.transform.position+_target.transform.forward*transform.localScale.z;
        // transform.rotation = _target.transform.rotation;


        // if(_currentFOV != Camera.main.fieldOfView)
        // {
        //     ResetScaleAndOffset();
        // }
        Vector3 pos;
        Quaternion rot;
        if(_target.stereoEnabled){
            bool left = _target.stereoTargetEye == StereoTargetEyeMask.Left;
            var matrix = _target.GetStereoViewMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right).inverse;
            pos = (Vector3)matrix.GetColumn(3);//new Vector3(matrix.m03, matrix.m13, matrix.m23);
            //pos += (left?-1:1)*_target.stereoSeparation*transform.localScale.x*(Vector3)matrix.GetColumn(0)*offset;
            float angleOffset = (left?-1:1)* offset;//_target.stereoConvergence;
            Vector3 ray = Quaternion.AngleAxis(angleOffset, (Vector3)matrix.GetColumn(1))*(-transform.localScale.z*(Vector3)matrix.GetColumn(2));
            Debug.DrawRay(pos, ray);   
            pos += ray; //new Vector3(matrix.m02, matrix.m12, matrix.m22);
            pos += xPosOffset*_target.transform.up + yPosOffset*_target.transform.right;
            //rot = Quaternion.LookRotation(ray,(Vector3)matrix.GetColumn(1));//-(Vector3)matrix.GetColumn(2)
            rot = _target.transform.rotation;
        }else
        {
            pos = _target.transform.position+_target.transform.forward*0.25f;
            rot = _target.transform.rotation;
        }
            
        _poseHistory.Enqueue((pos, rot));//_target.transform.rotation*Quaternion.Euler(0,angleOffset,0)));
        while(_poseHistory.Count>_framesOfDelay){
            var temp = _poseHistory.Dequeue();
            transform.position = temp.Item1;
            transform.rotation = temp.Item2;
        }
    }
}
