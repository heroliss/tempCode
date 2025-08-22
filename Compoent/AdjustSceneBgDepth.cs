using Ddz;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AdjustSceneBgDepth : MonoBehaviour
{
    public Camera targetCamera;
    
    void Start()
    {
        if (targetCamera == null)
            targetCamera = ToolCamera.MainCamera;
            
        AdjustDepth();
    }
    
    public void AdjustDepth()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        float spriteHeight = spriteRenderer.bounds.size.y;
        
        if (targetCamera.orthographic)
        {
            // 正交摄像机计算
            float orthoSize = targetCamera.orthographicSize;
            float distance = spriteHeight / (2 * orthoSize) * targetCamera.farClipPlane * 0.9f;
            transform.position = new Vector3(
                transform.position.x,
                transform.position.y,
                distance);
        }
        else
        {
            // 透视摄像机计算
            float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float distance = spriteHeight * 0.5f / Mathf.Tan(fovRad * 0.5f);
            transform.position = new Vector3(
                transform.position.x,
                transform.position.y,
                distance);
        }
    }
}