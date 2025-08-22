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
        [Header("����������")]
        [Tooltip("����key�����ֶ�����·���ť���¶�����ֵ\n��keyΪ�գ���ᱣ�ֵ�ǰ������ֵ���䣬�����ڲ�����ʽ������")]
        [SerializeField]
        private string strKey = null;
        public string GetKey => strKey;

        [Header("��ǰ����")]
        [Tooltip("�������Ժ����ֶ�����·���ť���¶�����ֵ")]
        [SerializeField]
        private eLanguageType language = eLanguageType.Cn;
        public eLanguageType GetLanguage => language;

        [Header("������ֵ")]
        [Tooltip("���Ƕ�����Key����Ӧ��ֵ��Text������ı����ɸ�ֱֵ�ӿ���")]
        [TextArea(3, 10)]
        [SerializeField]
        private string languageValue = "";
        public string GetLanguageValue => languageValue;

        [Header("������ֵ����")]
        [Tooltip("����Ϊ��ʽ��������ֵ�ṩ�༭�������ĵط���֧�����ܽ���Ϊ���֡�����ֵ������")]
        [SerializeField]
        private string[] _strParameters = Array.Empty<string>();

        private Text _textComponent;
        private object[] _runtimeParameters = Array.Empty<object>();

        /// <summary>
        /// ��ȡ����
        /// </summary>
        /// <param name="index"></param>
        public object GetParameter(int index)
        {
            return _runtimeParameters[index];
        }

        /// <summary>
        /// ��������ʱ�ĸ�ʽ��������������õķ�����ע��Ḳ�Ǳ༭�������õ����в���ֵ��
        /// </summary>
        public void SetParameters(params object[] args)
        {
            _runtimeParameters = args ?? Array.Empty<object>();
#if UNITY_EDITOR
            //Ŀ��������ʱ��������ʾ�ڱ༭����
            _strParameters = new string[_runtimeParameters.Length];
            for (int i = 0; i < _runtimeParameters.Length; i++)
            {
                _strParameters[i] = _runtimeParameters[i].ToString();
            }
#endif
            RefreshText();
        }

        /// <summary>
        /// ��ʱ�޸Ķ�����ֵ���޸����Ի�keyʱ�ᱻ���ǣ�ͨ����Ϊ������
        /// </summary>
        /// <param name="value"></param>
        public void SetLanguageValue(string value)
        {
            languageValue = value;
            RefreshText();
        }

        /// <summary>
        /// ���ö�����key����Ϊ������ʹ�õ�ǰ�Ķ�����ֵ
        /// </summary>
        /// <param name="key"></param>
        public void SetLanguageKey(string key)
        {
            strKey = key;
            RefreshView();
        }

        /// <summary>
        /// ����ʱ���Ա仯ʱ�������޸����ԺͶ�����ֵ
        /// </summary>
        protected override void RefreshView()
        {
            base.RefreshView();

            language = GameGlobal.MultilingualMgr.LanguageType;
            if (string.IsNullOrEmpty(strKey) == false) //�����keyʱ���Զ�����ֵ��ֵ��Ŀ���Ǳ���������ֵ���Զ�������
            {
                languageValue = GameGlobal.MultilingualMgr.Txt(strKey);
            }
            RefreshText();
        }

        /// <summary>
        /// ˢ���ı���ʾ
        /// </summary>
        void RefreshText()
        {
            if (_textComponent == null)
            {
                _textComponent = GetComponent<Text>();
                if (_textComponent == null) return;
            }

            // Ӧ�ò�����ʽ��
            string textContent = ApplyTextFormatting(languageValue);

            _textComponent.text = textContent;
        }

        /// <summary>
        /// Ӧ���ı���ʽ������
        /// </summary>
        private string ApplyTextFormatting(string text)
        {
            // ȷ��Ҫʹ�õĲ���
            object[] parameters = _runtimeParameters.Length > 0 ?
                _runtimeParameters : ParseStringParameters(_strParameters);

            // Ӧ�ò�����ʽ��
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
        /// �����ַ�������Ϊ��������
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
        /// ���ܲ���������֧�����֡�����ֵ�����ں��ַ�����
        /// �������ȼ������� > ����ֵ > ���� > �ַ���
        /// 
        /// ʾ�����������
        ///   "123,456"     �� long ���� (123456)
        ///   "3.14"        �� decimal ���� (3.14)
        ///   "true"        �� bool ���� (true)
        ///   "2023-08-05"  �� DateTime ���� (2023��8��5��)
        ///   "05/08/2023"  �� DateTime ���� (2023��5��8�� �� 2023��8��5�գ�ȡ����ϵͳ��������)
        ///   "hello"       �� string ���� ("hello")
        ///   
        /// ���ڽ���˵����
        ///   1. ֧��ISO8601��׼��ʽ���� "2023-08-05"��"20230805T143000Z"��
        ///   2. ֧�ֱ��ػ���ʽ���� "05/08/2023"��"August 5, 2023"��
        ///   3. ֧�ִ�ʱ������ڣ��� "2023-08-05 14:30:00"��
        ///   4. ʹ�õ�ǰ�̵߳��������ý��н���
        ///   5. ��������ַ��������������ַ��������ַ�/б�ܣ����ᱻ��ȷʶ��Ϊ����
        /// </summary>
        private object ParseParameter(string param)
        {
            if (string.IsNullOrEmpty(param)) return param;

            // ���Խ���Ϊ���֣�֧�ֶ��ŷָ���
            if (IsNumericString(param))
            {
                string cleanNumber = param.Replace(",", "");
                if (long.TryParse(cleanNumber, out long longValue)) return longValue;
                if (decimal.TryParse(cleanNumber, out decimal decimalValue)) return decimalValue;
            }

            // ���Խ���Ϊ����ֵ��֧�� "true"/"false" �����ִ�Сд��
            if (bool.TryParse(param, out bool boolValue)) return boolValue;

            // ���Խ���Ϊ���ڣ�֧�ֶ��ֳ�����ʽ��
            // ʹ�õ�ǰ�̵߳��������ã�CultureInfo.CurrentCulture��
            // DateTimeStyles.None ��ʾʹ��Ĭ�Ͻ���ѡ��
            if (DateTime.TryParse(param, null, System.Globalization.DateTimeStyles.None, out DateTime dateValue))
                return dateValue;

            // �޷�ʶ��Ϊ����/����ֵ/����ʱ������ԭʼ�ַ���
            return param;
        }

        /// <summary>
        /// ����Ƿ��������ַ�����֧�ֶ��ŷָ���
        /// 
        /// �Ż���Ĺ���
        ///   1. ֻ��������(0-9)������(,)������(-)��С����(.)
        ///   2. ����ֻ�ܳ������ַ�����ͷ
        ///   3. С�������ֻ�ܳ���һ��
        ///   4. �����������һ������
        /// 
        /// ʾ����
        ///   "123,456"     �� true
        ///   "-3.14"       �� true
        ///   ".5"          �� true (��ʾ0.5)
        ///   "1,000.00"    �� true
        ///   "2023-08-05"  �� false (�������ַ����ǿ�ͷλ��)
        ///   "12..34"      �� false (���С����)
        ///   "--123"       �� false (�������)
        ///   "abc"         �� false (������ĸ)
        ///   "."           �� false (������)
        ///   "-"           �� false (������)
        /// </summary>
        private bool IsNumericString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            bool hasDigit = false;      // �Ƿ��������
            bool hasDecimal = false;    // �Ƿ��Ѱ���С����
            bool hasMinus = false;      // �Ƿ��Ѱ�������

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (char.IsDigit(c))
                {
                    hasDigit = true;  // ����ҵ�����һ������
                    continue;
                }

                switch (c)
                {
                    case ',':
                        // �����������κ�λ�ã�ǧλ�ָ�����
                        break;

                    case '-':
                        // ����ֻ�ܳ������ַ�����ͷ
                        if (i != 0 || hasMinus)
                            return false;
                        hasMinus = true;
                        break;

                    case '.':
                        // С�������ֻ����һ��
                        if (hasDecimal)
                            return false;
                        hasDecimal = true;
                        break;

                    default:
                        // �����ַ�һ����Ч
                        return false;
                }
            }

            // �����������һ������
            return hasDigit;
        }

        private void OnValidate()
        {
            RefreshText();
        }

        /// <summary>
        /// �ָ�Ĭ�϶�����ֵ
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
                GUILayout.Box("����keyΪ�գ���ǰ������ֵ�������κ��������ö��ı䣬��ʱ�ɵ����ַ�����ʽ������ʹ��");
            }
            else if (GUILayout.Button("���� Key �� Language ���ö�����ֵ"))
            {
                //ע�᳷����
                Undo.RecordObject(component, "Restore Localization Value");

                //��ȡ��ǰֵ���ڿ��ܵĳ�������
                string languageValue = component.GetLanguageValue;

                //ִ�����ò���
                component.RestoreDefaultValue();

                //���ֵ�Ƿ�ʵ�ʷ����仯
                if (component.GetLanguageValue != languageValue)
                {
                    //��ǳ���Ϊ���޸�
                    EditorUtility.SetDirty(component);

                    //����󽫽������õ���ť���Ա�۲쵽������ֵ�ı������ݵı仯
                    GUI.FocusControl(ButtonControlName);

                    //ˢ�³�����ͼ�Ա�ʵʱ�۲�仯
                    //SceneView.RepaintAll();
                }
            }
        }
    }
#endif
}