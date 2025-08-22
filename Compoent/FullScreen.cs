using UnityEngine;

namespace Ddz.BoloExtensions.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("BoloComponent/FullScreen")]
    public class FullScreen : MonoBehaviour
    {
        [SerializeField]
        private bool isUIBg = true;
        [SerializeField]
        private Vector2 bgSize = new Vector2(2340, 1080);

        void Awake()
        {
            CalculateBg();
        }

        private void CalculateBg()
        {
            // 窄屏的不管
            if (!GlobalConfig.IsWideScreen)
            {
                return;
            }

            float bgRatio = bgSize.x / bgSize.y;
            Vector3 orgScale = transform.localScale;

            // 计算背景需要的缩放比例
            if (GlobalConfig.ScreenRatio >= bgRatio)
            {
                // 如果当前屏幕更宽，则适配宽度
                transform.localScale = orgScale * (GlobalConfig.ScreenRatio / bgRatio);
            }
            else if (isUIBg)
            {
                // 如果当前屏幕更窄，则适配高度
                transform.localScale = orgScale * (bgRatio / GlobalConfig.ScreenRatio);
            }
        }
    }
}

