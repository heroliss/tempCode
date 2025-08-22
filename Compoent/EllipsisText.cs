using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.EventSystems;

[ExecuteAlways]
public class EllipsisText : Text, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum EllipsisMode
    {
        MaxCharacters,
        VisibleArea,
        MaxWidth
    }

    [SerializeField]
    private EllipsisMode _mode = EllipsisMode.MaxCharacters;
    public EllipsisMode mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                MarkDirty();
            }
        }
    }

    [SerializeField]
    private string _ellipsisSymbol = "...";
    public string ellipsisSymbol
    {
        get => _ellipsisSymbol;
        set
        {
            if (_ellipsisSymbol != value)
            {
                _ellipsisSymbol = value;
                MarkDirty();
            }
        }
    }

    [SerializeField]
    private int _maxCharacters = 20;
    public int maxCharacters
    {
        get => _maxCharacters;
        set
        {
            if (_maxCharacters != value)
            {
                _maxCharacters = value;
                MarkDirty();
            }
        }
    }

    [SerializeField]
    private float _maxWidth = 200;
    public float maxWidth
    {
        get => _maxWidth;
        set
        {
            if (_maxWidth != value)
            {
                _maxWidth = value;
                MarkDirty();
            }
        }
    }

    [SerializeField]
    [TextArea(3, 10)]
    private string _originalText = "";
    public string originalText
    {
        get => _originalText;
        set
        {
            if (_originalText != value)
            {
                _originalText = value;
                MarkDirty();
                UpdateEllipsis();
            }
        }
    }

    // 尺寸变化检测阈值
    private const float SIZE_CHANGE_THRESHOLD = 0.1f;

    private bool _isDirty = true;
    private TextGenerator _textGenerator;
    private TextGenerationSettings _generationSettings;
    private Vector2 _lastSize = Vector2.zero;
    private Vector2 _lastMaxSize = Vector2.zero;
    private HorizontalWrapMode _originalHorizontalOverflow;
    private VerticalWrapMode _originalVerticalOverflow;
    private bool _isDragging = false;

    // 调试信息
    [System.NonSerialized] public Vector2 debugCurrentSize = Vector2.zero;
    [System.NonSerialized] public int debugLineCount = 0;
    [System.NonSerialized] public int debugMaxLines = 0;
    [System.NonSerialized] public float debugLineHeight = 0;
    [System.NonSerialized] public float debugTextHeight = 0;

    protected override void Awake()
    {
        base.Awake();
        _textGenerator = new TextGenerator();

        if (string.IsNullOrEmpty(_originalText))
        {
            _originalText = base.text;
        }

        _originalHorizontalOverflow = horizontalOverflow;
        _originalVerticalOverflow = verticalOverflow;

        MarkDirty();
        UpdateEllipsis();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.willRenderCanvases += OnCanvasRender;
        MarkDirty();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Canvas.willRenderCanvases -= OnCanvasRender;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !_isDragging)
        {
            Vector2 currentSize = rectTransform.rect.size;

            // 使用阈值检测尺寸变化
            if (Vector2.Distance(_lastSize, currentSize) > SIZE_CHANGE_THRESHOLD)
            {
                MarkDirty();
                UpdateEllipsis();
                _lastSize = currentSize;
            }
        }
