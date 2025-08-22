using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.Events;
using System;
using System.Linq;

[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class LineChart : UIBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler
{
    #region 样式类定义

    /// <summary>
    /// 线段样式定义
    /// </summary>
    [System.Serializable]
    public class LineStyle
    {
        public LineMode mode = LineMode.Line;  // 线段渲染模式
        public Color color = Color.white;      // 线段颜色
        public float width = 2f;               // 线段宽度
        public Sprite sprite;                  // 线段精灵（用于Sprite模式）
        public Material material;              // 线段材质
    }

    /// <summary>
    /// 数据点样式定义
    /// </summary>
    [System.Serializable]
    public class DataPointStyle
    {
        public bool showPoints = true;         // 是否显示数据点
        public PointMode mode = PointMode.Circle; // 点渲染模式
        public float pointSize = 20f;           // 点尺寸
        public Color color = Color.white;      // 点颜色
        public Sprite pointSprite;             // 点精灵（用于Sprite模式）
    }

    /// <summary>
    /// 坐标轴标签样式定义
    /// </summary>
    [System.Serializable]
    public class AxisLabelStyle
    {
        public Font font;                     // 标签字体
        public int fontSize = 20;              // 字体大小
        public Color color = Color.white;       // 字体颜色
        public Vector2 offset = Vector2.zero;  // 位置偏移
        public TextAnchor alignment;           // 文本对齐方式
        public HorizontalWrapMode horizontalOverflow = HorizontalWrapMode.Overflow; // 水平溢出模式
        public VerticalWrapMode verticalOverflow = VerticalWrapMode.Overflow;       // 垂直溢出模式
    }

    /// <summary>
    /// 图表数据线定义
    /// </summary>
    [System.Serializable]
    public class LineData
    {
        public string name;                   // 数据线名称
        public List<Vector2> points = new List<Vector2>(); // 数据点集合
        public LineStyle lineStyle = new LineStyle();      // 线段样式
        public DataPointStyle pointStyle = new DataPointStyle(); // 点样式
    }

    /// <summary>
    /// 图表指针事件定义
    /// </summary>
    [System.Serializable]
    public class ChartPointerEvent : UnityEvent<ChartPointerData> { }

    /// <summary>
    /// 图表指针事件数据结构
    /// </summary>
    public struct ChartPointerData
    {
        /// <summary>指针在图表中的视图坐标（相对于组件左下角）</summary>
        public Vector2 anchoredPosition;

        /// <summary>指针对应的数据坐标</summary>
        public Vector2 dataPosition;

        /// <summary>最近的数据点位置（如果找到）</summary>
        public Vector2 nearestPointDataPosition;

        /// <summary>最近的数据点在视图中的位置（如果找到）</summary>
        public Vector2 nearestPointAnchoredPosition;

        /// <summary>最近点所属的数据线名称</summary>
        public string nearestLineName;

        /// <summary>是否找到最近的数据点</summary>
        public bool hasNearestPoint;
    }

    #endregion

    #region 枚举定义

    public enum LineMode { Line, Sprite }      // 线段渲染模式
    public enum PointMode { Circle, Sprite }    // 点渲染模式

    // 新增吸附模式枚举
    public enum SnapMode
    {
        None,     // 不吸附
        Point,    // 普通点吸附（欧几里得距离）
        XAxis,    // X轴优先吸附
        YAxis     // Y轴优先吸附
    }

    #endregion

    #region 公共字段和属性

    [Header("Axis Settings")]
    public LineStyle xAxisStyle = new LineStyle() { width = 10, color = Color.red };  // X轴样式
    public LineStyle yAxisStyle = new LineStyle() { width = 10, color = Color.green };  // Y轴样式
    public float axisOffsetX = 0;                 // X轴偏移
    public float axisOffsetY = 0;                 // Y轴偏移
    public bool showAxisArrows = false;           // 是否显示坐标轴箭头
    public Sprite axisArrowSprite;                // 坐标轴箭头精灵

    [Header("Axis Arrow Settings")]
    public Vector2 xAxisArrowSize = new Vector2(20f, 20f); // X轴箭头尺寸
    public Vector2 yAxisArrowSize = new Vector2(20f, 20f); // Y轴箭头尺寸
    public float xAxisArrowRotation = 0f;         // X轴箭头旋转角度
    public float yAxisArrowRotation = 90f;        // Y轴箭头旋转角度
    public Color xAxisArrowColor = Color.white;   // X轴箭头颜色
    public Color yAxisArrowColor = Color.white;   // Y轴箭头颜色

    [Header("Grid Settings")]
    public bool showGridX = true;                  // 是否显示X轴网格
    public bool showGridY = true;                  // 是否显示Y轴网格
    public LineStyle xGridStyle = new LineStyle();  // X轴网格样式
    public LineStyle yGridStyle = new LineStyle();  // Y轴网格样式
    [Tooltip("X轴网格在视图上的间隔（像素）")]
    public float gridIntervalX = 100f;             // X轴网格视图间隔
    [Tooltip("Y轴网格在视图上的间隔（像素）")]
    public float gridIntervalY = 100f;             // Y轴网格视图间隔
    [Tooltip("每个X轴网格表示的数据变化量")]
    public float gridDataIntervalX = 1f;           // X轴网格数据间隔
    [Tooltip("每个Y轴网格表示的数据变化量")]
    public float gridDataIntervalY = 1f;           // Y轴网格数据间隔

    [Header("Label Settings")]
    public bool showLabels = true;                // 是否显示标签
    public string xLabelFormat = "{0}";        // X轴标签格式
    public string yLabelFormat = "{0}";        // Y轴标签格式
    public AxisLabelStyle xLabelStyle = new AxisLabelStyle() // X轴标签样式
    {
        offset = new Vector2(0, -30f),
        alignment = TextAnchor.UpperCenter,
        horizontalOverflow = HorizontalWrapMode.Overflow,
        verticalOverflow = VerticalWrapMode.Overflow
    };
    public AxisLabelStyle yLabelStyle = new AxisLabelStyle() // Y轴标签样式
    {
        offset = new Vector2(-70f, 0),
        alignment = TextAnchor.MiddleRight,
        horizontalOverflow = HorizontalWrapMode.Overflow,
        verticalOverflow = VerticalWrapMode.Overflow
    };

    // 自定义标签格式化委托
    public Func<float, string> xLabelFormatter;   // X轴标签格式化方法
    public Func<float, string> yLabelFormatter;   // Y轴标签格式化方法

    [Header("Data Settings")]
    public Vector2 origin = Vector2.zero;         // 坐标原点
    public List<LineData> lines = new List<LineData>(); // 数据线集合

    [Header("Interaction Settings")]
    public bool enableInteraction = true;          // 是否启用交互
    public SnapMode snapMode = SnapMode.Point;     // 吸附模式
    public float snapRadius = 50f;                 // 吸附半径

    [Header("Events")]
    public ChartPointerEvent onPointerDown = new ChartPointerEvent(); // 指针按下事件
    public ChartPointerEvent onPointerMove = new ChartPointerEvent(); // 指针移动事件
    public ChartPointerEvent onPointerUp = new ChartPointerEvent();   // 指针抬起事件

    #endregion

    #region 私有字段

    private RectTransform _rectTransform;          // 图表的RectTransform
    private Canvas _canvas;                        // 所在画布

    // 图表容器
    private GameObject _xAxis;                     // X轴容器
    private GameObject _yAxis;                     // Y轴容器
    private Transform _gridContainer;              // 网格容器
    private Transform _linesContainer;             // 数据线容器
    private Transform _pointsContainer;            // 数据点容器
    private Transform _labelsContainer;            // 标签容器

    private Vector2 _lastSize;                     // 上一次记录的尺寸
    private Rect _viewportRect;                    // 视图矩形

    // 后备字体
    private Font _fallbackFont;                    // 后备字体资源

    // 唯一名称计数器
    private int _lineNameCounter = 1;              // 用于生成唯一名称

    // 交互状态跟踪
    private bool _isPointerDown = false;           // 指针是否按下
    private Vector2 _lastPointerPosition;          // 最后一次记录的指针位置

    // 重建锁 - 防止在销毁时重建
    private bool _isDestroying = false;

    // 脏标记系统 - 细粒度控制
    private bool _needsAxisRebuild = true;          // 轴需要重建
    private bool _needsGridRebuild = true;          // 网格需要重建
    private bool _needsLabelsRebuild = true;        // 标签需要重建
    private bool _needsDataRebuild = true;          // 数据需要重建
    private bool _needsViewRecalculation = true;    // 视图参数需要重新计算

    #endregion

    #region Unity生命周期方法

    protected override void Start()
    {
        base.Start();
        Initialize();
        // 确保在启用时重建图表
        RebuildChartIfNeeded();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (_rectTransform == null) return;

        // 当尺寸变化时标记需要重建
        Vector2 currentSize = _rectTransform.rect.size;
        if (currentSize != _lastSize)
        {
            _lastSize = currentSize;
            _needsViewRecalculation = true;
            _needsAxisRebuild = true;
            _needsGridRebuild = true;
            _needsLabelsRebuild = true;
            _needsDataRebuild = true; // 视图尺寸变化需要重建数据
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // 重置指针状态
        _isPointerDown = false;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _isDestroying = true;
    }

    private void Update()
    {
        // 每帧检查是否需要重建
        RebuildChartIfNeeded();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        if (!IsActive() || _rectTransform == null)
            return;

        // 检查线名重复
        CheckForDuplicateLineNames();

        // 标记所有部分需要重建 - 确保任何编辑器修改都能触发重建
        MarkAllForRebuild();
    }
#endif

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化图表
    /// </summary>
    private void Initialize()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParents<Canvas>(this);

        // 加载后备字体
        LoadFallbackFont();

        // 创建图表容器
        CreateContainers();

        // 记录初始尺寸
        _lastSize = _rectTransform.rect.size;
    }

    /// <summary>
    /// 标记所有部分需要重建
    /// </summary>
    private void MarkAllForRebuild()
    {
        _needsAxisRebuild = true;
        _needsGridRebuild = true;
        _needsLabelsRebuild = true;
        _needsDataRebuild = true;
        _needsViewRecalculation = true;
    }

    /// <summary>
    /// 加载后备字体资源
    /// </summary>
    private void LoadFallbackFont()
    {
        if (_fallbackFont == null)
        {
            _fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>
    /// 创建图表容器
    /// </summary>
    private void CreateContainers()
    {
        // 安全创建容器，防止重复创建
        // 以下容器的创建顺序影响遮挡关系
        _gridContainer = GetOrCreateContainer("GridContainer", transform).transform;
        _linesContainer = GetOrCreateContainer("LinesContainer", transform).transform;
        _yAxis = GetOrCreateContainer("YAxis", transform);
        _xAxis = GetOrCreateContainer("XAxis", transform);
        _pointsContainer = GetOrCreateContainer("PointsContainer", transform).transform;
        _labelsContainer = GetOrCreateContainer("LabelsContainer", transform).transform;
    }

    /// <summary>
    /// 获取或创建容器对象
    /// </summary>
    private GameObject GetOrCreateContainer(string name, Transform parent)
    {
        // 尝试查找现有容器
        Transform container = transform.Find(name);

        // 如果容器存在且有效，直接返回
        if (container != null && container.gameObject != null)
        {
            return container.gameObject;
        }

        // 创建新容器
        GameObject newContainer = new GameObject(name);
        newContainer.transform.SetParent(parent, false);
        newContainer.transform.localPosition = Vector3.zero;
        newContainer.transform.localScale = Vector3.one;

        // 添加并设置RectTransform
        RectTransform rt = newContainer.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        return newContainer;
    }

    #endregion

    #region 图表构建方法

    /// <summary>
    /// 重新构建整个图表
    /// </summary>
    [ContextMenu("Rebuild Chart")]
    public void RebuildChart()
    {
        if (_isDestroying) return;

        // 强制完全重建
        MarkAllForRebuild();
        InternalRebuildChart();
    }

    /// <summary>
    /// 检查并执行图表重建
    /// </summary>
    public void RebuildChartIfNeeded(bool includeInActive = false)
    {
        if (_isDestroying || (includeInActive is false && !IsActive()) || _rectTransform == null)
            return;

        // 如果有任何部分需要重建
        if (_needsAxisRebuild || _needsGridRebuild || _needsLabelsRebuild || _needsDataRebuild || _needsViewRecalculation)
        {
            InternalRebuildChart();
        }
    }

    /// <summary>
    /// 实际执行图表重建（细粒度）
    /// </summary>
    private void InternalRebuildChart()
    {
        if (_isDestroying || _rectTransform == null)
            return;

        // 确保容器存在
        EnsureContainers();

        // 重新计算视图参数（如果需要）
        if (_needsViewRecalculation)
        {
            CalculateViewParameters();
            _needsViewRecalculation = false;

            // 视图参数变化通常需要重建所有依赖元素
            _needsAxisRebuild = true;
            _needsGridRebuild = true;
            _needsLabelsRebuild = true;
            _needsDataRebuild = true;
        }

        // 重建网格（如果需要）
        if (_needsGridRebuild)
        {
            SafeClearContainer(_gridContainer);
            if (showGridX)
            {
                DrawGridX();
            }
            if (showGridY)
            {
                DrawGridY();
            }
            _needsGridRebuild = false;
        }

        // 重建数据（如果需要）
        if (_needsDataRebuild)
        {
            SafeClearContainer(_linesContainer);
            SafeClearContainer(_pointsContainer);
            DrawLines();
            _needsDataRebuild = false;
        }

        // 重建坐标轴（如果需要）
        if (_needsAxisRebuild)
        {
            SafeClearContainer(_xAxis.transform);
            SafeClearContainer(_yAxis.transform);
            DrawAxis();
            _needsAxisRebuild = false;
        }

        // 重建标签（如果需要）
        if (_needsLabelsRebuild)
        {
            SafeClearContainer(_labelsContainer);
            if (showLabels)
            {
                DrawLabels();
            }
            _needsLabelsRebuild = false;
        }
    }

    /// <summary>
    /// 确保所有容器存在且有效
    /// </summary>
    private void EnsureContainers()
    {
        //以下容器的创建顺序影响遮挡关系
        if (_gridContainer == null || !_gridContainer) _gridContainer = GetOrCreateContainer("GridContainer", transform).transform;
        if (_linesContainer == null || !_linesContainer) _linesContainer = GetOrCreateContainer("LinesContainer", transform).transform;
        if (_yAxis == null || !_yAxis) _yAxis = GetOrCreateContainer("YAxis", transform);
        if (_xAxis == null || !_xAxis) _xAxis = GetOrCreateContainer("XAxis", transform);
        if (_pointsContainer == null || !_pointsContainer) _pointsContainer = GetOrCreateContainer("PointsContainer", transform).transform;
        if (_labelsContainer == null || !_labelsContainer) _labelsContainer = GetOrCreateContainer("LabelsContainer", transform).transform;
    }

    /// <summary>
    /// 安全清除容器
    /// </summary>
    private void SafeClearContainer(Transform container)
    {
        if (container == null) return;

        // 检查容器是否有效
        if (container.gameObject == null) return;

        // 清除子对象
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < container.childCount; i++)
        {
            children.Add(container.GetChild(i).gameObject);
        }

        // 在编辑模式下使用DestroyImmediate，运行时使用Destroy
        foreach (GameObject child in children)
        {
            if (child != null)
            {
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }

    /// <summary>
    /// 计算视图参数和边界
    /// </summary>
    private void CalculateViewParameters()
    {
        // 计算视图矩形（相对于左下角）
        _viewportRect = new Rect(
            axisOffsetX,
            axisOffsetY,
            Mathf.Max(1, _rectTransform.rect.width - axisOffsetX),
            Mathf.Max(1, _rectTransform.rect.height - axisOffsetY)
        );
    }

    /// <summary>
    /// 计算所有数据点的边界（公共方法）
    /// </summary>
    public void CalculateDataBounds(out Vector2 min, out Vector2 max)
    {
        // 检查是否有数据线
        if (lines.Count == 0)
        {
            min = Vector2.zero;
            max = Vector2.one;
            return;
        }

        // 找到第一条有效的数据线
        LineData firstValidLine = null;
        foreach (var line in lines)
        {
            if (line.points != null && line.points.Count > 0)
            {
                firstValidLine = line;
                break;
            }
        }

        // 如果没有有效数据，使用默认值
        if (firstValidLine == null)
        {
            min = Vector2.zero;
            max = Vector2.one;
            return;
        }

        // 初始化最小/最大值
        min = firstValidLine.points[0];
        max = firstValidLine.points[0];

        // 遍历所有数据点计算边界
        foreach (var line in lines)
        {
            if (line.points == null) continue;

            foreach (var point in line.points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
    }

    /// <summary>
    /// 检查线名重复并在编辑器给出警告
    /// </summary>
    private void CheckForDuplicateLineNames()
    {
        if (lines == null || lines.Count < 2)
            return;

        var nameCount = new Dictionary<string, int>();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line.name))
                continue;

            if (nameCount.ContainsKey(line.name))
            {
                nameCount[line.name]++;
            }
            else
            {
                nameCount[line.name] = 1;
            }
        }

        foreach (var kvp in nameCount)
        {
            if (kvp.Value > 1)
            {
                Debug.LogWarning($"发现重复的线名: {kvp.Value} 条线都命名为 '{kvp.Key}'。这可能导致不可预期的行为。", this);
            }
        }
    }

    #endregion

    #region 图表绘制方法

    /// <summary>
    /// 绘制坐标轴
    /// </summary>
    private void DrawAxis()
    {
        // X轴
        Vector2 xStart = new Vector2(axisOffsetX, axisOffsetY);
        Vector2 xEnd = new Vector2(axisOffsetX + _viewportRect.width, axisOffsetY);
        DrawLine("XAxis_Line", xStart, xEnd, xAxisStyle, _xAxis.transform);

        // 绘制X轴箭头
        if (showAxisArrows && axisArrowSprite)
        {
            CreateAxisArrow(xEnd, xAxisArrowRotation, xAxisArrowSize, xAxisArrowColor, _xAxis.transform);
        }

        // Y轴
        Vector2 yStart = new Vector2(axisOffsetX, axisOffsetY);
        Vector2 yEnd = new Vector2(axisOffsetX, axisOffsetY + _viewportRect.height);
        DrawLine("YAxis_Line", yStart, yEnd, yAxisStyle, _yAxis.transform);

        // 绘制Y轴箭头
        if (showAxisArrows && axisArrowSprite)
        {
            CreateAxisArrow(yEnd, yAxisArrowRotation, yAxisArrowSize, yAxisArrowColor, _yAxis.transform);
        }
    }

    /// <summary>
    /// 创建坐标轴箭头
    /// </summary>
    private void CreateAxisArrow(Vector2 position, float rotation, Vector2 size, Color color, Transform parent)
    {
        if (parent == null) return;

        GameObject arrow = CreateChild("AxisArrow", parent);
        if (arrow == null) return;

        RectTransform rt = arrow.GetComponent<RectTransform>();
        if (rt == null) return;

        // 设置箭头位置和旋转
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localEulerAngles = new Vector3(0, 0, rotation);

        // 添加并设置图像组件
        Image img = arrow.AddComponent<Image>();
        img.sprite = axisArrowSprite;
        img.color = color;
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGridX()
    {
        // 确保网格容器存在
        if (_gridContainer == null) return;

        // X方向网格线
        int xCount = Mathf.FloorToInt(_viewportRect.width / gridIntervalX);
        for (int i = 1; i <= xCount; i++)
        {
            float xPos = axisOffsetX + i * gridIntervalX;
            Vector2 start = new Vector2(xPos, axisOffsetY);
            Vector2 end = new Vector2(xPos, axisOffsetY + _viewportRect.height);

            DrawLine($"XGridLine_{i}", start, end, xGridStyle, _gridContainer);
        }
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGridY()
    {
        // 确保网格容器存在
        if (_gridContainer == null) return;

        // Y方向网格线
        int yCount = Mathf.FloorToInt(_viewportRect.height / gridIntervalY);
        for (int i = 1; i <= yCount; i++)
        {
            float yPos = axisOffsetY + i * gridIntervalY;
            Vector2 start = new Vector2(axisOffsetX, yPos);
            Vector2 end = new Vector2(axisOffsetX + _viewportRect.width, yPos);

            DrawLine($"YGridLine_{i}", start, end, yGridStyle, _gridContainer);
        }
    }

    /// <summary>
    /// 绘制坐标轴标签
    /// </summary>
    private void DrawLabels()
    {
        // 确保标签容器存在
        if (_labelsContainer == null) return;

        // X轴标签
        int xCount = Mathf.FloorToInt(_viewportRect.width / gridIntervalX);
        for (int i = 0; i <= xCount; i++)
        {
            float xPos = axisOffsetX + i * gridIntervalX;

            // 计算标签值（考虑数据间隔）
            float value = origin.x + i * gridDataIntervalX;

            // 格式化标签文本
            string text = xLabelFormatter != null ?
                xLabelFormatter(value) :
                string.Format(xLabelFormat, value);

            // 创建标签
            CreateLabel($"XLabel_{i}",
                new Vector2(xPos, axisOffsetY) + xLabelStyle.offset,
                text,
                xLabelStyle
            );
        }

        // Y轴标签
        int yCount = Mathf.FloorToInt(_viewportRect.height / gridIntervalY);
        for (int i = 0; i <= yCount; i++)
        {
            float yPos = axisOffsetY + i * gridIntervalY;

            // 计算标签值（考虑数据间隔）
            float value = origin.y + i * gridDataIntervalY;

            // 格式化标签文本
            string text = yLabelFormatter != null ?
                yLabelFormatter(value) :
                string.Format(yLabelFormat, value);

            // 创建标签
            CreateLabel($"YLabel_{i}",
                new Vector2(axisOffsetX, yPos) + yLabelStyle.offset,
                text,
                yLabelStyle
            );
        }
    }

    /// <summary>
    /// 创建标签
    /// </summary>
    private void CreateLabel(string name, Vector2 position, string text, AxisLabelStyle style)
    {
        if (_labelsContainer == null) return;

        GameObject labelObj = CreateChild(name, _labelsContainer);
        if (labelObj == null) return;

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        if (rt == null) return;

        // 设置标签位置
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(100, 30);

        // 添加并设置文本组件
        Text txt = labelObj.AddComponent<Text>();
        txt.text = text;
        txt.font = style.font ? style.font : _fallbackFont;
        txt.fontSize = style.fontSize;
        txt.color = style.color;
        txt.alignment = style.alignment;
        txt.horizontalOverflow = style.horizontalOverflow;
        txt.verticalOverflow = style.verticalOverflow;
    }

    /// <summary>
    /// 绘制所有数据线和点
    /// </summary>
    private void DrawLines()
    {
        foreach (var line in lines)
        {
            if (line.points == null || line.points.Count == 0) continue;

            // 为当前数据线创建容器
            Transform lineContainer = CreateChild($"{line.name}_Line", _linesContainer)?.transform;
            if (lineContainer == null) continue;

            // 绘制线段
            for (int i = 0; i < line.points.Count - 1; i++)
            {
                Vector2 start = DataToView(line.points[i]);
                Vector2 end = DataToView(line.points[i + 1]);

                // 检查线段是否在视图内
                if (IsLineInViewport(start, end, line.pointStyle.pointSize))
                {
                    DrawLine($"Segment_{i}", start, end, line.lineStyle, lineContainer);
                }
            }

            // 绘制数据点
            if (line.pointStyle.showPoints)
            {
                for (int i = 0; i < line.points.Count; i++)
                {
                    Vector2 viewPos = DataToView(line.points[i]);

                    // 检查点是否在视图内
                    if (IsPointInViewport(viewPos, line.pointStyle.pointSize))
                    {
                        DrawPoint($"{line.name}_Point_{i}", viewPos, line.pointStyle, _pointsContainer);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 安全创建子对象
    /// </summary>
    private GameObject CreateChild(string name, Transform parent)
    {
        if (parent == null) return null;

        try
        {
            // 创建新对象
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);

            // 添加并设置RectTransform
            RectTransform rt = child.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            return child;
        }
        catch (Exception)
        {
            // 忽略创建过程中的错误（通常在销毁时发生）
            return null;
        }
    }

    /// <summary>
    /// 检查点是否在视图范围内
    /// </summary>
    /// <param name="pointSize">点直径</param>
    private bool IsPointInViewport(Vector2 viewPos, float pointSize)
    {
        // 扩展视图矩形以包含点半径
        Rect expandedViewport = new Rect(
            _viewportRect.x - pointSize / 2,
            _viewportRect.y - pointSize / 2,
            _viewportRect.width + pointSize,
            _viewportRect.height + pointSize
        );
        return expandedViewport.Contains(viewPos);
    }

    /// <summary>
    /// 绘制线段
    /// </summary>
    private void DrawLine(string name, Vector2 start, Vector2 end, LineStyle style, Transform parent)
    {
        if (parent == null) return;

        try
        {
            // 根据渲染模式创建线段
            switch (style.mode)
            {
                case LineMode.Line:
                    CreateLineRenderer(name, start, end, style, parent);
                    break;

                case LineMode.Sprite when style.sprite != null:
                    CreateSpriteLine(name, start, end, style, parent);
                    break;
            }
        }
        catch (Exception)
        {
            // 忽略绘制错误
        }
    }

    /// <summary>
    /// 创建线段渲染器
    /// </summary>
    private void CreateLineRenderer(string name, Vector2 start, Vector2 end, LineStyle style, Transform parent)
    {
        GameObject lineObj = CreateChild(name, parent);
        if (lineObj == null) return;

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        if (rt == null) return;

        // 计算线段中点
        Vector2 midPoint = (start + end) * 0.5f;

        // 计算线段角度和长度
        Vector2 direction = end - start;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 设置线段属性
        rt.sizeDelta = new Vector2(distance, style.width);
        rt.anchoredPosition = midPoint;
        rt.localEulerAngles = new Vector3(0, 0, angle);

        // 添加并设置图像组件
        Image img = lineObj.AddComponent<Image>();
        img.color = style.color;
        img.material = style.material;
    }

    /// <summary>
    /// 创建精灵线段
    /// </summary>
    private void CreateSpriteLine(string name, Vector2 start, Vector2 end, LineStyle style, Transform parent)
    {
        GameObject lineObj = CreateChild(name, parent);
        if (lineObj == null) return;

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        if (rt == null) return;

        // 计算线段中点
        Vector2 midPoint = (start + end) * 0.5f;

        // 计算线段角度和长度
        Vector2 direction = end - start;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 设置线段属性
        rt.sizeDelta = new Vector2(distance, style.width);
        rt.anchoredPosition = midPoint;
        rt.localEulerAngles = new Vector3(0, 0, angle);

        // 添加并设置图像组件
        Image img = lineObj.AddComponent<Image>();
        img.sprite = style.sprite;
        img.color = style.color;
        img.material = style.material;
        img.type = Image.Type.Sliced;
    }

    /// <summary>
    /// 绘制数据点
    /// </summary>
    private void DrawPoint(string name, Vector2 position, DataPointStyle style, Transform parent)
    {
        if (parent == null) return;

        GameObject pointObj = CreateChild(name, parent);
        if (pointObj == null) return;

        RectTransform rt = pointObj.GetComponent<RectTransform>();
        if (rt == null) return;

        // 设置点的位置
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(style.pointSize, style.pointSize);

        // 添加并设置图像组件
        Image img = pointObj.AddComponent<Image>();

        // 根据渲染模式设置点样式
        if (style.mode == PointMode.Sprite && style.pointSprite != null)
        {
            img.sprite = style.pointSprite;
            img.color = style.color;
        }
        else
        {
            img.color = style.color;
            img.sprite = CreateCircleSprite((int)style.pointSize, style.color);
        }
    }

    /// <summary>
    /// 创建圆形精灵
    /// </summary>
    private Sprite CreateCircleSprite(int size, Color color)
    {
        if (size <= 0) size = 1;

        // 创建纹理
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        float radius = size / 2f;
        float radiusSq = radius * radius;

        // 填充纹理像素
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                colors[y * size + x] = (dx * dx + dy * dy) <= radiusSq ? color : Color.clear;
            }
        }

        // 应用纹理设置
        tex.SetPixels(colors);
        tex.Apply();

        // 创建精灵
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }

    #endregion

    #region 坐标转换方法

    /// <summary>
    /// 数据点坐标转换为视图坐标（相对于组件左下角）
    /// </summary>
    public Vector2 DataToView(Vector2 dataPoint)
    {
        return new Vector2(
            axisOffsetX + (dataPoint.x - origin.x) * (gridIntervalX / gridDataIntervalX),
            axisOffsetY + (dataPoint.y - origin.y) * (gridIntervalY / gridDataIntervalY)
        );
    }

    /// <summary>
    /// 视图坐标转换为数据点坐标
    /// </summary>
    public Vector2 ViewToData(Vector2 viewPoint)
    {
        return new Vector2(
            origin.x + (viewPoint.x - axisOffsetX) * (gridDataIntervalX / gridIntervalX),
            origin.y + (viewPoint.y - axisOffsetY) * (gridDataIntervalY / gridIntervalY)
        );
    }

    /// <summary>
    /// 将本地坐标转换为相对于左下角的坐标
    /// </summary>
    private Vector2 ConvertToBottomLeft(Vector2 localPoint)
    {
        // 获取RectTransform的尺寸
        Rect rect = _rectTransform.rect;

        // 转换为左下角坐标系
        return new Vector2(
            localPoint.x + rect.width / 2f,
            localPoint.y + rect.height / 2f
        );
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查线段是否在视图范围内
    /// </summary>
    private bool IsLineInViewport(Vector2 start, Vector2 end, float pointSize)
    {
        // 检查线段端点是否在视图内
        if (IsPointInViewport(start, pointSize)) return true;
        if (IsPointInViewport(end, pointSize)) return true;

        // 检查线段是否与视图矩形相交
        return LineIntersectsRect(start, end, _viewportRect);
    }

    /// <summary>
    /// 检查线段是否与矩形相交
    /// </summary>
    private bool LineIntersectsRect(Vector2 p1, Vector2 p2, Rect rect)
    {
        // 检查线段是否与矩形的四条边相交
        return LineIntersectsLine(p1, p2, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin)) ||
               LineIntersectsLine(p1, p2, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax)) ||
               LineIntersectsLine(p1, p2, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)) ||
               LineIntersectsLine(p1, p2, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin));
    }

    /// <summary>
    /// 检查两条线段是否相交
    /// </summary>
    private bool LineIntersectsLine(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        // 计算叉积
        float denominator = ((a2.x - a1.x) * (b2.y - b1.y)) - ((a2.y - a1.y) * (b2.x - b1.x));
        if (denominator == 0) return false;

        // 计算交点参数
        float ua = (((a1.y - b1.y) * (b2.x - b1.x)) - ((a1.x - b1.x) * (b2.y - b1.y))) / denominator;
        float ub = (((a1.y - b1.y) * (a2.x - a1.x)) - ((a1.x - b1.x) * (a2.y - a1.y))) / denominator;

        // 检查交点是否在线段上
        return ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1;
    }

    /// <summary>
    /// 从当前对象开始递归获取第一个父节点组件（包含自身）
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="component">当前组件</param>
    /// <param name="includeInactive">是否包含未激活对象</param>
    /// <returns>找到的组件或null</returns>
    private T GetComponentInParents<T>(Component component, bool includeInactive = false)
        where T : Component
    {
        if (component == null) return null;

        Transform current = component.transform;

        while (current != null)
        {
            // 检查当前对象是否满足条件
            if (includeInactive || current.gameObject.activeInHierarchy)
            {
                T comp = current.GetComponent<T>();
                if (comp != null) return comp;
            }

            // 移动到父对象
            current = current.parent;
        }

        return null;
    }

    #endregion

    #region 交互事件处理

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enableInteraction) return;

        // 将屏幕坐标转换为本地坐标（相对于组件RectTransform）
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, eventData.position, _canvas.worldCamera, out localPoint))
        {
            // 转换为相对于左下角的坐标
            Vector2 bottomLeftPoint = ConvertToBottomLeft(localPoint);

            // 更新指针状态
            _isPointerDown = true;
            _lastPointerPosition = bottomLeftPoint;

            // 创建事件数据并触发事件
            onPointerDown.Invoke(CreatePointerData(bottomLeftPoint));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!enableInteraction) return;

        // 将屏幕坐标转换为本地坐标（相对于组件RectTransform）
        Vector2 localPoint;
        bool positionValid = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, eventData.position, _canvas.worldCamera, out localPoint);

        // 转换为相对于左下角的坐标
        Vector2 pointerPos = positionValid ?
            ConvertToBottomLeft(localPoint) :
            _lastPointerPosition;

        // 更新指针状态
        _isPointerDown = false;

        // 创建事件数据并触发事件
        onPointerUp.Invoke(CreatePointerData(pointerPos));
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!enableInteraction || !_isPointerDown) return;

        // 将屏幕坐标转换为本地坐标（相对于组件RectTransform）
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, eventData.position, _canvas.worldCamera, out localPoint))
        {
            // 转换为相对于左下角的坐标
            Vector2 bottomLeftPoint = ConvertToBottomLeft(localPoint);

            // 更新指针位置
            _lastPointerPosition = bottomLeftPoint;

            // 创建事件数据并触发事件
            onPointerMove.Invoke(CreatePointerData(bottomLeftPoint));
        }
        else
        {
            // 使用最后一次有效位置
            onPointerMove.Invoke(CreatePointerData(_lastPointerPosition));
        }
    }

    /// <summary>
    /// 创建指针事件数据（使用相对于左下角的坐标）
    /// </summary>
    private ChartPointerData CreatePointerData(Vector2 bottomLeftPoint)
    {
        ChartPointerData data = new ChartPointerData();
        data.anchoredPosition = bottomLeftPoint; // 相对于组件左下角
        data.dataPosition = ViewToData(bottomLeftPoint);

        // 尝试获取最近的点
        if (snapMode != SnapMode.None && TryGetNearestPoint(bottomLeftPoint, out Vector2 nearestPoint, out string nearestLine))
        {
            data.nearestPointDataPosition = nearestPoint;
            data.nearestPointAnchoredPosition = DataToView(nearestPoint);
            data.nearestLineName = nearestLine;
            data.hasNearestPoint = true;
        }
        else
        {
            data.hasNearestPoint = false;
        }

        return data;
    }

    /// <summary>
    /// 尝试获取最近的数据点（使用相对于左下角的坐标）
    /// </summary>
    public bool TryGetNearestPoint(Vector2 viewPosition, out Vector2 nearestPoint, out string lineName)
    {
        nearestPoint = Vector2.zero;
        lineName = "";
        float minDistance = float.MaxValue;
        bool found = false;

        // 计算实际吸附半径（考虑画布缩放因子）
        //float actualSnapRadius = snapRadius * (_canvas != null ? _canvas.scaleFactor : 1f);
        float actualSnapRadius = snapRadius;
        float sqrSnapRadius = actualSnapRadius * actualSnapRadius;

        // 遍历所有数据线
        foreach (var line in lines)
        {
            // 跳过无效数据线
            if (line.points == null) continue;

            // 遍历数据点
            for (int i = 0; i < line.points.Count; i++)
            {
                Vector2 point = line.points[i];
                Vector2 pointViewPos = DataToView(point);

                // 根据吸附模式计算距离
                float distance = float.MaxValue;
                switch (snapMode)
                {
                    case SnapMode.Point:
                        // 普通点吸附：使用欧几里得距离
                        distance = (viewPosition - pointViewPos).sqrMagnitude;
                        break;

                    case SnapMode.XAxis:
                        // X轴优先：先比较X轴方向距离，再比较Y轴方向距离
                        float xDist = Mathf.Abs(viewPosition.x - pointViewPos.x);
                        if (xDist <= actualSnapRadius)
                        {
                            // 在X轴方向满足条件后，再考虑Y轴方向
                            distance = Mathf.Abs(viewPosition.y - pointViewPos.y);
                        }
                        break;

                    case SnapMode.YAxis:
                        // Y轴优先：先比较Y轴方向距离，再比较X轴方向距离
                        float yDist = Mathf.Abs(viewPosition.y - pointViewPos.y);
                        if (yDist <= actualSnapRadius)
                        {
                            // 在Y轴方向满足条件后，再考虑X轴方向
                            distance = Mathf.Abs(viewPosition.x - pointViewPos.x);
                        }
                        break;
                }

                // 检查是否在吸附半径内且是最近的点
                if (distance < minDistance && (snapMode is SnapMode.Point ? distance <= actualSnapRadius : true))
                {
                    minDistance = distance;
                    nearestPoint = point;
                    lineName = line.name;
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// 获取最近的数据点
    /// </summary>
    public Vector2? GetNearestDataPoint(Vector2 viewPosition)
    {
        if (TryGetNearestPoint(viewPosition, out Vector2 point, out _))
        {
            return point;
        }
        return null;
    }

    /// <summary>
    /// 获取最近的视图点
    /// </summary>
    public Vector2? GetNearestViewPoint(Vector2 viewPosition)
    {
        if (TryGetNearestPoint(viewPosition, out Vector2 point, out _))
        {
            return DataToView(point);
        }
        return null;
    }

    #endregion

    #region 公共接口方法

    /// <summary>
    /// 设置坐标原点
    /// </summary>
    public void SetOrigin(float? originX, float? originY)
    {
        Vector2 newOrigin = origin;
        if (originX != null) newOrigin.x = originX.Value;
        if (originY != null) newOrigin.y = originY.Value;
        origin = newOrigin;
        _needsViewRecalculation = true;
        _needsLabelsRebuild = true;
    }

    /// <summary>
    /// 设置网格视图间隔（像素）
    /// </summary>
    public void SetGridViewInterval(float? xInterval, float? yInterval)
    {
        if (xInterval != null) gridIntervalX = xInterval.Value;
        if (yInterval != null) gridIntervalY = yInterval.Value;
        _needsGridRebuild = true;
        _needsLabelsRebuild = true;
        _needsDataRebuild = true;   // 坐标转换变了，数据需要重绘
    }

    /// <summary>
    /// 设置网格数据间隔（每个网格表示的数据变化量）
    /// </summary>
    public void SetGridDataInterval(float? xInterval, float? yInterval)
    {
        if (xInterval != null) gridDataIntervalX = xInterval.Value;
        if (yInterval != null) gridDataIntervalY = yInterval.Value;
        _needsGridRebuild = true;   // 虽然网格线位置不变，但标签会变
        _needsLabelsRebuild = true;
        _needsDataRebuild = true;   // 坐标转换变了
    }

    /// <summary>
    /// 添加数据线（自动生成唯一名称）
    /// </summary>
    public void AddLine(LineData lineData)
    {
        if (lineData == null) return;

        // 确保名称唯一
        if (string.IsNullOrEmpty(lineData.name))
        {
            // 自动生成唯一名称
            lineData.name = GenerateUniqueLineName("Line");
        }
        else
        {
            // 检查并处理重复名称 - 运行时抛出异常
            if (lines.Any(l => l.name == lineData.name))
            {
                throw new ArgumentException($"线名 '{lineData.name}' 已存在。请使用唯一名称。");
            }
        }

        lines.Add(lineData);
        _needsDataRebuild = true;
    }

    public void ClearLinePoints(int index)
    {
        lines[index].points.Clear();
        _needsDataRebuild = true;
    }

    /// <summary>
    /// 生成唯一的数据线名称
    /// </summary>
    private string GenerateUniqueLineName(string baseName)
    {
        string newName = $"{baseName}_{_lineNameCounter++}";
        while (lines.Any(l => l.name == newName))
        {
            newName = $"{baseName}_{_lineNameCounter++}";
        }
        return newName;
    }

    /// <summary>
    /// 清除所有数据线
    /// </summary>
    public void ClearLines()
    {
        lines.Clear();
        _needsDataRebuild = true;
    }

    /// <summary>
    /// 设置特定数据线的数据点
    /// </summary>
    public void SetData(string lineName, List<Vector2> points)
    {
        LineData line = lines.Find(l => l.name == lineName);
        if (line != null)
        {
            line.points = points;
            _needsDataRebuild = true;
        }
        else
        {
            // 如果不存在，创建新线并确保名称唯一
            LineData newLine = new LineData();
            newLine.name = lineName;

            // 检查名称唯一性
            if (lines.Any(l => l.name == lineName))
            {
                throw new ArgumentException($"线名 '{lineName}' 已存在。请使用唯一名称。");
            }

            newLine.points = points;
            lines.Add(newLine);
            _needsDataRebuild = true;
        }
    }

    /// <summary>
    /// 设置视图参数
    /// </summary>
    public void SetViewport(Vector2 newOrigin, float viewIntervalX, float viewIntervalY, float dataIntervalX, float dataIntervalY)
    {
        origin = newOrigin;
        SetGridViewInterval(viewIntervalX, viewIntervalY);
        SetGridDataInterval(dataIntervalX, dataIntervalY);
    }

    /// <summary>
    /// 设置视图参数（仅视图间隔，数据间隔保持为1）
    /// </summary>
    public void SetViewport(Vector2 newOrigin, float viewIntervalX, float viewIntervalY)
    {
        SetViewport(newOrigin, viewIntervalX, viewIntervalY, 1f, 1f);
    }

    /// <summary>
    /// 设置X轴标签格式化方法
    /// </summary>
    public void SetXLabelFormatter(Func<float, string> formatter)
    {
        xLabelFormatter = formatter;
        _needsLabelsRebuild = true;
    }

    /// <summary>
    /// 设置Y轴标签格式化方法
    /// </summary>
    public void SetYLabelFormatter(Func<float, string> formatter)
    {
        yLabelFormatter = formatter;
        _needsLabelsRebuild = true;
    }

    #endregion
}