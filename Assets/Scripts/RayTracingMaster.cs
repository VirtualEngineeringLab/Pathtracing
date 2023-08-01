using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using Random = UnityEngine.Random;
using UnityEngine.Rendering.Denoising;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Events;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Experimental.Rendering;
using System.Threading;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine.Rendering.HighDefinition;

using UnityEngine.XR.Management;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    [SerializeField] private Material _renderTextureMat = null;

    [Header("Spheres")]
    public int SphereSeed;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;

    private Camera _camera;
    private Projector _projector;
    private float _lastFieldOfView;
    public RenderTexture _target;
    public RenderTexture denoisedTex;
    [SerializeField] private RenderTexture _converged;
    [SerializeField]
    private Material _addMaterial;
    [SerializeField] private uint _currentSample = 0;
    private ComputeBuffer _sphereBuffer;
    private static List<Transform> _transformsToWatch = new List<Transform>();
    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    public Transform obj;

    public void RenderScale(float variable)
    {
        renderScale = variable;
    }
    public void SamplesPer(float variable)
    {
        samplesPerPixel = (int)variable;
    }
    public void Accumulation(float variable)
    {
        sampleFrames = (uint)variable;
    }

    [SerializeField]
    private uint sampleFrames = 3;

    [SerializeField]
    private uint fastSampleFrames = 1;

    private uint actualSampleFrames;

    [SerializeField]
    private float renderScale = 1f;

    [SerializeField]
    private int samplesPerPixel = 1;

    public int RenderHight;
    public int RenderWidth;

    public Text ReproPerf;
    public Text RenderPerf;

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
    }

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    }

    private static GameObject otherEye;

    private void Awake()
    {
        
        XRSettings.eyeTextureResolutionScale = 1f;

        _camera = GetComponent<Camera>();
        _projector = GetComponent<Projector>();
        if (!XRSettings.enabled && _camera != Camera.main)
        {
            otherEye ??= gameObject;
            gameObject.SetActive(false);
        }

        transform.GetChild(0).gameObject.SetActive(foveation);

        _transformsToWatch.Add(transform);
        _transformsToWatch.Add(DirectionalLight.transform);
        thisCameraFOV = Camera.main.fieldOfView;

    }

    public void Reset()
    {
        SphereSeed = (int)Time.timeSinceLevelLoad%1000000 + 100000;
        OnEnable();
    }

    private void OnEnable()
    {       

        print(Mathf.RoundToInt(XRSettings.eyeTextureHeight * renderScale) +" " + Mathf.RoundToInt(XRSettings.eyeTextureWidth * renderScale));
        print(Mathf.RoundToInt(Screen.height * renderScale) +" " + Mathf.RoundToInt(Screen.width * renderScale));

        _currentSample = 0;
        SetUpScene();
        if(planerReproject){
            StartCoroutine(ASyncRenderer());
        }else{
            RenderPipelineManager.beginCameraRendering += RenderPipelineRender;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _sphereBuffer?.Release();
        _meshObjectBuffer?.Release();
        _vertexBuffer?.Release();
        _indexBuffer?.Release();
    }
    static bool xrEnabled = false;
  
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote) && gameObject != otherEye)
        {
            xrEnabled = !xrEnabled;
            
            if(xrEnabled){
                StartCoroutine(StartXRCoroutine());
            }else{
                StartCoroutine(StopXR());
            }
            
            otherEye?.SetActive(true);
        }else if (Input.GetKeyDown(KeyCode.F1))
        {
            renderMode = RenderMode.Default;
        }else if (Input.GetKeyDown(KeyCode.F2))
        {
            renderMode = RenderMode.Reproj;
        }else if (Input.GetKeyDown(KeyCode.F3))
        {
            renderMode = RenderMode.BlurAndReproj;
        }else if (Input.GetKeyDown(KeyCode.F4))
        {
            renderMode = RenderMode.StereoReproj;
        }else if (Input.GetKeyDown(KeyCode.F10))
        {
            renderMode = RenderMode.NewRender;
        }else if (Input.GetKeyDown(KeyCode.F11))
        {
            renderMode = RenderMode.DepthPause;
        }else if (Input.GetKeyDown(KeyCode.F12))
        {
            renderMode = RenderMode.PlanerPause;
        }
        
        // this example shows the different camera frustums when using asymmetric projection matrices (like those used by OpenVR).

        // var camera = GetComponent<Camera>();
        // Vector3[] frustumCorners = new Vector3[4];
        // camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

        // for (int i = 0; i < 4; i++)
        // {
        //     var worldSpaceCorner = camera.transform.TransformVector(frustumCorners[i]);
        //     Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.blue);
        // }

        // camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Left, frustumCorners);

        // for (int i = 0; i < 4; i++)
        // {
        //     var worldSpaceCorner = camera.transform.TransformVector(frustumCorners[i]);
        //     Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.green);
        // }

        // camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Right, frustumCorners);

        // for (int i = 0; i < 4; i++)
        // {
        //     var worldSpaceCorner = camera.transform.TransformVector(frustumCorners[i]);
        //     Debug.DrawRay(camera.transform.position, worldSpaceCorner, Color.red);
        // }
        
        // if (Input.GetKeyDown(KeyCode.F12))
        // {
        //     //ScreenCapture.CaptureScreenshot(Time.time + "-" + _currentSample + ".png");
        // }

        if (_camera.fieldOfView != _lastFieldOfView)
        {
            //_currentSample = 0;
            _lastFieldOfView = _camera.fieldOfView;
        }

        foreach (Transform t in _transformsToWatch)
        {
            if (t.hasChanged)
            {
                _meshObjectsNeedRebuilding = true;
                //_currentSample = 0;
                t.hasChanged = false;
            }
        }
    }

    public IEnumerator StartXRCoroutine()
    {
        Debug.Log("Initializing XR...");
        yield return XRGeneralSettings.Instance.Manager.InitializeLoader();

        if (XRGeneralSettings.Instance.Manager.activeLoader == null)
        {
            Debug.LogError("Initializing XR Failed. Check Editor or Player log for details.");
        }
        else
        {
            Debug.Log("Starting XR...");
            XRGeneralSettings.Instance.Manager.StartSubsystems();
        }
        renderMode = RenderMode.StereoReproj;
    }
    IEnumerator StopXR()
    {
        Debug.Log("Stopping XR...");

        XRGeneralSettings.Instance.Manager.StopSubsystems();
        XRGeneralSettings.Instance.Manager.DeinitializeLoader();
        Debug.Log("XR stopped completely.");
        yield return null;
        
        otherEye.SetActive(false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        renderMode = RenderMode.Reproj;
    }    

    public static int RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);        
        _meshObjectsNeedRebuilding = true;
        return _rayTracingObjects.Count-1;
    }
        
    public static void UpdateObject(Transform tra)
    {
        _transformsToWatch.Add(tra);
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private void SetUpScene()
    {
        Random.InitState(SphereSeed);
        List<Sphere> spheres = new List<Sphere>();

        // Add a number of random spheres
        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();

            // Radius and radius
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }

            // Albedo and specular color
            Color color = Random.ColorHSV();
            float chance = Random.value;
            if (chance < 0.8f)
            {
                bool metal = chance < 0.4f;
                sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);
                sphere.smoothness = Random.value;
            }
            else
            {
                Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
                sphere.emission = new Vector3(emission.r, emission.g, emission.b);
            }

            // Add the sphere to the list
            spheres.Add(sphere);

            SkipSphere:
            continue;
        }

        // Assign to compute buffer
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        if (spheres.Count > 0)
        {
            _sphereBuffer = new ComputeBuffer(spheres.Count, 56);
            _sphereBuffer.SetData(spheres);
        }
    }

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }

        _meshObjectsNeedRebuilding = false;
        // _currentSample = actualSampleFrames;

        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);

            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));

            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }

        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 72);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
        where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }

        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }

            // Set data on the buffer
            buffer.SetData(data);
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
    private Matrix4x4 oldIPR;
    private Matrix4x4 oldCTW;
    private Matrix4x4 oldWTC;
    private Matrix4x4 oldPRJ;

    private void SetShaderParameters()
    {
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        
        if(_camera.stereoEnabled){
            bool left = _camera.stereoTargetEye == StereoTargetEyeMask.Left;
            left = !left;
            RayTracingShader.SetBool("_leftEye",left);
            var vMatrix = _camera.GetStereoViewMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right).inverse;
            RayTracingShader.SetMatrix("_CameraToWorld", vMatrix);

            var pMatrix = _camera.GetStereoProjectionMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right).inverse;
            RayTracingShader.SetMatrix("_CameraInverseProjection", pMatrix);

            RayTracingShader.SetMatrix("_CameraInverseProjectionOld",oldIPR);
            RayTracingShader.SetMatrix("_CameraToWorldOld", oldCTW);
            RayTracingShader.SetMatrix("_WorldToCameraOld", oldWTC);
            RayTracingShader.SetMatrix("_CameraProjectionOld", oldPRJ);
            oldIPR = pMatrix;
            oldCTW = vMatrix;
            oldWTC = _camera.GetStereoViewMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right);
            oldPRJ = _camera.GetStereoProjectionMatrix(left? Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right);
        }else
        {           
            
            RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);            
            RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

            RayTracingShader.SetMatrix("_CameraInverseProjectionOld",oldIPR);
            RayTracingShader.SetMatrix("_CameraToWorldOld", oldCTW);
            RayTracingShader.SetMatrix("_WorldToCameraOld", oldWTC);
            RayTracingShader.SetMatrix("_CameraProjectionOld", oldPRJ);
            oldIPR = _camera.projectionMatrix.inverse;
            oldCTW = _camera.cameraToWorldMatrix;
            oldWTC = _camera.worldToCameraMatrix;
            oldPRJ = _camera.projectionMatrix;
        }
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value-0.5f, Random.value-0.5f));
        RayTracingShader.SetFloat("_Seed", Random.value);
        RayTracingShader.SetInt("_SamplesPerPixel", samplesPerPixel);
        RayTracingShader.SetInt("_Depth", depth? 1 : 0);
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);
    }

    private void InitRenderTexture()
    {
        GetRenderScale();
        if (_target == null || _target.width != RenderWidth || _target.height != RenderHight)
        {
            // Release render texture if we already have one
            if (_target != null)
            {
                _target.Release();
                _converged.Release();
            }

            if(RenderWidth == 0 ||  RenderHight == 0){
                return;
            }

            // Get a render target for Ray Tracing
            _target = new RenderTexture(RenderWidth, RenderHight, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
            _converged = new RenderTexture((int)(RenderWidth/renderScale), (int)(RenderHight / renderScale), 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();

            denoisedTex = new RenderTexture((int)(RenderWidth/renderScale), (int)(RenderHight / renderScale), 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            denoisedTex.enableRandomWrite = true;
            denoisedTex.Create();
                            
            _renderTextureMat.mainTexture = _target;  

            // Reset sampling
            //_currentSample = 0;
        }
    }

    private void GetRenderScale()
    {
        if (XRSettings.enabled)
        {
            RenderHight = Mathf.RoundToInt(XRSettings.eyeTextureHeight * renderScale);
            RenderWidth = Mathf.RoundToInt(XRSettings.eyeTextureWidth * renderScale);
        }
        else
        {
            RenderHight = Mathf.RoundToInt(Screen.height * renderScale);
            RenderWidth = Mathf.RoundToInt(Screen.width * renderScale);
        }
    }

    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(RenderWidth, RenderHight, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        RenderTexture.active = _target;
        tex.Apply();
        return tex;
    }
    RenderTexture Blur(RenderTexture source, int iterations)
    {
        RenderTexture result = source; //result will store partial results (blur iterations)
        //blur = new Material(Shader.Find("Blur")); //create blur material
        RenderTexture blit = RenderTexture.GetTemporary((int)(RenderWidth / renderScale), (int)(RenderHight / renderScale)); //get temp RT
        for (int i = 0; i < iterations; i++)
        {
            Graphics.SetRenderTarget(blit);
            GL.Clear(true, true, Color.black); //avoid artifacts in temp RT by clearing it
            Graphics.Blit(result, blit, blur); //PERFORM A BLUR ITERATION
            result = blit; //overwrite partial result
        }
        RenderTexture.ReleaseTemporary(blit);
        return result; //return the last partial result
    }

    public class PathtracingPipeline : RenderPipeline
    {
        public PathtracingPipeline() {
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            // Create and schedule a command to clear the current render target
            var cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, true, Color.red);
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();

            
            // Tell the Scriptable Render Context to tell the graphics API to perform the scheduled commands
            context.Submit();
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination);    
        if(planerReproject){
            return;
        }
        //UnityEngine.XR.XRSettings.eyeTextureResolutionScale = 2;
        thisCameraRot = _camera.transform.rotation.eulerAngles;
        thisCameraPos = _camera.transform.position;
        RebuildMeshObjectBuffers();        
        Render(source, destination);
        lastCameraRot = thisCameraRot;
        lastCameraPos = thisCameraPos;
    }

    private Vector3 lastCameraRot;
    private Vector3 lastCameraPos;
    private Vector3 thisCameraRot;
    private Vector3 thisCameraPos;
    private float thisCameraFOV;
    public Texture Detail;
    [SerializeField]
    private Material shiftMat;
    [SerializeField]
    private Material fovMat;
    [SerializeField]
    public Material blur;

    [SerializeField]
    public bool foveation = false;

    // RenderTexture temp;
    // RenderTexture temp2;

    public float movementSensitivity = 1f;

    public bool imageBlur = true;
    public void ImageBlur(bool blur)
    {
        imageBlur = blur;
    }

    public bool depth = false;

    public void ImageDepth(bool depthI)
    {
        depth = depthI;
    }

    public bool RenderPathtracing = true;

    public void RenderToggle(bool render)
    {
        RenderPathtracing = render;
    }
    public static bool RenderPathtracingStatic = true;
    private bool RenderDirty = false;
    int divisions = 10;
    int counter = 0;
    public bool planerReproject = false;
    private void Render(RenderTexture source, RenderTexture destination)
    {
        
        // if(isRendering){
        //     // Graphics.Blit(_target, destination);    
        //     return;
        // }
        
        isRendering = true;
        RebuildMeshObjectBuffers();        

        SetShaderParameters();
        // Make sure we have a current render target
        InitRenderTexture();

        RenderPathtracingStatic = RenderPathtracing;
        if(RenderPathtracing && _target != null && RenderWidth>0 && RenderHight>0){
            ReproPerf.text = "";
            RenderPerf.text = $"{renderTimer*1000f}";
            // Set the target and dispatch the compute shader
            RayTracingShader.SetInt("renderMode", (int)renderMode);
            RayTracingShader.SetInt("_Divisions", divisions);
            RayTracingShader.SetInt("_Counter", counter%divisions);
            RayTracingShader.SetInt("_AccumulationFrames", (int)sampleFrames);

            RayTracingShader.SetTexture(0, "Result", _target);
            RayTracingShader.SetTexture(0, "Result1", _converged);
            int threadGroupsX = Mathf.CeilToInt(RenderWidth / 32.0f);
            int threadGroupsY = Mathf.CeilToInt(RenderHight / 32.0f);
            RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
            // if(counter%10!=0){
            //     return;
            // }
        }else{
            RenderDirty = true;
            _currentSample = 0;
            Graphics.Blit(source, destination);          
            return;
        }
        if(xrEnabled){
            Graphics.Blit(_target, destination, shiftMat);    
        }else{
            Graphics.Blit(_target, destination);    
        }

        // float movement = Mathf.Max(Mathf.Abs(lastCameraRot.x - thisCameraRot.x),
        //     Mathf.Abs(lastCameraRot.y - thisCameraRot.y),
        //     Mathf.Abs(lastCameraRot.z - thisCameraRot.z));

        //actualSampleFrames = (uint)Mathf.RoundToInt((sampleFrames - fastSampleFrames) * rotationSensitivity / Mathf.Max(1, movement)) + fastSampleFrames;
        // movement = Math.Max(movement, Vector3.Distance(lastCameraPos,thisCameraPos)*10);
        // if (movement > movementSensitivity)
        // {
        //     actualSampleFrames = fastSampleFrames;
        //     _renderTextureMat.mainTexture = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); 
        //     // _converged.Release();
        //     // _converged = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHight / renderScale), 0,
        //     //         RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        //     // _converged.enableRandomWrite = true;
        //     // _converged.Create();
        //     _currentSample = 0;
        // }
        // else if (movement > movementSensitivity / 2)
        // {
        //     actualSampleFrames = sampleFrames / 2;
        // }
        // else
        // {
        //     actualSampleFrames = sampleFrames;
        // }

        
        // if (temp == null || temp2 == null ||temp.height!=_converged.height || temp.width != _converged.width)
        // {
        //     Destroy(temp);
        //     Destroy(temp2);
        //     temp = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        //     temp2 = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        // }

        // if(_renderTextureMat != null){   
        //     _addMaterial.SetFloat("_Sample", depth ? 0 : _currentSample);

            // _converged.Release();
            // _converged = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHight / renderScale), 0,
            //         RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            // _converged.enableRandomWrite = true;
            // _converged.Create();     

            //Graphics.Blit(source, _converged);

            
      
            // Graphics.Blit(source, _converged);
            // if(_currentSample>0){
            //      Graphics.Blit(source, _target, _addMaterial);
            // }
            // _renderTextureMat.mainTexture = _target;

            
            // if(cmd==null || cmdDenoiser == null){
            //     cmd = new CommandBuffer();
            //     cmdDenoiser = new CommandBufferDenoiser();
            // }
            // // Initialize the denoising state
            // result = cmdDenoiser.Init(DenoiserType.Optix, RenderWidth, RenderHight);
            // // Assert.AreEqual(Denoiser.State.Success, result);

            // // Create a new denoise request for a color image stored in a Render Texture
            // cmdDenoiser.DenoiseRequest(cmd, "color", _target);

            // // // Use albedo AOV to improve results
            // // denoiser.DenoiseRequest(cmd, "albedo", _target);

            // // // Check if the denoiser request is still executing
            // // while (denoiser.QueryCompletion() != Denoiser.State.Executing)
            // // {
            // //    yield return null;
            // // }
            // // Create a new denoise request for a color image stored in a Render Texture
            // var src = ScriptableRenderContext();

            // // Wait until the denoising request is done executing
            // result = cmdDenoiser.WaitForCompletion(src, cmd);
            // // Assert.AreEqual(Denoiser.State.Success, result);

            // // Get the results
            // // var dst = new RenderTexture(colorImage.descriptor);
            // result = cmdDenoiser.GetResults(cmd, _target);
            // StartCoroutine(waitForDenoise());
            
             // Check if the denoiser request is still executing
            // if (cmdDenoiser.QueryCompletion() != Denoiser.State.Executing)
            // {
            //     // Get the results
            //     // var dst = new RenderTexture(_target.descriptor);
            //     result = cmdDenoiser.GetResults(cmd, destination);
            //     print("success denoising");
            //     // Assert.AreEqual(Denoiser.State.Success, result);
            // }

            // Get the results
            // var dst = new RenderTexture(_target.descriptor);
            // result = cmdDenoiser.GetResults(cmd, dst);
            // Graphics.Blit(dst, _target);
            // Assert.AreEqual(Denoiser.State.Success, result);
            

            // Graphics.Blit(_target, destination);    
        //     isRendering = false;

        //     if(RenderDirty){
        //         RenderDirty = false;  
        //         return;    
        //     }
        // }
        // else{
        //     Graphics.Blit(imageBlur?Blur(_target, 1): _target, destination, shiftMat);
        // }


        // if (_currentSample < actualSampleFrames)
        //     _currentSample++;
        // else
        //     _currentSample = actualSampleFrames;
    }
    CommandBuffer cmd;    

    // Create a new denoiser object
    CommandBufferDenoiser cmdDenoiser = null;
    Denoiser.State result;

    IEnumerator waitForDenoise(){
        print(cmdDenoiser.QueryCompletion());
        while (cmdDenoiser.QueryCompletion() == Denoiser.State.Executing)
        {
            yield return null;
            print(cmdDenoiser.QueryCompletion());
        }
        // var dstemp = new RenderTexture(_target.descriptor);
        result = cmdDenoiser.GetResults(cmd, _target);
        print("success denoising");
        isRendering = false;
    }

    private void RenderPipelineRender(ScriptableRenderContext context, Camera camera){
        print("rendering");
        if(isRendering){
            return;
        }
        DelayedFollow.reprojectionUpdate?.Invoke();
        // _camera.transform.position -= camera.transform.right;
        isRendering = true;
        RebuildMeshObjectBuffers();        

        SetShaderParameters();
        // Make sure we have a current render target
        InitRenderTexture();

        RenderPathtracingStatic = RenderPathtracing;
        if(RenderPathtracing){
            ReproPerf.text = "";
            RenderPerf.text = $"{renderTimer*1000f}";
            // Set the target and dispatch the compute shader
            RayTracingShader.SetInt("renderMode", (int)renderMode);
            RayTracingShader.SetInt("_Divisions", divisions);
            RayTracingShader.SetInt("_Counter", counter%divisions);

            RayTracingShader.SetTexture(0, "Result", _target);
            RayTracingShader.SetTexture(0, "Result1", _converged);
            int threadGroupsX = Mathf.CeilToInt(RenderWidth / 32.0f);
            int threadGroupsY = Mathf.CeilToInt(RenderHight / 32.0f);
            RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
            print("success rendering");
        }

        if(cmd==null || cmdDenoiser == null){
            cmd = new CommandBuffer();
            cmdDenoiser = new CommandBufferDenoiser();
            print("initialize denoising");            
            // result = cmdDenoiser.Init(DenoiserType.Optix, RenderWidth, RenderHight);
        }
        
        cmd.Clear();
    
        result = cmdDenoiser.Init(DenoiserType.Optix, RenderWidth, RenderHight);

        // Create a new denoise request for a color image stored in a Render Texture
        cmdDenoiser.DenoiseRequest(cmd, "color", _target);

        // Wait until the denoising request is done executing
        result = cmdDenoiser.WaitForCompletion(context, cmd);
        // Assert.AreEqual(Denoiser.State.Success, result);
        print(result);
        
        result = cmdDenoiser.GetResults(cmd, _target);
        print("success denoising");
        cmdDenoiser?.DisposeDenoiser();
        
        isRendering = false;
    }

    

    private bool isRendering = false;

    private float renderTimer = 0f;

    // public int resetnum = 0;
    public enum RenderMode{
        Default = 1,
        Reproj = 2,
        BlurAndReproj = 3,
        StereoReproj = 4,
        NewRender = 10,
        DepthPause = 11,
        PlanerPause = 12,
    }
    public static RenderMode renderMode = RenderMode.Default;
    NativeArray<Vector4> dst;
    Texture2D cpuTexture;
    Denoiser denoiser;

    void RenderAsyncSync(){
        // if(counter%divisions==divisions-2){
        //     resetPos = true;
        // }

        // if(counter%divisions==0 || depth)
        {
            DelayedFollow.reprojectionUpdate?.Invoke();
            ReproPerf.text = $"{Time.deltaTime*1000f}";
            RenderPerf.text = $"{renderTimer*1000f}";
            renderTimer = 0;
            RebuildMeshObjectBuffers();        

            SetShaderParameters();
            // Make sure we have a current render target
            InitRenderTexture();
        }
        
        renderTimer += Time.deltaTime;
        RenderPathtracingStatic = RenderPathtracing;
        if(RenderPathtracing){
            isRendering = true;
            // RayTracingShader.SetBuffer(0, "Result", _computeBuffer);
            // Set the target and dispatch the compute shader
            // AsyncGPUReadback.Request(_target, );
            RayTracingShader.SetInt("renderMode", (int)renderMode);
            // RayTracingShader.SetInt("_Divisions", divisions);
            // RayTracingShader.SetInt("_Counter", counter%divisions);
            

            RayTracingShader.SetTexture(0, "Result", _target);
            RayTracingShader.SetTexture(0, "Result1", _converged);

            int threadGroupsX = Mathf.CeilToInt(RenderWidth / 32.0f);
            int threadGroupsY = Mathf.CeilToInt(RenderHight / 32.0f);
            RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
            // if(counter%divisions!=0){
            //     return;
            // }
        }else{
            RenderDirty = true;
            _currentSample = 0;
            // Graphics.Blit(source, destination);          
            // return;
        }

        float movement = Mathf.Max(Mathf.Abs(lastCameraRot.x - thisCameraRot.x),
            Mathf.Abs(lastCameraRot.y - thisCameraRot.y),
            Mathf.Abs(lastCameraRot.z - thisCameraRot.z));

        actualSampleFrames = sampleFrames;

        // else{
        //     // Graphics.Blit(imageBlur?Blur(_target, 1): _target, destination, shiftMat);
        // }
        
        // if(false && counter%divisions==0 && RenderPathtracing)
        {
        unsafe{
            Debug.LogError("Denoising");
            Denoiser.State result;
            if(denoiser == null
            || dst == null || dst.Length != RenderWidth * RenderHight*4
            || cpuTexture== null || cpuTexture.width != RenderWidth)
            {
                denoiser?.DisposeDenoiser();
                // Create a new denoiser object
                denoiser = new Denoiser();

                // Initialize the denoising state
                result = denoiser.Init(DenoiserType.Optix, RenderWidth, RenderHight);
                dst = new NativeArray<Vector4>(RenderWidth * RenderHight*4, Allocator.Temp);
            
                cpuTexture = new Texture2D(RenderWidth, RenderHight,TextureFormat.RGBAFloat, 1, false);
            }
            // Assert.AreEqual(Denoiser.State.Success, result);
            

            // dst.Copy(_target.colorBuffer);
            // byte* ptr = (byte*)_target.colorBuffer.GetNativeRenderBufferPtr().ToPointer();
            // byte[] temp = new byte[RenderWidth * RenderHight*4];
            // Marshal.Copy(_target.colorBuffer.GetNativeRenderBufferPtr(), temp, 0, RenderWidth * RenderHight*4);
            // Marshal.Copy(temp, 0,(IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(dst), RenderWidth * RenderHight*4);

            // NativeArray<Vector4> src = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Vector4>(
            //     _target.colorBuffer.GetNativeRenderBufferPtr().ToPointer()
            //     ,RenderWidth * RenderHight*4, Allocator.Persistent
            // );
            
            // AsyncGPUReadback.RequestIntoNativeArray(ref dst, _target, Denoise);
            
            // Buffer.MemoryCopy(_target.colorBuffer.GetNativeRenderBufferPtr().ToPointer(),NativeArrayUnsafeUtility.GetUnsafePtr(dst),RenderWidth * RenderHight*4,RenderWidth * RenderHight*4);
            // Buffer.MemoryCopy(temp.GetNativeTexturePtr().ToPointer(),NativeArrayUnsafeUtility.GetUnsafePtr(dst),RenderWidth * RenderHight*4,RenderWidth * RenderHight*4);
            // dst.CopyFrom(temp.GetNativeTexturePtr().ToPointer());
            
            // dst = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Vector4>(
            //     temp.GetNativeTexturePtr().ToPointer()
            //     ,RenderWidth * RenderHight*4, Allocator.Temp
            // );

            RenderTexture.active =_target;
            Graphics.CopyTexture(_target, cpuTexture);
            cpuTexture.ReadPixels(new Rect(0, 0, RenderWidth, RenderHight), 0, 0);
            
            RenderTexture.active = null;   
            // _target.Release();
            
            dst = cpuTexture.GetRawTextureData<Vector4>();
            Destroy(cpuTexture);


            // Create a new denoise request for a colorImage on a native array
            result = denoiser.DenoiseRequest("color", dst);
            // Assert.AreEqual(Denoiser.State.Success, result);
            print("denoising request");

            // Get the results
            try{
                result = denoiser.GetResults(dst);
                Debug.LogError(result);
                cpuTexture.SetPixelData<Vector4>(dst, 0, 0);
                cpuTexture.Apply();
                Graphics.CopyTexture(cpuTexture, _target);
                Debug.LogError("copy denoising");

            }catch(Exception e){
                Debug.LogError(e);
            }
            // print("success denoising");
            // Assert.AreEqual(Denoiser.State.Success, result);
        }}


        // var rgc = new RenderGraphContext();
        // var cmd = new CommandBuffer();
        // //  Create a new denoiser object
        // CommandBufferDenoiser denoiser = new CommandBufferDenoiser();

        // // Initialize the denoising state
        // Denoiser.State result = denoiser.Init(DenoiserType.Optix, RenderWidth, RenderHight);
        // // Assert.AreEqual(Denoiser.State.Success, result);

        // // Create a new denoise request for a color image stored in a Render Texture
        // denoiser.DenoiseRequest(cmd, "color", _target);

        // // var src = new ScriptableRenderContext();

        // // Wait until the denoising request is done executing
        // // result = denoiser.WaitForCompletion(src, cmd);
        // // Assert.AreEqual(Denoiser.State.Success, result);

        // // Get the results
        // // var dst = new RenderTexture(colorImage.descriptor);
        // // var dst = new RenderTexture(_target.descriptor);

        // result = denoiser.GetResults(cmd, _target);
        // // Assert.AreEqual(Denoiser.State.Success, result);


        // var cmd = new CommandBuffer();
        

        // // Create a new denoiser object
        // CommandBufferDenoiser denoiser = new CommandBufferDenoiser();

        // // Initialize the denoising state
        // Denoiser.State result = denoiser.Init(DenoiserType.Optix, RenderWidth, RenderHight);
        // // Assert.AreEqual(Denoiser.State.Success, result);

        // // Create a new denoise request for a color image stored in a Render Texture
        // denoiser.DenoiseRequest(cmd, "color", _target);

        // // // Use albedo AOV to improve results
        //// denoiser.DenoiseRequest(cmd, "albedo", _target);

        // // Check if the denoiser request is still executing
        // while (denoiser.QueryCompletion() != Denoiser.State.Executing)
        // {
        //    yield return null;
        // }
        
        // // Get the results
        // var dst = new RenderTexture(_target.descriptor);
        // result = denoiser.GetResults(cmd, dst);
        // Graphics.Blit(dst, _target);
        // // Assert.AreEqual(Denoiser.State.Success, result);
        // print("success denoising");
        counter++;
        isRendering = false;

        if(RenderDirty){
            RenderDirty = false;  
        }        
    }
    public void FramerateToggle(string framerate)
    {
        divisions = Int32.Parse(framerate);
        // frametimer = 1f/Int32.Parse(framerate);
    }
    public float frametimer = 0.05f;
    private float timer = 0.05f;

    public static bool resetPos = false;
    [SerializeField] private Material depthMat;
    IEnumerator ASyncRenderer()
    {
        while(true){
            // while((timer-=Time.deltaTime)>0 || isRendering){
            //     yield return null;
                
            // }
            // yield return null;
            // timer = frametimer;            
            
            // if(_renderTextureMat != null){   
            //     // _addMaterial.SetFloat("_Sample", depth ? 0 : _currentSample);
            
            //     // _camera.targetTexture = _converged;
            //     // _camera.Render();

            //     // if(_currentSample>0){
            //     //      Graphics.Blit(_converged, _target, _addMaterial);
            //     // }
            //     // Graphics.Blit(_target, _converged); 
            //     // var viewMatrix = _camera.worldToCameraMatrix;
            //     // var projectionMatrix = _camera.projectionMatrix;
            //     // projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false);
            //     // var clipToPos = (projectionMatrix * viewMatrix).inverse;
            //     // depthMat.SetMatrix("clipToWorld", clipToPos);

  
            //     // _camera.targetTexture = null;   
                
            // }
            yield return null;
            
            while(isRendering){
                yield return null;
            }
            isRendering = true;
            RenderAsyncSync();
            // StartCoroutine(RenderAsyncSync());
            
        }
    }
}
