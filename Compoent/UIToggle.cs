using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ddz.BoloExtensions.Component
{
    [ExecuteAlways]
    [RequireComponent(typeof(Toggle))]
    public class UIToggle : MonoBehaviour
    {
        public Action<Toggle, bool> onValueChanged;
        //******  属性面板可设置的值 ********
        [SerializeField]
        private List<GameObject> offObjs;
        [SerializeField]
        private List<GameObject> onObjs;

        public bool isOn
        {
            set
            {
                if (_toggle != null)
                    _toggle.isOn = value;
                _OnValueChanged();
            }

            get
            {
                if (_toggle == null)
                {
                    _toggle = GetComponent<Toggle>();
                }

                return _toggle.isOn;
            }
        }
        private Toggle _toggle = null;

        public List<GameObject> OffObjects
        {
            get { return offObjs; }
            set
            {
                offObjs = value;
                if (_toggle != null)
                    _OnValueChanged();
            }
        }
        public List<GameObject> OnObjects
        {
            get { return onObjs; }
            set
            {
                onObjs = value;
                if (_toggle != null)
                    _OnValueChanged();
            }
        }
        void Awake()
        {
            _toggle = GetComponent<Toggle>();

            _toggle.onValueChanged.AddListener(OnValueChanged);
            _OnValueChanged();

        }

        private void OnValueChanged(bool isOn)
        {
            _OnValueChanged();
            if (onValueChanged != null)
                onValueChanged(_toggle, isOn);
        }

        private void _OnValueChanged()
        {
            if (_toggle != null)
            {
                if (offObjs != null && offObjs.Count > 0)
                {
                    foreach (GameObject offObj in offObjs)
                    {
                        offObj.SetActive(!_toggle.isOn);
                    }
                }
                if (onObjs != null && onObjs.Count > 0)
                {
                    foreach (GameObject onObj in onObjs)
                    {
                        onObj.SetActive(_toggle.isOn);
                    }
                }
            }
        }

        public Toggle GetToggle()
        {
            if (_toggle == null)
            {
                _toggle = GetComponent<Toggle>();
            }

            return _toggle;
        }

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }
        }
    }
}