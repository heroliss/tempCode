using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Ddz.BoloExtensions.Component
{
    /// <summary>
    /// UGUI描边
    /// </summary>
    [AddComponentMenu("UI/Effects/TextOutLineAndSpace")]
    [DisallowMultipleComponent]
    public class TextOutLineAndSpace : BaseMeshEffect
    {
        [FormerlySerializedAs("OutlineColor")] public Color OutlineClr = Color.white;

        [FormerlySerializedAs("OutlineWidth")]
        [Range(0, 6)]
        public float OutlineWidth;

        [FormerlySerializedAs("AutoAdjustSpaceRate")]
        [Range(0.0f, 2.0f)]
        public float AutoAdjustSpaceRate;

        public bool IsGray = false;

        [FormerlySerializedAs("IsArtTest")] public bool IsArtTest;

        private float m_RealOutlineWidth;

        private static readonly List<UIVertex> VertexList = new();
        private static readonly List<int> LineChatCountList = new();
        private static float m_ScaleFactor = 1.0f;
        private bool m_IsReset = false;

        public float OutlineWidthValue
        {
            get => OutlineWidth;
            set
            {
                OutlineWidth = value;
                _Refresh();
            }
        }

        public Color OutlineClrValue
        {
            get => OutlineClr;
            set
            {
                OutlineClr = value;
                _Refresh();
            }
        }

        protected override void Start()
        {
            base.Start();
            if (!graphic || !graphic.canvas)
            {
                return;
            }

            var canvas = graphic.canvas;
            var v1 = canvas.additionalShaderChannels;
            var v2 = AdditionalCanvasShaderChannels.TexCoord1;
            if ((v1 & v2) != v2)
            {
                canvas.additionalShaderChannels |= v2;
            }

            v2 = AdditionalCanvasShaderChannels.TexCoord2;
            if ((v1 & v2) != v2)
            {
                canvas.additionalShaderChannels |= v2;
            }

            v2 = AdditionalCanvasShaderChannels.TexCoord3;
            if ((v1 & v2) != v2)
            {
                canvas.additionalShaderChannels |= v2;
            }

            _Refresh();
        }

        private static void AdjustOutlineWidth(CanvasScaler canvasScaler)
        {
            var referenceResolution = canvasScaler.referenceResolution;
            float currentWidth = Screen.width;
            float currentHeight = Screen.height;

            var scaleFactor = 1.0f;
            switch (canvasScaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
                    var match = canvasScaler.matchWidthOrHeight;
                    var logWidth = Mathf.Log(currentWidth / referenceResolution.x, 2);
                    var logHeight = Mathf.Log(currentHeight / referenceResolution.y, 2);
                    var logWeightedAverage = Mathf.Lerp(logWidth, logHeight, match);
                    scaleFactor = Mathf.Pow(2, logWeightedAverage);
                    break;
                case CanvasScaler.ScreenMatchMode.Expand:
                    scaleFactor = Mathf.Min(currentWidth / referenceResolution.x, currentHeight / referenceResolution.y);
                    break;
                case CanvasScaler.ScreenMatchMode.Shrink:
                    scaleFactor = Mathf.Max(currentWidth / referenceResolution.x, currentHeight / referenceResolution.y);
                    break;
            }

            m_ScaleFactor = scaleFactor;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (graphic != null && graphic.material != null)
            {
                if (graphic.material.shader.name != "UI/TextOutLineAndSpace")
                {
                    var texMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Shader/TextOutlineAndSpace.mat");
                    if (texMaterial != null)
                    {
                        graphic.material = texMaterial;
                    }
                    else
                    {
                        Debug.LogError("没有找到材质 TextOutlineAndSpace.mat");
                    }
                }

                _Refresh();
            }
        }
#endif


        private void _Refresh()
        {
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
#if UNITY_EDITOR
            //if (graphic != null && graphic.canvas != null)
            //{
            //    var canvasScaler =  graphic.canvas.GetComponent<CanvasScaler>();
            //    if (canvasScaler != null)
            //    {
            //        AdjustOutlineWidth(canvasScaler);
            //    }   
            //}
#endif
            m_RealOutlineWidth = m_ScaleFactor * OutlineWidth;
            vh.GetUIVertexStream(VertexList);

            ProcessVertices();

            vh.Clear();
            vh.AddUIVertexTriangleStream(VertexList);
            if (!m_IsReset && gameObject.activeInHierarchy)
            {
                StartCoroutine(ResetActive());
                m_IsReset = true;
            }
            else
            {

                m_IsReset = false;
            }
        }

        private IEnumerator ResetActive()
        {
            yield return new WaitForEndOfFrame();
            if (gameObject != null && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                gameObject.SetActive(true);
            }
        }

        private void ProcessVertices()
        {
            //计算每行字符数
            CalcLineCharCount();
            // 水平对齐方式
            var alignment = GetAlignment();

            for (int i = 0, line = 0, lineCharCount = LineChatCountList[line], count = VertexList.Count, chatIndex = 0;
                i < count;
                i += 6)
            {
                var v1 = VertexList[i];
                var v2 = VertexList[i + 1];
                var v3 = VertexList[i + 2];
                var v4 = VertexList[i + 3];
                var v5 = VertexList[i + 4];
                var v6 = VertexList[i + 5];
                // 计算原顶点坐标中心点
                var minX = _Min(v1.position.x, v2.position.x, v3.position.x);
                var minY = _Min(v1.position.y, v2.position.y, v3.position.y);
                var maxX = _Max(v1.position.x, v2.position.x, v3.position.x);
                var maxY = _Max(v1.position.y, v2.position.y, v3.position.y);
                var posCenter = new Vector2(minX + maxX, minY + maxY) * 0.5f;
                // 计算原始顶点坐标和UV的方向

                Vector2 pos1 = v1.position, pos2 = v2.position, pos3 = v3.position;
                Vector2 triX = pos2 - pos1, triY = pos3 - pos2, uvX = v2.uv0 - v1.uv0, uvY = v3.uv0 - v2.uv0;
                Vector2 rateX = uvX / triX.magnitude, rateY = uvY / triY.magnitude;

                // 计算原始UV框
                var uvMin = _Min(v1.uv0, v2.uv0, v3.uv0);
                var uvMax = _Max(v1.uv0, v2.uv0, v3.uv0);

                //计算x偏移量
                var outlineXOffset = alignment switch
                {
                    TextAlign.Left => chatIndex * m_RealOutlineWidth,
                    TextAlign.Center => (chatIndex + 0.5f - lineCharCount * 0.5f) * m_RealOutlineWidth,
                    TextAlign.Right => (chatIndex - lineCharCount) * m_RealOutlineWidth,
                    _ => throw new ArgumentOutOfRangeException()
                } * AutoAdjustSpaceRate;

                // 为每个顶点设置新的Position和UV，并传入原始UV框
                v1 = SetNewVertexInfo(v1, m_RealOutlineWidth, posCenter, rateX, rateY, uvMin, uvMax, OutlineClr,
                    outlineXOffset);
                v2 = SetNewVertexInfo(v2, m_RealOutlineWidth, posCenter, rateX, rateY, uvMin, uvMax, OutlineClr,
                    outlineXOffset);
                v3 = SetNewVertexInfo(v3, m_RealOutlineWidth, posCenter, rateX, rateY, uvMin, uvMax, OutlineClr,
                    outlineXOffset);
                v4 = SetNewVertexInfo(v4, m_RealOutlineWidth, posCenter, rateX, rateY, uvMin, uvMax, OutlineClr,
                    outlineXOffset);
                v5 = SetNewVertexInfo(v5, m_RealOutlineWidth, posCenter, rateX, rateY, uvMin, uvMax, OutlineClr,
                    outlineXOffset);
                v6 = SetNewVertexInfo(v6, m_RealOutlineWidth, posCenter, rateX, rateY, uvMin, uvMax, OutlineClr,
                    outlineXOffset);

                if (++chatIndex == lineCharCount && ++line < LineChatCountList.Count)
                {
                    lineCharCount = LineChatCountList[line];
                    chatIndex = 0;
                }

                // 应用设置后的UIVertex
                VertexList[i] = v1;
                VertexList[i + 1] = v2;
                VertexList[i + 2] = v3;
                VertexList[i + 3] = v4;
                VertexList[i + 4] = v5;
                VertexList[i + 5] = v6;
            }
        }

        private static void CalcLineCharCount()
        {
            LineChatCountList.Clear();
            var lastMinY = float.MaxValue;
            var cCount = 0;
            for (int i = 0, count = VertexList.Count; i < count; i += 6)
            {
                var p1 = VertexList[i].position;
                var p2 = VertexList[i + 1].position;
                var p3 = VertexList[i + 2].position;
                var maxY = _Max(p1.y, p2.y, p3.y);

                if (maxY < lastMinY)
                {
                    if (cCount > 0)
                    {
                        LineChatCountList.Add(cCount);
                    }

                    cCount = 1;
                    lastMinY = _Min(p1.y, p2.y, p3.y);
                }
                else
                {
                    ++cCount;
                }
            }

            LineChatCountList.Add(cCount);
        }

        private TextAlign GetAlignment()
        {
            var text = GetComponent<Text>();
            if (text == null)
            {
                throw new ArgumentNullException(nameof(Text));
            }

            return text.alignment switch
            {
                TextAnchor.UpperLeft => TextAlign.Left,
                TextAnchor.UpperCenter => TextAlign.Center,
                TextAnchor.UpperRight => TextAlign.Right,
                TextAnchor.MiddleLeft => TextAlign.Left,
                TextAnchor.MiddleCenter => TextAlign.Center,
                TextAnchor.MiddleRight => TextAlign.Right,
                TextAnchor.LowerLeft => TextAlign.Left,
                TextAnchor.LowerCenter => TextAlign.Center,
                TextAnchor.LowerRight => TextAlign.Right,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private UIVertex SetNewVertexInfo(UIVertex pVertex, float pOutLineWidth,
            Vector2 pPosCenter,
            Vector2 rateX, Vector2 rateY,
            Vector2 pUVOriginMin, Vector2 pUVOriginMax,
            Vector4 outlineCol,
            float outlineXOffset)
        {
            // Position
            var pos = pVertex.position;
            var posXOffset = pos.x > pPosCenter.x ? pOutLineWidth : -pOutLineWidth;
            var posYOffset = pos.y > pPosCenter.y ? pOutLineWidth : -pOutLineWidth;
            pos.x += posXOffset + outlineXOffset;
            pos.y += posYOffset;
            pVertex.position = pos;
            // UV
            Vector2 uv = pVertex.uv0;
            uv += rateX * posXOffset;
            uv -= rateY * posYOffset;
            pVertex.uv0.x = uv.x;
            pVertex.uv0.y = uv.y;
            pVertex.uv0.z = pUVOriginMin.x;
            pVertex.uv0.w = pUVOriginMin.y;
            pVertex.uv1.x = pUVOriginMax.x;
            pVertex.uv1.y = pUVOriginMax.y;

            //使用uv储存outline信息, tangent和normal在缩放情况下会有问题
            pVertex.uv1.z = pOutLineWidth;
            pVertex.uv2 = outlineCol;
            pVertex.uv3.x = IsGray ? 1 : 0;
            return pVertex;
        }

        private static float _Min(float pA, float pB, float pC)
        {
            return Mathf.Min(Mathf.Min(pA, pB), pC);
        }

        private static float _Max(float pA, float pB, float pC)
        {
            return Mathf.Max(Mathf.Max(pA, pB), pC);
        }

        private static Vector2 _Min(Vector2 pA, Vector2 pB, Vector2 pC)
        {
            return new Vector2(_Min(pA.x, pB.x, pC.x), _Min(pA.y, pB.y, pC.y));
        }

        private static Vector2 _Max(Vector2 pA, Vector2 pB, Vector2 pC)
        {
            return new Vector2(_Max(pA.x, pB.x, pC.x), _Max(pA.y, pB.y, pC.y));
        }
    }

#if false
public static class OutlineExManager
{
    /// <summary>
    /// 手动修改像素宽度
    /// </summary>
    /// <param name="fromObj"></param>
    /// <param name="outlineWidth"></param>
    public static void SetOutlineWidth(GameObject fromObj, int outlineWidth)
    {
        if (fromObj == null) return;
        var outlineEx = fromObj.GetComponent<OutlineEx>();
        if (outlineEx != null)
        {
            outlineEx.OutlineWidth = outlineWidth;
        }
    }

    /// <summary>
    /// 手动修改像素颜色
    /// </summary>
    /// <param name="fromObj"></param>
    /// <param name="outlineColor"></param>
    public static void SetOutlineColor(GameObject fromObj, Color outlineColor)
    {
        if (fromObj == null) return;
        var outlineEx = fromObj.GetComponent<OutlineEx>();
        if (outlineEx != null)
        {
            outlineEx.OutlineColor = outlineColor;
        }
    }
}
#endif
    internal enum TextAlign
    {
        Left,
        Center,
        Right
    }
}