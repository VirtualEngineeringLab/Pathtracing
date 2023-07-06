﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using Random = UnityEngine.Random;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    [Header("Spheres")]
    public int SphereSeed;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;

    private Camera _camera;
    private float _lastFieldOfView;
    private RenderTexture _target;
    private RenderTexture _converged;
    [SerializeField]
    private Material _addMaterial;
    private uint _currentSample = 0;
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

    public float targetFrametime = 0.01f;
    public bool dynamicRenderScale = true;
    public void DynamicRenderScale(bool dynamic)
    {
        dynamicRenderScale = dynamic;
    }

    public void RenderScaleCalculation()
    {
        if (dynamicRenderScale)
        {
            float t;
            if (XRStats.TryGetGPUTimeLastFrame(out t))
            {
                print(t);
            }
            print(Time.deltaTime.ToString() +" | "+targetFrametime);
            if (Time.deltaTime > targetFrametime)
            {
                RenderScale(renderScale -=0.1f);
            }
            else if (Time.deltaTime < 0.5f * targetFrametime)
            {
                RenderScale(renderScale +=0.1f);
            }
        }
    }

    public void RenderScale(float variable)
    {
        renderScale = variable;
        print(Mathf.RoundToInt(XRSettings.eyeTextureHeight * renderScale) + " " + Mathf.RoundToInt(XRSettings.eyeTextureWidth * renderScale));
        print(Mathf.RoundToInt(Screen.height * renderScale) + " " + Mathf.RoundToInt(Screen.width * renderScale));
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

    public int RenderHeight;
    public int RenderWidth;

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

    private void Awake()
    {
        //InvokeRepeating("RenderScaleCalculation",1f,1f);
        XRSettings.eyeTextureResolutionScale = 1f;
        GetRenderScale();
        _camera = GetComponent<Camera>();


        if (AltRenderMode)
        {
            Setup(_camera);
        }



        if (!XRSettings.enabled && _camera != Camera.main)
        {
            gameObject.SetActive(false);
        }

        transform.GetChild(0).gameObject.SetActive(foveation);

        _transformsToWatch.Add(transform);
        _transformsToWatch.Add(DirectionalLight.transform);
        thisCameraFOV = Camera.main.fieldOfView;
    }

    public void Reset()
    {
        SphereSeed = (int)Time.timeSinceLevelLoad % 1000000 + 100000;
        OnEnable();
    }

    private void OnEnable()
    {

        print(Mathf.RoundToInt(XRSettings.eyeTextureHeight * renderScale) + " " + Mathf.RoundToInt(XRSettings.eyeTextureWidth * renderScale));
        print(Mathf.RoundToInt(Screen.height * renderScale) + " " + Mathf.RoundToInt(Screen.width * renderScale));

        _currentSample = 0;
        SetUpScene();
    }

    private void OnDisable()
    {
        _sphereBuffer?.Release();
        _meshObjectBuffer?.Release();
        _vertexBuffer?.Release();
        _indexBuffer?.Release();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            //ScreenCapture.CaptureScreenshot(Time.time + "-" + _currentSample + ".png");
        }

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

    public static int RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
        return _rayTracingObjects.Count - 1;
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
        _currentSample = actualSampleFrames;

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

    private void SetShaderParameters()
    {
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(0, 0));
        RayTracingShader.SetFloat("_Seed", Random.value);
        RayTracingShader.SetInt("_SamplesPerPixel", samplesPerPixel);
        RayTracingShader.SetVector("_FoveateArea", new Vector4(RenderWidth/3, RenderHeight / 3, RenderWidth*2/3, RenderHeight * 2/3));
        RayTracingShader.SetInt("_SuperSampleRate", 4);

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
        if (_target == null || _target.width != RenderWidth || _target.height != RenderHeight)
        {
            // Release render texture if we already have one
            if (_target != null)
            {
                _target.Release();
                _converged.Release();
            }

            // Get a render target for Ray Tracing
            _target = new RenderTexture(RenderWidth, RenderHeight, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
            _converged = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();

            // Reset sampling
            //_currentSample = 0;
        }
    }

    private void GetRenderScale()//Added by William Sokol Erhard
    {
        if (XRSettings.enabled)
        {
            RenderHeight = Mathf.RoundToInt(XRSettings.eyeTextureHeight * renderScale);
            RenderWidth = Mathf.RoundToInt(XRSettings.eyeTextureWidth * renderScale);
        }
        else
        {
            RenderHeight = Mathf.RoundToInt(Screen.height * renderScale);
            RenderWidth = Mathf.RoundToInt(Screen.width * renderScale);
        }
    }

    Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(RenderWidth, RenderHeight, TextureFormat.RGB24, false);
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
        RenderTexture blit = RenderTexture.GetTemporary((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale)); //get temp RT
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

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
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

    RenderTexture temp;
    RenderTexture temp2;

    public float movementSensitivity = 1f;

    public bool imageBlur = true;
    public void ImageBlur(bool blur)
    {
        imageBlur = blur;
    }

    private void Render(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        // Make sure we have a current render target
        InitRenderTexture();
        actualSampleFrames = sampleFrames;
        if (AltRenderMode)
        {
            AltRender(source,destination);
            return;
        }


        SetShaderParameters();
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(RenderWidth / 32.0f);
        int threadGroupsY = Mathf.CeilToInt(RenderHeight / 32.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        //float movement = Mathf.Max(Mathf.Abs(lastCameraRot.x - thisCameraRot.x),
        //    Mathf.Abs(lastCameraRot.y - thisCameraRot.y),
        //    Mathf.Abs(lastCameraRot.z - thisCameraRot.z));

        ////actualSampleFrames = (uint)Mathf.RoundToInt((sampleFrames - fastSampleFrames) * rotationSensitivity / Mathf.Max(1, movement)) + fastSampleFrames;
        //movement = Math.Max(movement, Vector3.Distance(lastCameraPos, thisCameraPos) * 10);
        //if (movement > movementSensitivity)
        //{
        //    actualSampleFrames = fastSampleFrames;
        //    _converged.Release();
        //    _converged = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0,
        //            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        //    _converged.enableRandomWrite = true;
        //    _converged.Create();
        //    _currentSample = 0;
        //}
        ////else if (movement > movementSensitivity / 2)
        ////{
        ////    actualSampleFrames = sampleFrames / 2;
        ////}
        //else
        {
            actualSampleFrames = sampleFrames;
        }

        //Graphics.Blit(_target, _converged);
        // Blit the result texture to the screen
        //if (_addMaterial == null)
        //    _addMaterial = new Material(Shader.Find("Hidden/AddShaderOriginal"));



        //Material temp = new Material(Shader.Find("Standard"));
        //temp.mainTexture = toTexture2D(_converged);
        //temp.mainTextureOffset = new Vector2(1, 1);
        //_converged.Release();
        //_converged = new RenderTexture(Mathf.RoundToInt(Screen.width * renderScale), Mathf.RoundToInt(Screen.height * renderScale), 0,
        //        RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        //_converged = (RenderTexture)temp.mainTexture;

        //Added by William Sokol Erhard
        if (temp == null || temp2 == null || temp.height != _converged.height || temp.width != _converged.width)
        {
            Destroy(temp);
            Destroy(temp2);
            temp = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            temp2 = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        }


        if (actualSampleFrames > 0)
        {
            //Graphics.Blit(_target, test);
            //_addMaterial.SetTextureOffset("_MainTex", new Vector2(, );
            //if (shiftMat == null)
            //    shiftMat = new Material(Shader.Find("Hidden/ShiftMat"));

            _addMaterial.SetFloat("_Sample", _currentSample);

            shiftMat.SetFloat("_Sample", _currentSample);
            shiftMat.SetFloat("_xOffset", Mathf.DeltaAngle(lastCameraRot.y, thisCameraRot.y) / (thisCameraFOV * Camera.main.aspect));
            shiftMat.SetFloat("_yOffset", -Mathf.DeltaAngle(lastCameraRot.x, thisCameraRot.x) / thisCameraFOV);
            shiftMat.SetFloat("_zOffset", -Mathf.DeltaAngle(lastCameraRot.z, thisCameraRot.z) / thisCameraFOV);


            Graphics.CopyTexture(_converged, temp);
            //temp = _converged;

            _converged.Release();
            _converged = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0,
                    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();

            Graphics.Blit(temp, _converged, shiftMat);
            Graphics.Blit(imageBlur ? Blur(_target, 1) : _target, _converged, _addMaterial);
            Graphics.Blit(_converged, destination);
        }
        else if (foveation)
        {
            //if (fovMat == null)
            //    fovMat = new Material(Shader.Find("Hidden/FovMat"));
            //Texture2D temp = new Texture2D(RenderWidth, RenderHight);
            //CommandBuffer cb = new CommandBuffer();
            //cb.CopyTexture(_converged, temp);            

            Graphics.Blit(_target, _converged);
            Graphics.Blit(Detail, temp, fovMat);
            Graphics.CopyTexture(temp, 0, 0, (int)(temp.width / 3), (int)(temp.height / 3), (int)(temp.width / 3), (int)(temp.height / 3), _converged, 0, 0, (int)(_converged.width / 3), (int)(_converged.height / 3));
            //Graphics.Blit(Detail, temp, shiftMat);//, new Vector2(2f,2f), -new Vector2(0.5f,0.5f));
            //Graphics.Blit(_target, _converged, shiftMat);
            //Graphics.Blit(temp2, _converged, _addMaterial);

            //Graphics.Blit(_converged, _target, _addMaterial);


            Graphics.Blit(_converged, destination);//, Vector2.one, new Vector2(((lastCameraRot.y- thisCameraRot.y) / thisCameraFOV)/Screen.height, ((lastCameraRot.x- thisCameraRot.x) /thisCameraFOV)/ Screen.width));
        }
        else
        {
            //_addMaterial.SetFloat("_Sample", 0);
            //Graphics.Blit(_target, _converged);
            //for (int i =0;i<MultiRender;i++) { 
            //    SetShaderParameters();
            //    // Make sure we have a current render target
            //    InitRenderTexture();


            //    // Set the target and dispatch the compute shader
            //    RayTracingShader.SetTexture(0, "Result", _target);
            //    threadGroupsX = Mathf.CeilToInt(RenderWidth / 32.0f);
            //    threadGroupsY = Mathf.CeilToInt(RenderHight / 32.0f);
            //    RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

            //    _addMaterial.SetFloat("_Sample", MultiRender);
            //    Graphics.Blit(_target, _converged, _addMaterial);
            //}

            //Graphics.Blit(_converged, destination);
            Graphics.Blit(imageBlur ? Blur(_target, 1) : _target, destination);
        }


        if (_currentSample < actualSampleFrames)
            _currentSample++;





    }

    public bool AltRenderMode = false;
    private Camera scene_view_camera;
    private Vector3 last_camera_position;
    private Quaternion last_camera_rotation;
    private Matrix4x4 worldspace_frustum_corners;

    private ComputeShader path_tracing_CS;
    private int groups_x;
    private int groups_y;
    private int path_tracing_kernel;

    private Material tonemap_blit;

    private RenderTexture hdr_rt;

    //PUBLIC METHODS
    public void Setup(Camera cam)
    {
        //I must call this function every time the viewport is resized, VERY IMPORTANT

        scene_view_camera = cam;

        path_tracing_CS = Resources.Load<ComputeShader>("PathTracingCS");
        path_tracing_kernel = path_tracing_CS.FindKernel("PathTrace_uniform_grid");
        path_tracing_CS.SetVector("screen_size", new Vector4((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0, 0));

        groups_x = Mathf.CeilToInt((int)(RenderWidth / renderScale) / 8.0f);
        groups_y = Mathf.CeilToInt((int)(RenderHeight / renderScale) / 8.0f);

        tonemap_blit = new Material(Shader.Find("PathTracing/Tonemap"));

        hdr_rt = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        hdr_rt.enableRandomWrite = true;
        hdr_rt.Create();

        AccelerationStructures.BuildUniformGridGPU();
        SetUniformGrid();
    }

    public void Dispose()
    {
        if (hdr_rt) hdr_rt.Release();
    }

    public void SetUniformGrid()
    {
        path_tracing_CS.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "triangle_list", AccelerationStructures.TriangleBuffer);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "grid_data", AccelerationStructures.GridData);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "index_list", AccelerationStructures.IndexList);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "material_list", AccelerationStructures.MaterialBuffer);
        path_tracing_CS.SetBuffer(path_tracing_kernel, "material_index_list", AccelerationStructures.MaterialIndexBuffer);
        path_tracing_CS.SetInt("num_tris", AccelerationStructures.NumTris);
        path_tracing_CS.SetVector("grid_min", AccelerationStructures.SceneBounds.min);
        path_tracing_CS.SetVector("grid_max", AccelerationStructures.SceneBounds.max);
        path_tracing_CS.SetVector("grid_origin", AccelerationStructures.GridInfo.grid_origin);
        path_tracing_CS.SetVector("grid_size", AccelerationStructures.GridInfo.grid_size);
        path_tracing_CS.SetInt("num_cells_x", (int)AccelerationStructures.GridInfo.nx);
        path_tracing_CS.SetInt("num_cells_y", (int)AccelerationStructures.GridInfo.ny);
        path_tracing_CS.SetInt("num_cells_z", (int)AccelerationStructures.GridInfo.nz);
    }

    private void ResetBuffer()
    {
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = hdr_rt;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = old;
    }


    private void AltRender(RenderTexture source, RenderTexture destination){
        Vector3[] frustumCorners = new Vector3[4];
        scene_view_camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), scene_view_camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
        worldspace_frustum_corners.SetRow(0, scene_view_camera.transform.TransformVector(frustumCorners[0]));
        worldspace_frustum_corners.SetRow(1, scene_view_camera.transform.TransformVector(frustumCorners[1]));
        worldspace_frustum_corners.SetRow(2, scene_view_camera.transform.TransformVector(frustumCorners[3]));
        worldspace_frustum_corners.SetRow(3, scene_view_camera.transform.TransformVector(frustumCorners[2]));
        path_tracing_CS.SetMatrix("worldspace_frustum_corners", worldspace_frustum_corners);
        path_tracing_CS.SetVector("camera_position", scene_view_camera.transform.position);

        path_tracing_CS.SetTexture(path_tracing_kernel, "output", hdr_rt);

        int random_seed = Random.Range(0, int.MaxValue / 100);
        path_tracing_CS.SetInt("start_seed", random_seed);

        path_tracing_CS.Dispatch(path_tracing_kernel, groups_x, groups_y, 1);


        if (temp == null || temp2 == null || temp.height != _converged.height || temp.width != _converged.width)
        {
            Destroy(temp);
            Destroy(temp2);
            temp = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            temp2 = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        }


        if (actualSampleFrames > 0)
        {
            //Graphics.Blit(_target, test);
            //_addMaterial.SetTextureOffset("_MainTex", new Vector2(, );
            //if (shiftMat == null)
            //    shiftMat = new Material(Shader.Find("Hidden/ShiftMat"));

            _addMaterial.SetFloat("_Sample", _currentSample);

            shiftMat.SetFloat("_Sample", _currentSample);
            shiftMat.SetFloat("_xOffset", Mathf.DeltaAngle(lastCameraRot.y, thisCameraRot.y) / (thisCameraFOV * Camera.main.aspect));
            shiftMat.SetFloat("_yOffset", -Mathf.DeltaAngle(lastCameraRot.x, thisCameraRot.x) / thisCameraFOV);
            shiftMat.SetFloat("_zOffset", -Mathf.DeltaAngle(lastCameraRot.z, thisCameraRot.z) / thisCameraFOV);


            Graphics.CopyTexture(_converged, temp);
            //temp = _converged;

            _converged.Release();
            _converged = new RenderTexture((int)(RenderWidth / renderScale), (int)(RenderHeight / renderScale), 0,
                    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();

            Graphics.Blit(temp, _converged, shiftMat);
            Graphics.Blit(imageBlur ? Blur(hdr_rt, 1) : hdr_rt, _converged, _addMaterial);
            Graphics.Blit(_converged, destination);
        }
        else
        {
            Graphics.Blit(imageBlur ? Blur(hdr_rt, 1) : hdr_rt, destination, tonemap_blit, 0);
        }
        if (_currentSample < actualSampleFrames)
            _currentSample++;
        ResetBuffer();
    }


}