using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.EventSystems;

/// <summary>
/// 省略号文本修改器组件，与Text组件平行工作
/// 功能：自动获取同一节点上的Text组件并修改其值，提供多种省略模式
/// 特性：支持实时刷新选项，提供手动刷新方法，支持撤销操作
/// </summary>
[ExecuteAlways] // 在编辑模式下执行
[RequireComponent(typeof(Text))] // 要求节点上必须有Text组件
public class EllipsisTextModifier : MonoBehaviour
{
    /// <summary>
    /// 省略模式枚举
    /// </summary>
    public enum EllipsisMode
    {
        MaxCharacters,  // 最大字符数模式
        VisibleArea,    // 可视区域模式（默认）
        MaxWidth        // 最大宽度模式
    }

    [Tooltip("是否实时刷新文本（尺寸变化或设置变化时）")]
    public bool autoRefresh = true; // 实时刷新开关

    [Tooltip("是否考虑空格，按照英文单词截断")]
    public bool considerSpaces = false; // 新增：考虑空格的选项

    [SerializeField]
    private EllipsisMode _mode = EllipsisMode.VisibleArea; // 省略模式，默认为VisibleArea
    /// <summary>
    /// 获取或设置省略模式
    /// </summary>
    public EllipsisMode mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                MarkDirty(); // 标记需要刷新
            }
        }
    }

    [SerializeField]
    private string _ellipsisSymbol = "..."; // 省略符号
    /// <summary>
    /// 获取或设置省略符号
    /// </summary>
    public string ellipsisSymbol
    {
        get => _ellipsisSymbol;
        set
        {
            if (_ellipsisSymbol != value)
            {
                _ellipsisSymbol = value;
                MarkDirty(); // 标记需要刷新
            }
        }
    }

    [SerializeField]
    private int _maxCharacters = 10; // 最大字符数（用于MaxCharacters模式）
    /// <summary>
    /// 获取或设置最大字符数
    /// </summary>
    public int maxCharacters
    {
        get => _maxCharacters;
        set
        {
            if (_maxCharacters != value)
            {
                _maxCharacters = value;
                MarkDirty(); // 标记需要刷新
            }
        }
    }

    [SerializeField]
    private float _maxWidth = 100; // 最大宽度（用于MaxWidth模式）
    /// <summary>
    /// 获取或设置最大宽度
    /// </summary>
    public float maxWidth
    {
        get => _maxWidth;
        set
        {
            if (_maxWidth != value)
            {
                _maxWidth = value;
                MarkDirty(); // 标记需要刷新
            }
        }
    }

    [SerializeField]
    [TextArea(3, 10)] // 多行文本编辑区域
    [Tooltip("原始文本内容")]
    private string _sourceText = ""; // 原始文本存储
    /// <summary>
    /// 获取或设置原始文本
    /// </summary>
    public string sourceText
    {
        get => _sourceText;
        set
        {
            if (_sourceText != value)
            {
                _sourceText = value;
                MarkDirty(); // 标记需要刷新

                // 在编辑模式下，立即更新文本
#if UNITY_EDITOR
                if (!Application.isPlaying && isActiveAndEnabled)
                {
                    UpdateEllipsisImmediate();
                }
#endif
            }
        }
    }

    // 尺寸变化检测阈值
    private const float SIZE_CHANGE_THRESHOLD = 0.1f;

    private Text _targetText; // 目标Text组件引用
    private bool _isDirty = true; // 脏标记，表示需要刷新
    private TextGenerator _textGenerator = new TextGenerator(); // 文本生成器
    private TextGenerationSettings _generationSettings; // 文本生成设置
    private Vector2 _lastSize = Vector2.zero; // 上一次记录的尺寸
    private Vector2 _lastMaxSize = Vector2.zero; // 上一次记录的最大尺寸
    private HorizontalWrapMode _originalHorizontalOverflow; // 原始水平溢出设置
    private VerticalWrapMode _originalVerticalOverflow; // 原始垂直溢出设置
    private bool _isDragging = false; // 是否正在拖拽

    // 调试信息
    [System.NonSerialized] public Vector2 debugCurrentSize = Vector2.zero; // 当前尺寸（调试）
    [System.NonSerialized] public int debugLineCount = 0; // 行数（调试）
    [System.NonSerialized] public int debugMaxLines = 0; // 最大行数（调试）
    [System.NonSerialized] public float debugLineHeight = 0; // 行高（调试）
    [System.NonSerialized] public float debugTextHeight = 0; // 文本高度（调试）
    [System.NonSerialized] public float debugTotalWidth = 0; // 总宽度（调试）

    /// <summary>
    /// Awake生命周期方法
    /// </summary>
    private void Awake()
    {
        InitializeTargetText(); // 初始化目标Text组件
    }

    /// <summary>
    /// OnEnable生命周期方法
    /// </summary>
    private void OnEnable()
    {
        InitializeTargetText(); // 初始化目标Text组件

        if (_targetText != null)
        {
            // 保存原始溢出设置
            _originalHorizontalOverflow = _targetText.horizontalOverflow;
            _originalVerticalOverflow = _targetText.verticalOverflow;

            // 初始时同步原始文本 - 只有在_sourceText为空时才从Text组件获取
            if (string.IsNullOrEmpty(_sourceText) && !string.IsNullOrEmpty(_targetText.text))
            {
                _sourceText = _targetText.text;
            }
        }

        // 注册Canvas渲染回调
        Canvas.willRenderCanvases += OnCanvasRender;
        MarkDirty(); // 标记需要刷新
    }

    /// <summary>
    /// OnDisable生命周期方法
    /// </summary>
    private void OnDisable()
    {
        // 取消注册Canvas渲染回调
        Canvas.willRenderCanvases -= OnCanvasRender;
        RestoreOriginalOverflowSettings(); // 恢复原始溢出设置
    }

    /// <summary>
    /// Update生命周期方法
    /// </summary>
    private void Update()
    {
#if UNITY_EDITOR
        // 在编辑模式下且非拖拽状态时检查尺寸变化
        if (!Application.isPlaying && !_isDragging)
        {
            CheckSizeChange();
        }
#endif
    }

    /// <summary>
    /// 初始化目标Text组件
    /// </summary>
    private void InitializeTargetText()
    {
        if (_targetText == null)
        {
            _targetText = GetComponent<Text>(); // 获取同一节点上的Text组件
        }
    }

    /// <summary>
    /// 检查尺寸变化
    /// </summary>
    private void CheckSizeChange()
    {
        if (_targetText == null) return;

        Vector2 currentSize = _targetText.rectTransform.rect.size;
        // 如果尺寸变化超过阈值，标记需要刷新
        if (Vector2.Distance(_lastSize, currentSize) > SIZE_CHANGE_THRESHOLD)
        {
            MarkDirty();
            _lastSize = currentSize;
        }
    }

    /// <summary>
    /// Canvas渲染回调
    /// </summary>
    private void OnCanvasRender()
    {
        if (_targetText == null) return;

        // 更新当前尺寸调试信息
        debugCurrentSize = _targetText.rectTransform.rect.size;

        // 如果尺寸变化超过阈值或需要刷新，且开启自动刷新，则更新省略号
        if (_isDirty ||
            Vector2.Distance(_lastSize, debugCurrentSize) > SIZE_CHANGE_THRESHOLD)
        {
            if (autoRefresh)
            {
                UpdateEllipsis();
            }
            _lastSize = debugCurrentSize;
        }
    }

    /// <summary>
    /// 恢复原始溢出设置
    /// </summary>
    private void RestoreOriginalOverflowSettings()
    {
        if (_targetText != null)
        {
            _targetText.horizontalOverflow = _originalHorizontalOverflow;
            _targetText.verticalOverflow = _originalVerticalOverflow;
        }
    }

    /// <summary>
    /// 手动刷新文本（当autoRefresh=false时使用）
    /// </summary>
    public void ManualRefresh()
    {
        MarkDirty(); // 标记需要刷新
        UpdateEllipsis(); // 更新省略号
    }

    /// <summary>
    /// 从Text组件获取当前文本并设置为原始文本
    /// </summary>
    public void ApplyTextFromTarget()
    {
        if (_targetText == null) return;

        sourceText = _targetText.text; // 设置原始文本
        ManualRefresh(); // 手动刷新
    }

    /// <summary>
    /// 标记需要刷新
    /// </summary>
    private void MarkDirty()
    {
        _isDirty = true; // 设置脏标记
    }

    /// <summary>
    /// 立即更新省略号（不检查脏标记）
    /// </summary>
    private void UpdateEllipsisImmediate()
    {
        if (_targetText == null) return;

        // 检查有效尺寸
        if (_targetText.rectTransform.rect.width <= 0 ||
            _targetText.rectTransform.rect.height <= 0) return;

        // 处理空文本情况
        if (string.IsNullOrEmpty(_sourceText))
        {
            _targetText.text = "";
            _isDirty = false;
            return;
        }

        _isDirty = false; // 清除脏标记

        // 保存原始溢出设置
        var origHorizontal = _targetText.horizontalOverflow;
        var origVertical = _targetText.verticalOverflow;

        // 检查不支持的溢出模式
        if ((_mode == EllipsisMode.VisibleArea || _mode == EllipsisMode.MaxWidth) &&
            (origHorizontal == HorizontalWrapMode.Overflow ||
             origVertical == VerticalWrapMode.Overflow))
        {
            return; // 不支持的模式，直接返回
        }

        // 根据模式应用临时设置
        if (_mode != EllipsisMode.MaxCharacters)
        {
            _targetText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _targetText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // 根据模式应用不同的省略号处理方法
        switch (_mode)
        {
            case EllipsisMode.MaxCharacters:
                ApplyMaxCharacters(); // 应用最大字符数模式
                break;
            case EllipsisMode.VisibleArea:
                ApplyVisibleArea(); // 应用可视区域模式
                break;
            case EllipsisMode.MaxWidth:
                ApplyMaxWidth(); // 应用最大宽度模式
                break;
        }

        // 恢复原始溢出设置
        _targetText.horizontalOverflow = origHorizontal;
        _targetText.verticalOverflow = origVertical;
    }

    /// <summary>
    /// 更新省略号
    /// </summary>
    private void UpdateEllipsis()
    {
        // 检查目标Text组件和脏标记
        if (_targetText == null || !_isDirty) return;

        UpdateEllipsisImmediate();
    }

    /// <summary>
    /// 应用最大字符数模式
    /// </summary>
    private void ApplyMaxCharacters()
    {
        // 如果文本长度小于等于最大字符数，直接显示完整文本
        if (_sourceText.Length <= _maxCharacters)
        {
            _targetText.text = _sourceText;
            return;
        }

        // 截断文本并添加省略符号
        _targetText.text = _sourceText.Substring(0, _maxCharacters) + _ellipsisSymbol;
    }

    /// <summary>
    /// 应用可视区域模式
    /// </summary>
    private void ApplyVisibleArea()
    {
        if (_targetText == null) return;

        // 确保使用Wrap和Truncate模式
        _targetText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _targetText.verticalOverflow = VerticalWrapMode.Truncate;

        // 获取文本区域尺寸
        Vector2 textAreaSize = _targetText.rectTransform.rect.size;
        // 获取文本生成设置
        _generationSettings = GetGenerationSettings(textAreaSize);

        // 生成文本并获取行信息
        _textGenerator.Populate(_sourceText, _generationSettings);
        IList<UILineInfo> lines = _textGenerator.lines;
        debugLineCount = lines.Count; // 更新行数调试信息

        // 处理空行情况
        if (lines.Count == 0)
        {
            _targetText.text = _sourceText;
            return;
        }

        // 计算实际文本高度
        debugTextHeight = _textGenerator.GetPreferredHeight(_sourceText, _generationSettings);
        // 计算实际行高
        debugLineHeight = CalculateActualLineHeight();
        float lineHeight = debugLineHeight;

        // 计算最大行数
        float availableHeight = textAreaSize.y;
        debugMaxLines = CalculateMaxLines(availableHeight, lineHeight, lines);

        // 处理无行可显示情况
        if (debugMaxLines <= 0)
        {
            _targetText.text = _ellipsisSymbol;
            return;
        }

        // 如果文本行数小于等于最大行数，直接显示完整文本
        if (lines.Count <= debugMaxLines)
        {
            _targetText.text = _sourceText;
            return;
        }

        // 截取前maxLines行
        int endIndex = lines[debugMaxLines].startCharIdx;
        string truncatedText = _sourceText.Substring(0, endIndex);

        // 在最后一行添加省略号
        int lastLineStart = lines[debugMaxLines - 1].startCharIdx;
        int lastLineLength = endIndex - lastLineStart;
        string lastLine = truncatedText.Substring(lastLineStart, lastLineLength);

        // 添加省略号后的最终文本
        _targetText.text = truncatedText.Substring(0, lastLineStart) +
                      TruncateLineWithEllipsis(lastLine, textAreaSize.x);
    }

    /// <summary>
    /// 计算最大行数
    /// </summary>
    private int CalculateMaxLines(float availableHeight, float lineHeight, IList<UILineInfo> lines)
    {
        // 计算总内容高度
        float contentHeight = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            contentHeight += lines[i].height;
        }

        // 计算理论最大行数
        int maxLines = Mathf.FloorToInt(availableHeight / lineHeight);

        // 如果内容高度小于可用高度，返回所有行
        if (contentHeight <= availableHeight) return lines.Count;

        // 计算实际可显示的行数
        float accumulatedHeight = 0;
        int actualMaxLines = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            accumulatedHeight += lines[i].height;
            if (accumulatedHeight > availableHeight) break;
            actualMaxLines++;
        }

        return actualMaxLines > 0 ? actualMaxLines : 0;
    }

    /// <summary>
    /// 计算实际行高
    /// </summary>
    private float CalculateActualLineHeight()
    {
        // 如果生成器有行信息，使用第一行高度
        if (_textGenerator.lineCount > 0 && _textGenerator.lines.Count > 0)
        {
            return _textGenerator.lines[0].height;
        }

        // 否则根据字体大小计算行高
        float lineHeight = _targetText.fontSize * _targetText.lineSpacing;
        return lineHeight > 0 ? lineHeight : _targetText.fontSize * 1.2f;
    }

    /// <summary>
    /// 应用最大宽度模式
    /// </summary>
    private void ApplyMaxWidth()
    {
        // 获取文本区域尺寸
        Vector2 textAreaSize = _targetText.rectTransform.rect.size;
        _generationSettings = GetGenerationSettings(textAreaSize);

        // 计算总文本宽度
        debugTotalWidth = GetTextWidth(_sourceText);

        // 如果总宽度小于最大宽度，直接显示完整文本
        if (debugTotalWidth <= _maxWidth)
        {
            _targetText.text = _sourceText;
            return;
        }

        // 处理文本以适应最大宽度
        _targetText.text = ProcessTextForWidth(_sourceText, _maxWidth);
    }

    /// <summary>
    /// 处理文本以适应最大宽度
    /// </summary>
    private string ProcessTextForWidth(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // 设置生成器使用最大宽度
        _generationSettings = GetGenerationSettings(new Vector2(maxWidth, float.PositiveInfinity));

        // 先检查整个文本是否适合
        float totalWidth = GetTextWidth(text);
        if (totalWidth <= maxWidth)
        {
            return text;
        }

        // 使用二分查找找到合适的截断位置
        int low = 0;
        int high = text.Length;
        int bestIndex = 0;
        float ellipsisWidth = GetTextWidth(_ellipsisSymbol);

        while (low <= high)
        {
            int mid = (low + high) / 2;
            string testText = text.Substring(0, mid) + _ellipsisSymbol;
            float testWidth = GetTextWidth(testText);

            if (testWidth <= maxWidth)
            {
                bestIndex = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        // 确保不会截断到负数位置
        bestIndex = Mathf.Max(0, bestIndex);

        // 处理空格问题 - 如果考虑空格且截断位置在单词中间，尝试找到最近的空格位置
        if (considerSpaces && bestIndex < text.Length && bestIndex > 0)
        {
            // 如果截断位置不在空格处，尝试找到前一个空格
            if (!char.IsWhiteSpace(text[bestIndex - 1]) && bestIndex > 1)
            {
                int lastSpaceIndex = text.LastIndexOf(' ', bestIndex - 1, Mathf.Min(bestIndex, 20));
                if (lastSpaceIndex > 0)
                {
                    // 检查使用空格位置是否仍然适合
                    string testText = text.Substring(0, lastSpaceIndex) + _ellipsisSymbol;
                    float testWidth = GetTextWidth(testText);
                    if (testWidth <= maxWidth)
                    {
                        bestIndex = lastSpaceIndex;
                    }
                }
            }
        }

        return text.Substring(0, bestIndex) + _ellipsisSymbol;
    }

    /// <summary>
    /// 截断单行文本并添加省略号
    /// </summary>
    private string TruncateLineWithEllipsis(string line, float availableWidth)
    {
        if (string.IsNullOrEmpty(line)) return line;

        // 计算省略号宽度和可用宽度
        float ellipsisWidth = GetTextWidth(_ellipsisSymbol);
        float usableWidth = Mathf.Max(0, availableWidth - ellipsisWidth);

        // 如果连省略号都显示不下，直接返回省略号
        if (ellipsisWidth > availableWidth)
        {
            return _ellipsisSymbol;
        }

        // 如果整行文本加上省略号都能显示，直接返回
        float lineWidth = GetTextWidth(line);
        if (lineWidth + ellipsisWidth <= availableWidth)
        {
            return line + _ellipsisSymbol;
        }

        // 二分查找最佳截断位置
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
                bestIndex = mid; // 更新最佳索引
                low = mid + 1;   // 尝试更长的文本
            }
            else
            {
                high = mid - 1;  // 尝试更短的文本
            }
        }

        // 处理空格问题 - 如果考虑空格且截断位置在单词中间，尝试找到最近的空格位置
        if (considerSpaces && bestIndex < line.Length && bestIndex > 0)
        {
            // 如果截断位置不在空格处，尝试找到前一个空格
            if (!char.IsWhiteSpace(line[bestIndex - 1]) && bestIndex > 1)
            {
                int lastSpaceIndex = line.LastIndexOf(' ', bestIndex - 1, Mathf.Min(bestIndex, 20));
                if (lastSpaceIndex > 0)
                {
                    // 检查使用空格位置是否仍然适合
                    string testText = line.Substring(0, lastSpaceIndex);
                    float testWidth = GetTextWidth(testText);
                    if (testWidth <= usableWidth)
                    {
                        bestIndex = lastSpaceIndex;
                    }
                }
            }
        }

        // 返回截断后的文本加省略号
        return line.Substring(0, bestIndex) + _ellipsisSymbol;
    }

    /// <summary>
    /// 获取文本宽度
    /// </summary>
    private float GetTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return _textGenerator.GetPreferredWidth(text, _generationSettings);
    }

    /// <summary>
    /// 获取文本生成设置
    /// </summary>
    private TextGenerationSettings GetGenerationSettings(Vector2 extents)
    {
        return new TextGenerationSettings
        {
            generationExtents = extents, // 生成范围
            textAnchor = _targetText.alignment, // 文本锚点
            color = _targetText.color, // 文本颜色
            font = _targetText.font, // 字体
            fontSize = _targetText.fontSize, // 字体大小
            fontStyle = _targetText.fontStyle, // 字体样式
            lineSpacing = _targetText.lineSpacing, // 行间距
            richText = _targetText.supportRichText, // 富文本支持
            scaleFactor = 1, // 缩放因子
            horizontalOverflow = _targetText.horizontalOverflow, // 水平溢出
            verticalOverflow = _targetText.verticalOverflow, // 垂直溢出
            updateBounds = false, // 不更新边界
            resizeTextForBestFit = _targetText.resizeTextForBestFit, // 自适应大小
            resizeTextMinSize = _targetText.resizeTextMinSize, // 最小字体大小
            resizeTextMaxSize = _targetText.resizeTextMaxSize // 最大字体大小
        };
    }

    /// <summary>
    /// OnValidate生命周期方法（编辑器模式下调用）
    /// </summary>
    private void OnValidate()
    {
        MarkDirty(); // 标记需要刷新
        // 如果开启自动刷新且组件激活，更新省略号
        if (autoRefresh && isActiveAndEnabled)
        {
            // 在编辑模式下立即更新
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UpdateEllipsisImmediate();
            }
            else
            {
                UpdateEllipsis();
            }
#else
            UpdateEllipsis();
#endif
        }
    }

    // 拖拽事件处理（用于编辑器中的尺寸变化检测）
    public void OnBeginDrag(PointerEventData eventData) => _isDragging = true;
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) => _isDragging = false;
}

