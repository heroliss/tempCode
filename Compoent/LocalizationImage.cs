using UnityEngine;
using UnityEngine.UI;

namespace Ddz.BoloExtensions.Component
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("BoloComponent/LocalizationImage")]
    public class LocalizationImage : LocalizationBase
    {
        [SerializeField]
        private Sprite twSprite = null;
        [SerializeField]
        private Sprite cnSprite = null;
        private Image _image;

        protected override void RefreshView()
        {
            base.RefreshView();
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }
            if (_image == null)
            {
                return;
            }
            GameGlobal.MultilingualMgr.Img(_image, new Sprite[] { twSprite, cnSprite });
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }
            if (_image == null)
            {
                return;
            }
            MultilingualMgr.EditorPreviewImg(_image, new Sprite[] { twSprite, cnSprite });
        }
    }
}