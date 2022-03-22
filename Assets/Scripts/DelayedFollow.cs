using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DelayedFollow : MonoBehaviour
{
    [SerializeField] private Camera _target;
    [SerializeField] private int _framesOfDelay = 1;
    
    private Queue<(Vector3, Quaternion)> _poseHistory  = new Queue<(Vector3, Quaternion)>();
    private float _currentFOV = 0f;

    public float offset = 5f;
    public float wOffset = 0.95f;
    public float hOffset = 0.95f;
    public float xPosOffset = 0.1f;
    public float yPosOffset = 0.1f;

    void ResetScaleAndOffset(){
        _currentFOV = _target.fieldOfView;
       
        var frustumHeight = 2.0f * transform.localScale.z * Mathf.Tan(_currentFOV * 0.5f * Mathf.Deg2Rad);
        var frustumWidth = frustumHeight * _target.aspect; 
        transform.localScale = new Vector3(frustumWidth, frustumHeight, transform.localScale.z);
        if(_target.stereoEnabled){
            // bool left = _target.stereoTargetEye == StereoTargetEyeMask.Left;
            // var matrix = _target.projectionMatrix.inverse;//_target.GetStereoViewMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right);
            // frustumHeight = (2.0f * transform.localScale.z) /matrix[1,1];
            transform.localScale = new Vector3(frustumWidth*wOffset, frustumHeight*hOffset, transform.localScale.z);
        }        
    }

    // Update is called once per frame
    void Update()
    {
        //if(_currentFOV != Camera.main.fieldOfView)
        {
            ResetScaleAndOffset();
        }
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
            pos = _target.transform.position+_target.transform.forward*transform.localScale.z;
            rot = _target.transform.rotation;
        }
        
        _poseHistory.Enqueue((pos, rot));//_target.transform.rotation*Quaternion.Euler(0,angleOffset,0)));
        if(_poseHistory.Count>_framesOfDelay){
            var temp = _poseHistory.Dequeue();
            transform.position = temp.Item1;
            transform.rotation = temp.Item2;
        }
    }
}
