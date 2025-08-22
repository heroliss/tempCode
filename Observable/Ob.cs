using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 可观察对象类
/// </summary>
/// <typeparam name="T">值的类型</typeparam>
/// <remarks>
/// 当值被修改且与原值不同时，会触发含有旧值和新值的事件通知。
/// </remarks>
[Serializable]
public class Ob<T>
{
    [SerializeField]
    private T _value;

    /// <summary>
    /// 值变化时触发的事件
    /// </summary>
    [SerializeField]
    public UnityEvent<T, T> OnValueChanged = new();

    /// <summary>
    /// 初始化可观察对象的新实例
    /// </summary>
    /// <param name="defaultValue">初始默认值</param>
    public Ob(T defaultValue = default)
    {
        _value = defaultValue;
    }

    /// <summary>
    /// 获取或设置可观察对象的值，当值被修改且与原值不同时，会触发含有旧值和新值的事件通知
    /// </summary>
    public T Value
    {
        get => _value;
        set
        {
            // 使用相等比较器检查值是否实际发生变化
            if (EqualityComparer<T>.Default.Equals(_value, value))
                return; // 值未变化，不触发事件

            // 保存旧值并更新为新值
            T oldValue = _value;
            _value = value;

            // 触发值变化事件，传递旧值和新值
            OnValueChanged.Invoke(oldValue, _value);
        }
    }

    /// <summary>
    /// 隐式转换操作符，允许将 Ob<T> 直接当作 T 使用
    /// </summary>
    public static implicit operator T(Ob<T> ob) => ob != null ? ob.Value : default;

    /// <summary>
    /// 返回表示当前对象的字符串
    /// </summary>
    public override string ToString()
    {
        return _value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 设置值但不触发事件
    /// </summary>
    public void SetValueWithoutNotify(T value)
    {
        _value = value;
    }
}