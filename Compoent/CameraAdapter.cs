/*
 * @Author: your name
 * @Date: 2025-08-20 10:21:25
 * @LastEditTime: 2025-08-20 10:52:44
 * @LastEditors: DESKTOP-GQ4TUDT
 * @Description: 相机适配 
 * @FilePath: \BoloDdz\Assets\BoloExtensions\Scripts\Framework\Compoent\CameraAdapter.cs
 */
using Ddz;
using UnityEngine;

public class CameraAdapter : MonoBehaviour
{
    void Awake()
    {
        Camera camera = GetComponent<Camera>();
        if (camera == null) return;
        ToolCamera.AdapterAspectRatio(camera);
    }
}
