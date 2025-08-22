using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleInverseEvent : MonoBehaviour
{
    [System.Serializable]
    public class InverseEvent : UnityEvent<bool> { }

    public InverseEvent onInverseValueChanged = new InverseEvent();

    private Toggle _toggle;

    void Awake()
    {
        _toggle = GetComponent<Toggle>();
        _toggle.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnValueChanged(bool value)
    {
        // 触发反向事件（传递相反的值）
        onInverseValueChanged.Invoke(!value);
    }

    void OnDestroy()
    {
        if (_toggle != null)
        {
            _toggle.onValueChanged.RemoveListener(OnValueChanged);
        }
    }
}