#endif
    }

    private void OnCanvasRender()
    {
        Vector2 currentSize = rectTransform.rect.size;
        debugCurrentSize = currentSize;

        // 使用阈值检测尺寸变化
        if (_isDirty || rectTransform.hasChanged ||
            Vector2.Distance(_lastSize, currentSize) > SIZE_CHANGE_THRESHOLD)
        {
            UpdateEllipsis();
            rectTransform.hasChanged = false;
            _lastSize = currentSize;
        }
    }

    public void OnBeginDrag(PointerEventData eventData) => _isDragging = true;
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) => _isDragging = false;

    public new string text
    {
        get => _originalText;
        set
        {
            if (_originalText != value)
            {
                _originalText = value;
                MarkDirty();
                UpdateEllipsis();
            }
        }
    }

    private void SetDisplayText(string displayText)
    {
        if (base.text == displayText) return;

        base.text = displayText;
        SetVerticesDirty();
        SetLayoutDirty();
    }

    private void MarkDirty() => _isDirty = true;

    private void UpdateEllipsis()
    {
        if (rectTransform.rect.width <= 0 || rectTransform.rect.height <= 0)
            return;

        if (string.IsNullOrEmpty(_originalText))
        {
            SetDisplayText("");
            return;
        }

        if (!_isDirty) return;
        _isDirty = false;

        // 保存原始设置
        var origHorizontal = horizontalOverflow;
        var origVertical = verticalOverflow;

        if ((_mode == EllipsisMode.VisibleArea || _mode == EllipsisMode.MaxWidth) &&
            (origHorizontal == HorizontalWrapMode.Overflow || origVertical == VerticalWrapMode.Overflow))
        {
            SetDisplayText(_originalText);
            return;
        }

        // 根据模式应用临时设置
        if (_mode != EllipsisMode.MaxCharacters)
        {
            horizontalOverflow = HorizontalWrapMode.Wrap;
            verticalOverflow = VerticalWrapMode.Truncate;
        }

        // 更新文本布局
        SetVerticesDirty();
        SetLayoutDirty();
        Canvas.ForceUpdateCanvases();

        // 应用省略号处理
        switch (_mode)
        {
            case EllipsisMode.MaxCharacters:
                ApplyMaxCharacters();
                break;
            case EllipsisMode.VisibleArea:
                ApplyVisibleArea();
                break;
            case EllipsisMode.MaxWidth:
                ApplyMaxWidth();
                break;
        }

        // 恢复原始设置
        horizontalOverflow = origHorizontal;
        verticalOverflow = origVertical;
    }

    private void ApplyMaxCharacters()
    {
        if (_originalText.Length <= _maxCharacters)
        {
            SetDisplayText(_originalText);
            return;
        }

        SetDisplayText(_originalText.Substring(0, _maxCharacters) + _ellipsisSymbol);
    }

    public void ApplyVisibleArea()
    {
        // 确保使用Wrap和Truncate模式
        horizontalOverflow = HorizontalWrapMode.Wrap;
        verticalOverflow = VerticalWrapMode.Truncate;

        // 强制更新布局以确保TextGenerator使用正确的设置
        SetVerticesDirty();
        SetLayoutDirty();
        Canvas.ForceUpdateCanvases();

        // 获取文本生成设置 - 使用实际尺寸
        Vector2 textAreaSize = rectTransform.rect.size;
        _generationSettings = GetGenerationSettings(textAreaSize);

        // 生成文本并获取行信息
        _textGenerator.Populate(_originalText, _generationSettings);
        IList<UILineInfo> lines = _textGenerator.lines;
        debugLineCount = lines.Count;

        if (lines.Count == 0)
        {
            SetDisplayText(_originalText);
            return;
        }

        // 计算实际文本高度
        debugTextHeight = _textGenerator.GetPreferredHeight(_originalText, _generationSettings);
        debugLineHeight = CalculateActualLineHeight();
        float lineHeight = debugLineHeight;

        // 计算最大行数 - 使用更精确的方法
        float availableHeight = textAreaSize.y;
        debugMaxLines = CalculateMaxLines(availableHeight, lineHeight, lines);

        if (debugMaxLines <= 0)
        {
            SetDisplayText(_ellipsisSymbol);
            return;
        }

        // 如果文本行数小于等于最大行数，直接显示完整文本
        if (lines.Count <= debugMaxLines)
        {
            SetDisplayText(_originalText);
            return;
        }

        // 截取前maxLines行
        int endIndex = lines[debugMaxLines].startCharIdx;
        string truncatedText = _originalText.Substring(0, endIndex);

        // 在最后一行添加省略号
        int lastLineStart = lines[debugMaxLines - 1].startCharIdx;
        int lastLineLength = endIndex - lastLineStart;
        string lastLine = truncatedText.Substring(lastLineStart, lastLineLength);

        // 添加省略号后的最终文本
        SetDisplayText(truncatedText.Substring(0, lastLineStart) +
                      TruncateLineWithEllipsis(lastLine, textAreaSize.x));
    }

    /// <summary>
    /// 修复行数计算逻辑 - 恢复原始实现
    /// </summary>
    private int CalculateMaxLines(float availableHeight, float lineHeight, IList<UILineInfo> lines)
    {
        // 计算实际内容高度（包括行间距）
        float contentHeight = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            contentHeight += lines[i].height;
        }

        // 计算最大行数（考虑行间距）
        int maxLines = Mathf.FloorToInt(availableHeight / lineHeight);

        // 如果内容高度小于可用高度，返回所有行
        if (contentHeight <= availableHeight)
        {
            return lines.Count;
        }

        // 否则计算实际可显示的行数
        float accumulatedHeight = 0;
        int actualMaxLines = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            accumulatedHeight += lines[i].height;
            if (accumulatedHeight > availableHeight)
            {
                break;
            }
            actualMaxLines++;
        }

        return actualMaxLines > 0 ? actualMaxLines : 0;
    }

    /// <summary>
    /// 修复行高计算逻辑 - 恢复原始实现
    /// </summary>
    private float CalculateActualLineHeight()
    {
        // 使用更可靠的方式计算行高
        if (_textGenerator.lineCount > 0 && _textGenerator.lines.Count > 0)
        {
            // 取第一行的高度
            return _textGenerator.lines[0].height;
        }

        // 回退到使用字体大小计算
        float lineHeight = fontSize * lineSpacing;
        return lineHeight > 0 ? lineHeight : fontSize * 1.2f;
    }

    private void ApplyMaxWidth()
    {
        SetDisplayText(ProcessTextForWidth(_originalText, _maxWidth));
    }

    private string ProcessTextForWidth(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;

        _generationSettings = GetGenerationSettings(new Vector2(maxWidth, float.PositiveInfinity));
        _textGenerator.Populate(text, _generationSettings);
        var lines = _textGenerator.lines;

        if (lines.Count == 0) return text;

        StringBuilder result = new StringBuilder();
        float totalWidth = 0f;

        for (int i = 0; i < lines.Count; i++)
        {
            int startIndex = lines[i].startCharIdx;
            int endIndex = (i < lines.Count - 1) ? lines[i + 1].startCharIdx : text.Length;
            string lineText = text.Substring(startIndex, endIndex - startIndex);

            float lineWidth = GetTextWidth(lineText);
            totalWidth += lineWidth;

            if (totalWidth > maxWidth)
            {
                result.Append(TruncateLineWithEllipsis(lineText, maxWidth - (totalWidth - lineWidth)));
                break;
            }

            result.Append(lineText);
        }

        return result.ToString();
    }

    /// <summary>
    /// 截断单行文本并添加省略号
    /// </summary>
    private string TruncateLineWithEllipsis(string line, float availableWidth)
    {
        float ellipsisWidth = GetTextWidth(_ellipsisSymbol);
        float usableWidth = Mathf.Max(0, availableWidth - ellipsisWidth);

        int low = 0;
        int high = line.Length;
        int bestIndex = 0;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            string testText = line.Substring(0, mid);
            float testWidth = GetTextWidth(testText);

            if (testWidth <= usableWidth)
            {
                bestIndex = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return line.Substring(0, bestIndex) + _ellipsisSymbol;
    }

    private float GetTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return _textGenerator.GetPreferredWidth(text, _generationSettings);
    }

    public void Refresh()
    {
        MarkDirty();
        UpdateEllipsis();
    }
    
