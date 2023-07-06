using UnityEngine;
using UnityEngine.InputSystem;

 
public class MoveCamera : MonoBehaviour {
    public float minX = -360.0f;
    public float maxX = 360.0f;

    public float minY = -45.0f;
    public float maxY = 45.0f;

    public float sensX = 100.0f;
    public float sensY = 100.0f;

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
        if (Input.GetMouseButton(1))
        {            
            if(deadframe){deadframe=false;return;}

            rotationX += Input.GetAxis("Mouse X")* sensX * Time.deltaTime;
            rotationY += Input.GetAxis("Mouse Y")* sensY * Time.deltaTime;
            rotationY = Mathf.Clamp(rotationY, minY, maxY);
            transform.eulerAngles = new Vector3(-rotationY, rotationX , 0);
        }else{
            deadframe = true;
        }
    }

}