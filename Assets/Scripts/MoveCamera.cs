using UnityEngine;
using UnityEngine.InputSystem;

 
public class MoveCamera : MonoBehaviour {
    public float minX = -360.0f;
    public float maxX = 360.0f;

    public float minY = -45.0f;
    public float maxY = 45.0f;

    public float sensX = 1.0f;
    public float sensY = 1.0f;

    float rotationY = 0.0f;
    float rotationX = 0.0f;

    [SerializeField] private bool move = true;

    private float speed = 5.0f;

    Transform t;
    private void Start()
    {
        t = Camera.main.transform;
    }

    bool deadframe = true;

    void Update()
    {        
        if(RayTracingMaster.xrEnabled){
            transform.localScale = new Vector3(1, -1, 1);
            transform.localRotation = Quaternion.Euler(180,0,0);
        }else{
            transform.localScale = Vector3.one;
            transform.eulerAngles = new Vector3(-rotationY, rotationX , 0);

        }

        if(move){
            if (Keyboard.current[Key.RightArrow].IsPressed()||Keyboard.current[Key.D].IsPressed())
            {
                transform.position += t.right * speed * Time.deltaTime;
            }
            if (Keyboard.current[Key.LeftArrow].IsPressed()||Keyboard.current[Key.A].IsPressed())
            {
                transform.position -= t.right * speed * Time.deltaTime;
            }
            if (Keyboard.current[Key.UpArrow].IsPressed()||Keyboard.current[Key.W].IsPressed())
            {
                transform.position += t.forward * speed * Time.deltaTime;
            }
            if (Keyboard.current[Key.DownArrow].IsPressed()||Keyboard.current[Key.S].IsPressed())
            {
                transform.position -= t.forward * speed * Time.deltaTime;
            }
            if (Keyboard.current[Key.DownArrow].IsPressed()||Keyboard.current[Key.E].IsPressed())
            {
                transform.position += t.up * speed * Time.deltaTime;
            }
            if (Keyboard.current[Key.DownArrow].IsPressed()||Keyboard.current[Key.Q].IsPressed())
            {
                transform.position -= t.up * speed * Time.deltaTime;
            }
        }
        if (Input.GetMouseButton(1) && !RayTracingMaster.xrEnabled)
        {            
            if(deadframe){deadframe=false;return;}

            rotationX += Input.GetAxis("Mouse X")* sensX;
            rotationY += Input.GetAxis("Mouse Y")* sensY;
            rotationY = Mathf.Clamp(rotationY, minY, maxY);
            Vector3 cross = Vector3.Cross(t.forward, transform.forward);
            Vector3 crossO = Vector3.Cross(transform.forward, t.forward);
            transform.eulerAngles = new Vector3(-rotationY, rotationX , 0);
        }else{
            deadframe = true;
        }
    }

}