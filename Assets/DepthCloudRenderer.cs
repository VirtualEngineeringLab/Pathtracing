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
    int _width = 3840;
    int _height = 2160;

    bool isDirty = false;
    int particleCount;

    // Start is called before the first frame update
    void Start()
    {
        _vFX = GetComponent<VisualEffect>();
    }

    // Update is called once per frame
    void Update()
    {
        // if(isDirty)
        if(_renderTextureMat.mainTexture!=null)
        {
            isDirty =false;
            // _renderTexture= _renderTextureMat.mainTexture;
            // particleCount = (int)(_renderTexture.height*_renderTexture.width);
            _vFX.Reinit();
            _vFX.SetUInt(Shader.PropertyToID("PixelCount"), (uint)particleCount);
            // _vFX.SetTexture(Shader.PropertyToID( "texColor"), _textureColor);
            _vFX.SetTexture(Shader.PropertyToID("texColor"), _renderTextureMat.mainTexture);
            // _vFX.SetTexture(Shader.PropertyToID( "texScale"), _textureScale);
            _vFX.SetUInt(Shader.PropertyToID("Height"), (uint)_height);
            _vFX.SetUInt(Shader.PropertyToID("Width"), (uint)_width);
        }
    }

    public void Rasterize(RenderTexture RenderImage){
        _renderTexture = RenderImage;
        particleCount = (int)(RenderImage.height*RenderImage.width);
        isDirty = true;
    }
    
    public void Rasterize(Vector3[] positions, Color[] colors) {
        // _textureColor = new Texture2D(_width, _height);
        // _textureScale = new Texture2D(_width, _height);

        // for (int y = 0; y < _height; y++) {
        //     for (int x = 0; x < _width; x++) {
        //         int index = x + y * _height;
        //         _textureColor.SetPixel(x, y, colors [index] ) ;
        //         var data = new Color(positions[index].x, positions[index].y, positions[index].z, 1);
        //         _textureScale.SetPixel(x, y, data);
        //     }
            
        // }
        // _textureColor.Apply();
        // _textureScale.Apply();
        particleCount = (int)positions.Length;
        isDirty = true;
    }
}
//https://www.youtube.com/watch?v=P5BgrdXis68