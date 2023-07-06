using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class DepthCloudRenderer : MonoBehaviour
{
    Texture2D _textureColor;
    Texture2D _textureScale;
    public RenderTexture _renderTexture;
    public Material _renderTextureMat;
    VisualEffect _vFX;
    public int _width = 3840;
    public int _height = 3840;

    bool isDirty = false;
    int particleCount;

    // Texture2D tex;

    // Start is called before the first frame update
    void Start()
    {
        _vFX = GetComponent<VisualEffect>();
    }

    // Update is called once per frame
    void Update()
    {
        // 
        // if(isDirty)
        if(_renderTextureMat.mainTexture!=null)
        {
            // RenderTexture.active = (RenderTexture)_renderTextureMat.mainTexture;
            // tex = new Texture2D(_width, _height, TextureFormat.RGB24, false);
            // Rect rectReadPicture = new Rect(0, 0, _width, _height);
            // tex.ReadPixels(rectReadPicture, 0, 0);
            // tex.Apply();
            // RenderTexture.active = null; 
            // isDirty =false;
            // _renderTexture= _renderTextureMat.mainTexture;
            // particleCount = (int)(_renderTexture.height*_renderTexture.width);
            // _vFX.Reinit();
            _vFX.SetUInt(Shader.PropertyToID("PixelCount"), (uint)particleCount);
            // _vFX.SetTexture(Shader.PropertyToID( "texColor"), tex);
            _vFX.SetTexture(Shader.PropertyToID("texColor"), _renderTextureMat.mainTexture);
            // _vFX.SetTexture(Shader.PropertyToID( "texScale"), _textureScale);
            _vFX.SetUInt(Shader.PropertyToID("Height"), (uint)_height);
            _vFX.SetUInt(Shader.PropertyToID("Width"), (uint)_width);
            // Destroy(tex);
        }

        // Rasterize();//_renderTextureMat.mainTexture.ToTexture2D());
    }

    public void Rasterize(){
        var cam = Camera.main.transform;
        // _renderTexture = RenderImage;
        particleCount = _width;//(int)(RenderImage.height*RenderImage.width);
        int count = 0;
        Vector3[] positions = new Vector3[particleCount]; 
        Color[] colors = new Color[particleCount];
        for(int i = 0; i<_height; i++){
            for(int j = 0; j<_width; j++){
                positions[count] = cam.position + cam.forward+cam.right*0.01f*j+cam.forward+cam.up*0.01f*i;//RenderImage.GetPixel(i,j);
                colors[count++] = Color.red;//RenderImage.GetPixel(i,j);
            }
        }
        Rasterize(positions, colors);
        isDirty = true;
    }
    
    public void Rasterize(Vector3[] positions, Color[] colors) {
        _textureColor = new Texture2D(_width, _height);
        _textureScale = new Texture2D(_width, _height);

        for (int y = 0; y < _height; y++) {
            for (int x = 0; x < _width; x++) {
                int index = x + y * _height;
                _textureColor.SetPixel(x, y, colors [index] ) ;
                var data = new Color(positions[index].x, positions[index].y, positions[index].z, 1);
                _textureScale.SetPixel(x, y, data);
            }
            
        }
        _textureColor.Apply();
        _textureScale.Apply();
        particleCount = (int)positions.Length;
        isDirty = true;
    }
}
//https://www.youtube.com/watch?v=P5BgrdXis68