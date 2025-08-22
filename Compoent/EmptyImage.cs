using UnityEngine;
using UnityEngine.UI;

namespace Ddz
{
    /// <summary>
    /// 空，替代仅热区的透明Image
    /// </summary>
    [AddComponentMenu("BoloComponent/EmptyImage (Custom UI)")]
    public class EmptyImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();
        }
    }
}