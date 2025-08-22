#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

[CustomPropertyDrawer(typeof(Ob<>), true)]
public class ObPropertyDrawer : PropertyDrawer
{
    // 存储每个属性的展开状态
    private static readonly System.Collections.Generic.Dictionary<string, bool> expandStates =
        new System.Collections.Generic.Dictionary<string, bool>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 获取唯一键来标识这个属性
        string key = $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";

        // 获取或初始化展开状态
        if (!expandStates.ContainsKey(key))
        {
            expandStates[key] = false;
        }
        bool showEvent = expandStates[key];

        // 获取值属性
        SerializedProperty valueProperty = property.FindPropertyRelative("_value");

        // 获取 OnValueChanged 事件属性
        SerializedProperty eventProperty = property.FindPropertyRelative("OnValueChanged");

        // 计算字段高度
        float fieldHeight = EditorGUI.GetPropertyHeight(valueProperty, label, true);

        // 创建值字段的矩形
        Rect valueRect = new Rect(position.x, position.y, position.width - 20, fieldHeight);

        // 创建勾选框的矩形
        Rect toggleRect = new Rect(position.x + position.width - 18, position.y, 16, fieldHeight);

        // 获取 Ob<T> 对象实例
        object obInstance = GetTargetObjectOfProperty(property);

        if (obInstance != null)
        {
            // 获取 _value 字段信息
            FieldInfo valueField = obInstance.GetType().GetField("_value",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // 在修改前获取当前值作为旧值
            object oldValue = valueField?.GetValue(obInstance);

            // 绘制值属性字段
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(valueRect, valueProperty, label, true);

            // 绘制勾选框
            bool newShowEvent = EditorGUI.Toggle(toggleRect, showEvent);
            if (newShowEvent != showEvent)
            {
                expandStates[key] = newShowEvent;
                showEvent = newShowEvent;
            }

            // 检查值是否发生变化
            if (EditorGUI.EndChangeCheck())
            {
                // 应用修改
                valueProperty.serializedObject.ApplyModifiedProperties();

                if (valueField != null)
                {
                    // 获取新值
                    object newValue = valueField.GetValue(obInstance);

                    // 通过反射获取 OnValueChanged 字段
                    FieldInfo onValueChangedField = obInstance.GetType().GetField("OnValueChanged",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (onValueChangedField != null)
                    {
                        // 获取 OnValueChanged 的值（UnityEvent<T, T>）
                        object unityEvent = onValueChangedField.GetValue(obInstance);

                        // 通过反射调用 Invoke 方法，传入旧值和新值
                        unityEvent?.GetType().GetMethod("Invoke")?
                            .Invoke(unityEvent, new[] { oldValue, newValue });
                    }
                }
            }

            // 如果勾选，显示事件属性
            if (showEvent && eventProperty != null)
            {
                Rect eventRect = new Rect(
                    position.x,
                    position.y + fieldHeight + 2,
                    position.width,
                    EditorGUI.GetPropertyHeight(eventProperty, true)
                );

                EditorGUI.PropertyField(eventRect, eventProperty, true);
            }
        }
        else
        {
            // 如果无法获取 Ob<T> 实例，则使用默认绘制
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 获取唯一键
        string key = $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";

        // 获取展开状态
        bool showEvent = expandStates.ContainsKey(key) && expandStates[key];

        // 获取值属性高度
        SerializedProperty valueProperty = property.FindPropertyRelative("_value");
        float height = EditorGUI.GetPropertyHeight(valueProperty, label, true);

        // 如果展开，添加事件属性高度
        if (showEvent)
        {
            SerializedProperty eventProperty = property.FindPropertyRelative("OnValueChanged");
            if (eventProperty != null)
            {
                height += EditorGUI.GetPropertyHeight(eventProperty, true) + 2;
            }
        }

        return height;
    }

    // 辅助方法：从SerializedProperty获取目标对象
    private object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        var path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');

        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = int.Parse(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue(obj, elementName, index);
            }
            else
            {
                obj = GetValue(obj, element);
            }
        }
        return obj;
    }

    private object GetValue(object source, string name)
    {
        if (source == null) return null;
        var type = source.GetType();

        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(source);

            var property = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null) return property.GetValue(source, null);

            type = type.BaseType;
        }
        return null;
    }

    private object GetValue(object source, string name, int index)
    {
        var enumerable = GetValue(source, name) as System.Collections.IEnumerable;
        if (enumerable == null) return null;

        var enm = enumerable.GetEnumerator();
        for (int i = 0; i <= index; i++)
        {
            if (!enm.MoveNext()) return null;
        }
        return enm.Current;
    }
}
#endif