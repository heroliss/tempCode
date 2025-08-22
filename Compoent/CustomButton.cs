using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ddz.BoloExtensions.Component
{
    public enum eBtnTransition
    {
        NONE,
        SCALE,
        SPRITE
    }

    [System.Serializable]
    public class CustomButtonOptions
    {
        [SerializeField]
        public Sprite nomal;

        [SerializeField]
        public Sprite pressed;

        [SerializeField]
        public Sprite disabled;

        [SerializeField]
        public float scale = 1.05f;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("BoloComponent/CustomButton")]
    public class CustomButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {

        [SerializeField]
        [Tooltip("按钮是否可交互")]
        private bool interactable = true;

        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable != value)
                {
                    interactable = value;
                    UpdateBtnImage();
                }
            }
        }

        [SerializeField]
        [Tooltip("按钮点击后是否延迟下次点击")]
        private bool isDelay = true;

        public bool IsDelay
        {
            get => isDelay;
            set
            {
                if (isDelay != value)
                {
                    isDelay = value;
                }
            }
        }

        [SerializeField]
        [Tooltip("按钮点击后是否播放默认音频")]
        private bool isPlayDefaultAudio = true;

        public bool IsPlayDefaultAudio
        {
            get => isPlayDefaultAudio;
            set
            {
                if (isPlayDefaultAudio != value)
                {
                    isPlayDefaultAudio = value;
                }
            }
        }

        [SerializeField]
        private eBtnTransition transition = eBtnTransition.SCALE;

        [SerializeField]
        private CustomButtonOptions options = new CustomButtonOptions();

        [SerializeField]
        public UnityEvent clickEvent = new UnityEvent();

        private Vector3 _originalScale;
        private readonly float _duration = 0.1f;
        private readonly float _delayTime = 0.5f;
        private float _lastClickTime = 0f;
        private Image _image;

        private bool _isPointDown = false;

        void Start()
        {
            _originalScale = transform.localScale;
            _image = transform.GetComponent<Image>();
            UpdateBtnImage();
        }

        private void OnDestroy()
        {
            transform?.DOKill();
            RemoveAllListener();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable) return;
            if (transition == eBtnTransition.NONE) return;

            if (_isPointDown) return;
            _isPointDown = true;

            if (transition == eBtnTransition.SCALE)
            {
                transform.DOKill();
                transform.DOScale(_originalScale * options.scale, _duration).SetUpdate(true);
            }
            else
            {
                UpdateImage(options.pressed);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!interactable) return;
            if (transition == eBtnTransition.NONE) return;

            _isPointDown = false;

            if (transition == eBtnTransition.SCALE)
            {
                transform.DOKill();
                transform.DOScale(_originalScale, _duration).SetUpdate(true).OnComplete(() =>
                {
                    _isPointDown = false;
                });
            }
            else
            {
                UpdateImage(options.nomal);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!interactable) return;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable) return;
            if (isPlayDefaultAudio) ToolAudio.PlayButtonClickSound();

            if (isDelay && (Time.time - _lastClickTime < _delayTime)) return;
            _lastClickTime = Time.time;
            clickEvent?.Invoke();
        }


        public void AddListener(UnityAction cb)
        {
            clickEvent.AddListener(cb);
        }

        public void RemoveListener(UnityAction cb)
        {
            clickEvent.RemoveListener(cb);
        }

        public void RemoveAllListener()
        {
            clickEvent.RemoveAllListeners();
        }

        private void UpdateImage(Sprite sp)
        {
            if (_image == null || sp == null || sp == _image.sprite) return;
            _image.sprite = sp;
        }

        public void UpdateBtnImage()
        {
            if (transition == eBtnTransition.SPRITE && _image != null)
            {
                if (interactable)
                {
                    UpdateImage(options.nomal);
                }
                else
                {
                    UpdateImage(options.disabled);
                }
            }
        }

        private void OnValidate()
        {
            if (_image == null)
            {
                _image = transform.GetComponent<Image>();
            }
            UpdateBtnImage();
        }
    }
}