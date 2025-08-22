using UnityEngine;

namespace Ddz.BoloExtensions.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("BoloComponent/LocalizationNode")]
    public class LocalizationNode : LocalizationBase
    {
        [SerializeField]
        private GameObject twObj = null;
        [SerializeField]
        private GameObject cnObj = null;

        protected override void RefreshView()
        {
            base.RefreshView();
            GameGlobal.MultilingualMgr.Node(new GameObject[] { twObj, cnObj });
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }
            MultilingualMgr.EditorPreviewNode(new GameObject[] { twObj, cnObj });
        }
    }
}