#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        MarkDirty();

        if (isActiveAndEnabled)
        {
            UpdateEllipsis();
        }
    }
#endif

}

#if UNITY_EDITOR
[CustomEditor(typeof(EllipsisText))]
public class EllipsisTextEditor : Editor
{
    private bool _showDebugInfo = false;
    private bool _showVisibleAreaDebug = false;

    private static readonly string[] excludedProperties =
    {
        "m_Script", "_mode", "_ellipsisSymbol",
        "_maxCharacters", "_maxWidth", "_originalText"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 绘制Text组件原始属性
        DrawPropertiesExcluding(serializedObject, excludedProperties);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ellipsis Settings", EditorStyles.boldLabel);

        // 绘制自定义属性
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_mode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_ellipsisSymbol"));

        var mode = (EllipsisText.EllipsisMode)serializedObject
            .FindProperty("_mode").enumValueIndex;

        if (mode == EllipsisText.EllipsisMode.MaxCharacters)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxCharacters"));
        }
        else if (mode == EllipsisText.EllipsisMode.MaxWidth)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxWidth"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("_originalText"));

        serializedObject.ApplyModifiedProperties();

        EllipsisText component = (EllipsisText)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Text"))
        {
            component.Refresh();
        }

        // 警告信息
        if (component.mode != EllipsisText.EllipsisMode.MaxCharacters &&
            (component.horizontalOverflow == HorizontalWrapMode.Overflow ||
             component.verticalOverflow == VerticalWrapMode.Overflow))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "VisibleArea and MaxWidth modes require Wrap/Truncate overflow settings. " +
                "Current settings may prevent these modes from working correctly.",
                MessageType.Warning
            );
        }

        // 调试信息分组
        EditorGUILayout.Space();
        _showDebugInfo = EditorGUILayout.Foldout(_showDebugInfo, "Debug Information", true);
        if (_showDebugInfo)
        {
            EditorGUI.indentLevel++;

            // 当前状态
            EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Content Size: {component.debugCurrentSize}");

            // 溢出设置
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Overflow Settings:");
            EditorGUILayout.LabelField($"Horizontal: {component.horizontalOverflow}");
            EditorGUILayout.LabelField($"Vertical: {component.verticalOverflow}");

            // 模式特定调试
            if (component.mode == EllipsisText.EllipsisMode.VisibleArea)
            {
                EditorGUILayout.Space();
                _showVisibleAreaDebug = EditorGUILayout.Foldout(_showVisibleAreaDebug, "VisibleArea Debug", true);
                if (_showVisibleAreaDebug)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Line Count: {component.debugLineCount}");
                    EditorGUILayout.LabelField($"Max Lines: {component.debugMaxLines}");
                    EditorGUILayout.LabelField($"Line Height: {component.debugLineHeight}");
                    EditorGUILayout.LabelField($"Text Height: {component.debugTextHeight}");

                    if (GUILayout.Button("Run VisibleArea Calculation"))
                    {
                        component.ApplyVisibleArea();
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
#endif