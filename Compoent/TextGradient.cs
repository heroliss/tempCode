using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ddz.BoloExtensions.Component
{
    [AddComponentMenu("UI/Effects/TextGradient")]
    public class TextGradient : BaseMeshEffect
    {
        [SerializeField]
        private Color32 m_TopColor = Color.white;

        [SerializeField]
        private Color32 m_BottomColor = Color.black;

        private List<UIVertex> m_VertexList;
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            if (m_VertexList == null)
            {
                m_VertexList = new List<UIVertex>();
            }

            vh.GetUIVertexStream(m_VertexList);
            ApplyGradient(m_VertexList);

            vh.Clear();
            vh.AddUIVertexTriangleStream(m_VertexList);
        }

        private void ApplyGradient(List<UIVertex> vertexList)
        {
            for (int i = 0; i < vertexList.Count;)
            {
                ChangeColor(vertexList, i, m_TopColor);
                ChangeColor(vertexList, i + 1, m_TopColor);
                ChangeColor(vertexList, i + 2, m_BottomColor);
                ChangeColor(vertexList, i + 3, m_BottomColor);
                ChangeColor(vertexList, i + 4, m_BottomColor);
                ChangeColor(vertexList, i + 5, m_TopColor);
                i += 6;
            }
        }

        private void ChangeColor(List<UIVertex> verList, int index, Color color)
        {
            UIVertex temp = verList[index];
            temp.color = color;
            verList[index] = temp;
        }
    }
}