#if UNITY_EDITOR
/// <summary>
/// 省略号文本修改器的自定义编辑器
/// </summary>
[CustomEditor(typeof(EllipsisTextModifier))]
public class EllipsisTextModifierEditor : Editor
{
    private bool _showDebugInfo = false; // 是否显示调试信息
    private bool _showVisibleAreaDebug = false; // 是否显示可视区域调试信息
    private bool _showMaxWidthDebug = false; // 是否显示最大宽度调试信息

    /// <summary>
    /// 绘制Inspector界面
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update(); // 更新序列化对象

        // 绘制实时刷新选项
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoRefresh"),
            new GUIContent("实时刷新", "尺寸变化或设置变化时自动刷新文本"));

        // 绘制考虑空格选项
        EditorGUILayout.PropertyField(serializedObject.FindProperty("considerSpaces"),
            new GUIContent("考虑空格", "是否考虑空格，按照英文单词截断"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("原始文本设置", EditorStyles.boldLabel);

        // 绘制原始文本编辑区域
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_sourceText"),
            new GUIContent("原始文本", "编辑原始文本内容"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("省略号设置", EditorStyles.boldLabel);

        // 绘制省略模式
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_mode"),
            new GUIContent("省略模式"));
        // 绘制省略符号
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_ellipsisSymbol"),
            new GUIContent("省略符号"));

        // 根据模式绘制相应设置
        var mode = (EllipsisTextModifier.EllipsisMode)serializedObject
            .FindProperty("_mode").enumValueIndex;

        if (mode == EllipsisTextModifier.EllipsisMode.MaxCharacters)
        {
            // 最大字符数设置
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxCharacters"),
                new GUIContent("最大字符数"));
        }
        else if (mode == EllipsisTextModifier.EllipsisMode.MaxWidth)
        {
            // 最大宽度设置
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxWidth"),
                new GUIContent("最大宽度"));
        }

        serializedObject.ApplyModifiedProperties(); // 应用修改

        EllipsisTextModifier component = (EllipsisTextModifier)target; // 获取目标组件

        EditorGUILayout.Space();

        // 添加Undo支持的"从Text获取"按钮
        if (GUILayout.Button("从Text获取文本到原始文本并应用省略"))
        {
            // 注册撤销操作
            Undo.RecordObjects(new Object[] { component, component.gameObject }, "Apply Text from Target");
            component.ApplyTextFromTarget(); // 执行操作
        }

        EditorGUILayout.Space();
        // 添加Undo支持的手动刷新按钮
        if (GUILayout.Button("根据原始文本应用省略"))
        {
            // 注册撤销操作
            Undo.RecordObjects(new Object[] { component, component.gameObject }, "Manual Refresh Text");
            component.ManualRefresh(); // 执行操作
        }

        // 警告信息（针对不支持的溢出模式）
        if (component.mode != EllipsisTextModifier.EllipsisMode.MaxCharacters)
        {
            Text textComponent = component.GetComponent<Text>();
            if (textComponent && (textComponent.horizontalOverflow == HorizontalWrapMode.Overflow ||
                                  textComponent.verticalOverflow == VerticalWrapMode.Overflow))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "VisibleArea和MaxWidth模式只有在溢出模式设置不为Overflow时才生效。",
                    MessageType.Info
                );
            }
        }

        // 调试信息分组
        EditorGUILayout.Space();
        _showDebugInfo = EditorGUILayout.Foldout(_showDebugInfo, "调试信息", true);
        if (_showDebugInfo)
        {
            EditorGUI.indentLevel++;
            // 显示当前尺寸
            EditorGUILayout.LabelField("当前尺寸", component.debugCurrentSize.ToString());

            // 可视区域模式特定调试信息
            if (component.mode == EllipsisTextModifier.EllipsisMode.VisibleArea)
            {
                EditorGUILayout.Space();
                _showVisibleAreaDebug = EditorGUILayout.Foldout(_showVisibleAreaDebug, "可视区域调试", true);
                if (_showVisibleAreaDebug)
                {
                    EditorGUI.indentLevel++;
                    // 显示行数信息
                    EditorGUILayout.LabelField($"行数: {component.debugLineCount}");
                    // 显示最大行数
                    EditorGUILayout.LabelField($"最大行数: {component.debugMaxLines}");
                    // 显示行高
                    EditorGUILayout.LabelField($"行高: {component.debugLineHeight:F2}");
                    // 显示文本高度
                    EditorGUILayout.LabelField($"文本高度: {component.debugTextHeight:F2}");
                    EditorGUI.indentLevel--;
                }
            }

            // 最大宽度模式特定调试信息
            if (component.mode == EllipsisTextModifier.EllipsisMode.MaxWidth)
            {
                EditorGUILayout.Space();
                _showMaxWidthDebug = EditorGUILayout.Foldout(_showMaxWidthDebug, "最大宽度调试", true);
                if (_showMaxWidthDebug)
                {
                    EditorGUI.indentLevel++;
                    // 显示总宽度信息
                    EditorGUILayout.LabelField($"总宽度: {component.debugTotalWidth:F2}");
                    // 显示最大宽度
                    EditorGUILayout.LabelField($"最大宽度: {component.maxWidth:F2}");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }
    }
}
#endif