using UnityEngine;
using UnityEngine.UI;
using System;
using Ddz.Define;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ddz.BoloExtensions.Component
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    [AddComponentMenu("BoloComponent/LocalizationText")]
    public class LocalizationText : LocalizationBase
    {
        [Header("多语言设置")]
        [Tooltip("设置key后需手动点击下方按钮更新多语言值\n若key为空，则会保持当前多语言值不变，可用于参数格式化配置")]
        [SerializeField]
        private string strKey = null;
        public string GetKey => strKey;

        [Header("当前语言")]
        [Tooltip("设置语言后需手动点击下方按钮更新多语言值")]
        [SerializeField]
        private eLanguageType language = eLanguageType.Cn;
        public eLanguageType GetLanguage => language;

        [Header("多语言值")]
        [Tooltip("这是多语言Key所对应的值，Text组件的文本仅由该值直接控制")]
        [TextArea(3, 10)]
        [SerializeField]
        private string languageValue = "";
        public string GetLanguageValue => languageValue;

        [Header("多语言值参数")]
        [Tooltip("这是为格式化多语言值提供编辑器参数的地方，支持智能解析为数字、布尔值、日期")]
        [SerializeField]
        private string[] _strParameters = Array.Empty<string>();

        private Text _textComponent;
        private object[] _runtimeParameters = Array.Empty<object>();

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <param name="index"></param>
        public object GetParameter(int index)
        {
            return _runtimeParameters[index];
        }

        /// <summary>
        /// 设置运行时的格式化参数（这是最常用的方法，注意会覆盖编辑器中设置的所有参数值）
        /// </summary>
        public void SetParameters(params object[] args)
        {
            _runtimeParameters = args ?? Array.Empty<object>();
#if UNITY_EDITOR
            //目的是运行时将参数显示在编辑器中
            _strParameters = new string[_runtimeParameters.Length];
            for (int i = 0; i < _runtimeParameters.Length; i++)
            {
                _strParameters[i] = _runtimeParameters[i].ToString();
            }
#endif
            RefreshText();
        }

        /// <summary>
        /// 临时修改多语言值，修改语言或key时会被覆盖，通常仅为调试用
        /// </summary>
        /// <param name="value"></param>
        public void SetLanguageValue(string value)
        {
            languageValue = value;
            RefreshText();
        }

        /// <summary>
        /// 设置多语言key，若为空则保留使用当前的多语言值
        /// </summary>
        /// <param name="key"></param>
        public void SetLanguageKey(string key)
        {
            strKey = key;
            RefreshView();
        }

        /// <summary>
        /// 运行时语言变化时触发，修改语言和多语言值
        /// </summary>
        protected override void RefreshView()
        {
            base.RefreshView();

            language = GameGlobal.MultilingualMgr.LanguageType;
            if (string.IsNullOrEmpty(strKey) == false) //这里空key时不对多语言值赋值，目的是保留多语言值的自定义能力
            {
                languageValue = GameGlobal.MultilingualMgr.Txt(strKey);
            }
            RefreshText();
        }

        /// <summary>
        /// 刷新文本显示
        /// </summary>
        void RefreshText()
        {
            if (_textComponent == null)
            {
                _textComponent = GetComponent<Text>();
                if (_textComponent == null) return;
            }

            // 应用参数格式化
            string textContent = ApplyTextFormatting(languageValue);

            _textComponent.text = textContent;
        }

        /// <summary>
        /// 应用文本格式化处理
        /// </summary>
        private string ApplyTextFormatting(string text)
        {
            // 确定要使用的参数
            object[] parameters = _runtimeParameters.Length > 0 ?
                _runtimeParameters : ParseStringParameters(_strParameters);

            // 应用参数格式化
            if (parameters.Length > 0 && !string.IsNullOrEmpty(text))
            {
                try
                {
                    text = string.Format(text, parameters);
                }
                catch (FormatException)
                {
                    text = $"[FormatError] {text}";
                }
            }

            return text;
        }

        /// <summary>
        /// 解析字符串参数为对象数组
        /// </summary>
        private object[] ParseStringParameters(string[] stringParams)
        {
            if (stringParams == null || stringParams.Length == 0)
                return Array.Empty<object>();

            object[] result = new object[stringParams.Length];
            for (int i = 0; i < stringParams.Length; i++)
            {
                result[i] = ParseParameter(stringParams[i]);
            }
            return result;
        }

        /// <summary>
        /// 智能参数解析（支持数字、布尔值、日期和字符串）
        /// 解析优先级：数字 > 布尔值 > 日期 > 字符串
        /// 
        /// 示例解析结果：
        ///   "123,456"     → long 类型 (123456)
        ///   "3.14"        → decimal 类型 (3.14)
        ///   "true"        → bool 类型 (true)
        ///   "2023-08-05"  → DateTime 类型 (2023年8月5日)
        ///   "05/08/2023"  → DateTime 类型 (2023年5月8日 或 2023年8月5日，取决于系统区域设置)
        ///   "hello"       → string 类型 ("hello")
        ///   
        /// 日期解析说明：
        ///   1. 支持ISO8601标准格式（如 "2023-08-05"、"20230805T143000Z"）
        ///   2. 支持本地化格式（如 "05/08/2023"、"August 5, 2023"）
        ///   3. 支持带时间的日期（如 "2023-08-05 14:30:00"）
        ///   4. 使用当前线程的区域设置进行解析
        ///   5. 如果日期字符串包含非数字字符（如连字符/斜杠），会被正确识别为日期
        /// </summary>
        private object ParseParameter(string param)
        {
            if (string.IsNullOrEmpty(param)) return param;

            // 尝试解析为数字（支持逗号分隔）
            if (IsNumericString(param))
            {
                string cleanNumber = param.Replace(",", "");
                if (long.TryParse(cleanNumber, out long longValue)) return longValue;
                if (decimal.TryParse(cleanNumber, out decimal decimalValue)) return decimalValue;
            }

            // 尝试解析为布尔值（支持 "true"/"false" 不区分大小写）
            if (bool.TryParse(param, out bool boolValue)) return boolValue;

            // 尝试解析为日期（支持多种常见格式）
            // 使用当前线程的区域设置（CultureInfo.CurrentCulture）
            // DateTimeStyles.None 表示使用默认解析选项
            if (DateTime.TryParse(param, null, System.Globalization.DateTimeStyles.None, out DateTime dateValue))
                return dateValue;

            // 无法识别为数字/布尔值/日期时，返回原始字符串
            return param;
        }

        /// <summary>
        /// 检测是否是数字字符串（支持逗号分隔）
        /// 
        /// 优化后的规则：
        ///   1. 只允许数字(0-9)、逗号(,)、负号(-)和小数点(.)
        ///   2. 负号只能出现在字符串开头
        ///   3. 小数点最多只能出现一次
        ///   4. 必须包含至少一个数字
        /// 
        /// 示例：
        ///   "123,456"     → true
        ///   "-3.14"       → true
        ///   ".5"          → true (表示0.5)
        ///   "1,000.00"    → true
        ///   "2023-08-05"  → false (包含连字符但非开头位置)
        ///   "12..34"      → false (多个小数点)
        ///   "--123"       → false (多个负号)
        ///   "abc"         → false (包含字母)
        ///   "."           → false (无数字)
        ///   "-"           → false (无数字)
        /// </summary>
        private bool IsNumericString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            bool hasDigit = false;      // 是否包含数字
            bool hasDecimal = false;    // 是否已包含小数点
            bool hasMinus = false;      // 是否已包含负号

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (char.IsDigit(c))
                {
                    hasDigit = true;  // 标记找到至少一个数字
                    continue;
                }

                switch (c)
                {
                    case ',':
                        // 逗号允许在任何位置（千位分隔符）
                        break;

                    case '-':
                        // 负号只能出现在字符串开头
                        if (i != 0 || hasMinus)
                            return false;
                        hasMinus = true;
                        break;

                    case '.':
                        // 小数点最多只能有一个
                        if (hasDecimal)
                            return false;
                        hasDecimal = true;
                        break;

                    default:
                        // 其他字符一律无效
                        return false;
                }
            }

            // 必须包含至少一个数字
            return hasDigit;
        }

        private void OnValidate()
        {
            RefreshText();
        }

        /// <summary>
        /// 恢复默认多语言值
        /// </summary>
        public void RestoreDefaultValue()
        {
            languageValue = MultilingualMgr.EditorPreviewTxt(language, strKey);
            RefreshText();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(LocalizationText))]
    public class LocalizationTextEditor : Editor
    {
        private const string ButtonControlName = "restoreButton";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            LocalizationText component = (LocalizationText)target;

            EditorGUILayout.Space();
            GUI.SetNextControlName(ButtonControlName);

            if (string.IsNullOrEmpty(component.GetKey))
            {
                GUILayout.Box("由于key为空，当前多语言值不会随任何语言设置而改变，此时可当作字符串格式化工具使用");
            }
            else if (GUILayout.Button("根据 Key 和 Language 重置多语言值"))
            {
                //注册撤销点
                Undo.RecordObject(component, "Restore Localization Value");

                //获取当前值用于可能的撤销操作
                string languageValue = component.GetLanguageValue;

                //执行重置操作
                component.RestoreDefaultValue();

                //检查值是否实际发生变化
                if (component.GetLanguageValue != languageValue)
                {
                    //标记场景为已修改
                    EditorUtility.SetDirty(component);

                    //点击后将焦点设置到按钮上以便观察到多语言值文本框内容的变化
                    GUI.FocusControl(ButtonControlName);

                    //刷新场景视图以便实时观察变化
                    //SceneView.RepaintAll();
                }
            }
        }
    }
#endif
}