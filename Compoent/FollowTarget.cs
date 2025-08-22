using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 缓动函数类型枚举
public enum EaseType
{
    Linear,
    SmoothStep,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInQuart,
    EaseOutQuart,
    EaseInOutQuart,
    EaseInQuint,
    EaseOutQuint,
    EaseInOutQuint,
    EaseInSine,
    EaseOutSine,
    EaseInOutSine,
    EaseInExpo,
    EaseOutExpo,
    EaseInOutExpo,
    EaseInCirc,
    EaseOutCirc,
    EaseInOutCirc,
    EaseInBack,
    EaseOutBack,
    EaseInOutBack,
    EaseInElastic,
    EaseOutElastic,
    EaseInOutElastic,
    EaseInBounce,
    EaseOutBounce,
    EaseInOutBounce,
    CustomCurve
}

// 更新时机枚举
public enum UpdateTiming
{
    Update,
    LateUpdate,
    FixedUpdate
}

[System.Serializable]
public class FollowSettings
{
    [Header("启用")]
    public bool enabled = true;

    [Header("偏移")]
    [Tooltip("相对于目标值的偏移")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("是否使用本地坐标系偏移")]
    public bool useLocalOffset = false;

    [Header("缓动设置")]
    [Tooltip("缓动函数类型")]
    public EaseType easeType = EaseType.SmoothStep;

    [Tooltip("曲线位置 (控制曲线左右平移)")]
    [Range(-1f, 1f)] public float curvePosition = 0f;

    [Tooltip("曲线强度 (控制曲线的垂直范围)")]
    [Range(0.1f, 5f)] public float intensity = 1f;

    [Tooltip("缓动持续时间(秒)")]
    [Min(0f)] public float duration = 0.5f;

    [Tooltip("自定义缓动曲线(当类型为CustomCurve时使用)")]
    public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("性能优化")]
    [Tooltip("停止阈值 (当变化量小于此值时停止计算)")]
    [Min(0.0001f)] public float stoppingThreshold = 0.01f;

    // 内部状态
    [System.NonSerialized] public float timer;
    [System.NonSerialized] public Vector3 startValue;
    [System.NonSerialized] public Vector3 targetValue;
    [System.NonSerialized] public bool isAnimating;
    [System.NonSerialized] public bool isStopped; // 是否因阈值停止

    // 状态信息
    [System.NonSerialized] public string status;
    [System.NonSerialized] public float currentDistance;

#if UNITY_EDITOR
    [SerializeField, HideInInspector]
    public AnimationCurve previewCurve;
    [System.NonSerialized] public Vector2 previewCurveRange; // 预览曲线的垂直范围
#endif
}

/// <summary>
/// 高级缓动跟随组件 - 支持多种缓动函数和自定义曲线
/// </summary>
[ExecuteInEditMode]
public class FollowTarget : MonoBehaviour
{
    // 阈值常量
    private const float MIN_DURATION_THRESHOLD = 0.0001f; // 最小持续时间阈值
    private const float POSITION_CHANGE_THRESHOLD = 0.001f; // 位置变化阈值
    private const float ROTATION_CHANGE_THRESHOLD = 0.1f; // 旋转变化阈值（角度）
    private const float SCALE_CHANGE_THRESHOLD = 0.001f; // 缩放变化阈值
    private const float EDITOR_DELTA_TIME = 0.0167f; // 编辑器模式下的固定时间增量（约60FPS）
    private const float CURVE_RANGE_MARGIN_MULTIPLIER = 0.1f; // 曲线范围边界的百分比余量
    private const float MIN_CURVE_RANGE_MARGIN = 0.1f; // 曲线范围最小余量

    [Header("全局设置")]
    [Tooltip("在编辑器中预览效果")]
    public bool enableEditorPreview = true;

    [Tooltip("更新时机")]
    public UpdateTiming updateTiming = UpdateTiming.LateUpdate;

    [Space(10)]
    [Header("跟随目标")]
    [Tooltip("要跟随的目标节点")]
    public Transform target;

    [Header("位置跟随")]
    public FollowSettings positionSettings = new FollowSettings
    {
        enabled = true,
        offset = Vector3.zero,
        useLocalOffset = true,
        easeType = EaseType.SmoothStep,
        curvePosition = 0f,
        intensity = 1f,
        duration = 0.3f,
        stoppingThreshold = 0.01f
    };

    [Header("旋转跟随")]
    public FollowSettings rotationSettings = new FollowSettings
    {
        enabled = true,
        offset = Vector3.zero,
        useLocalOffset = false,
        easeType = EaseType.EaseOutBack,
        curvePosition = 0f,
        intensity = 1f,
        duration = 0.4f,
        stoppingThreshold = 0.5f // 角度阈值
    };

    [Header("缩放跟随")]
    public FollowSettings scaleSettings = new FollowSettings
    {
        enabled = false,
        offset = Vector3.one,
        useLocalOffset = false,
        easeType = EaseType.EaseOutElastic,
        curvePosition = 0f,
        intensity = 1f,
        duration = 0.5f,
        stoppingThreshold = 0.01f
    };

    // 内部状态
    private Vector3 _lastTargetPosition;
    private Quaternion _lastTargetRotation;
    private Vector3 _lastTargetScale;
    private bool _initialized;

    [HideInInspector] public float _lastUpdateTime; // 设为public但隐藏

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        // 注册编辑器更新回调
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorUpdate;
            GeneratePreviewCurves();
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        // 注销编辑器更新回调
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorUpdate;
        }
#endif
    }

    void Initialize()
    {
        if (target == null)
        {
            return;
        }

        // 重置所有计时器
        ResetTimers();

        // 记录初始状态
        _lastTargetPosition = GetTargetPosition();
        _lastTargetRotation = GetTargetRotation();
        _lastTargetScale = GetTargetScale();

        // 应用初始状态
        if (positionSettings.enabled)
        {
            transform.position = _lastTargetPosition;
            positionSettings.startValue = _lastTargetPosition;
            positionSettings.targetValue = _lastTargetPosition;
            positionSettings.status = "已初始化";
        }

        if (rotationSettings.enabled)
        {
            transform.rotation = _lastTargetRotation;
            rotationSettings.startValue = _lastTargetRotation.eulerAngles;
            rotationSettings.targetValue = _lastTargetRotation.eulerAngles;
            rotationSettings.status = "已初始化";
        }

        if (scaleSettings.enabled)
        {
            ApplyTargetScale(_lastTargetScale);
            scaleSettings.startValue = transform.lossyScale;
            scaleSettings.targetValue = _lastTargetScale;
            scaleSettings.status = "已初始化";
        }

        _initialized = true;
        _lastUpdateTime = GetCurrentTime();
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (!enableEditorPreview || Application.isPlaying) return;

        // 在编辑模式下更新
        if (positionSettings.enabled) HandlePosition(true);
        if (rotationSettings.enabled) HandleRotation(true);
        if (scaleSettings.enabled) HandleScale(true);

        // 更新编辑器时间戳
        _lastUpdateTime = (float)EditorApplication.timeSinceStartup;

        // 强制场景视图重绘
        //SceneView.RepaintAll();
    }
#endif

    void Update()
    {
        if (updateTiming == UpdateTiming.Update && Application.isPlaying)
        {
            UpdateFollow();
        }
    }

    void LateUpdate()
    {
        if (updateTiming == UpdateTiming.LateUpdate && Application.isPlaying)
        {
            UpdateFollow();
        }
    }

    void FixedUpdate()
    {
        if (updateTiming == UpdateTiming.FixedUpdate && Application.isPlaying)
        {
            UpdateFollow();
        }
    }

    private void UpdateFollow()
    {
        // 运行时更新
        if (target == null || !_initialized) return;

        if (positionSettings.enabled) HandlePosition();
        if (rotationSettings.enabled) HandleRotation();
        if (scaleSettings.enabled) HandleScale();

        // 更新时间戳
        _lastUpdateTime = GetCurrentTime();
    }

#if UNITY_EDITOR
    // 生成预览曲线
    public void GeneratePreviewCurves()
    {
        GeneratePreviewCurve(ref positionSettings);
        GeneratePreviewCurve(ref rotationSettings);
        GeneratePreviewCurve(ref scaleSettings);
    }

    private void GeneratePreviewCurve(ref FollowSettings settings)
    {
        // 如果是自定义曲线类型，直接使用自定义曲线作为预览
        if (settings.easeType == EaseType.CustomCurve)
        {
            settings.previewCurve = new AnimationCurve(settings.customCurve.keys);
            CalculatePreviewCurveRange(ref settings);
            return;
        }

        if (settings.previewCurve == null)
        {
            settings.previewCurve = new AnimationCurve();
        }
        else
        {
            settings.previewCurve.keys = new Keyframe[0];
        }

        const int sampleCount = 100;
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float value = CalculateEase(t, settings.easeType, settings.customCurve, settings.curvePosition, settings.intensity);
            settings.previewCurve.AddKey(new Keyframe(t, value));
        }

        // 平滑曲线
        for (int i = 0; i < settings.previewCurve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(settings.previewCurve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(settings.previewCurve, i, AnimationUtility.TangentMode.ClampedAuto);
        }

        // 计算预览曲线的垂直范围
        CalculatePreviewCurveRange(ref settings);
    }

    // 计算预览曲线的垂直范围
    private void CalculatePreviewCurveRange(ref FollowSettings settings)
    {
        if (settings.previewCurve == null || settings.previewCurve.length == 0)
        {
            settings.previewCurveRange = new Vector2(0f, 1f);
            return;
        }

        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (Keyframe key in settings.previewCurve.keys)
        {
            if (key.value < min) min = key.value;
            if (key.value > max) max = key.value;
        }

        // 添加边距
        float margin = Mathf.Max((max - min) * CURVE_RANGE_MARGIN_MULTIPLIER, MIN_CURVE_RANGE_MARGIN);
        settings.previewCurveRange = new Vector2(min - margin, max + margin);
    }
#endif

    #region 位置跟随
    void HandlePosition(bool isEditor = false)
    {
        if (target == null) return;

        Vector3 targetPosition = GetTargetPosition();
        positionSettings.currentDistance = Vector3.Distance(transform.position, targetPosition);

        // 检查停止阈值
        if (positionSettings.currentDistance <= positionSettings.stoppingThreshold)
        {
            if (positionSettings.isAnimating)
            {
                // 直接设置到目标位置
                transform.position = targetPosition;
                positionSettings.isAnimating = false;
                positionSettings.isStopped = true;
                positionSettings.status = $"已停止 (距离: {positionSettings.currentDistance:F4})";
            }
            else if (positionSettings.isStopped)
            {
                // 保持停止状态
                positionSettings.status = $"已停止 (距离: {positionSettings.currentDistance:F4})";
                return;
            }
        }
        else
        {
            positionSettings.isStopped = false;
        }

        // 检查是否需要重新开始缓动
        if (Vector3.Distance(_lastTargetPosition, targetPosition) > POSITION_CHANGE_THRESHOLD)
        {
            if (!positionSettings.isAnimating || Vector3.Distance(positionSettings.targetValue, targetPosition) > POSITION_CHANGE_THRESHOLD)
            {
                positionSettings.timer = 0f;
                positionSettings.startValue = transform.position;
                positionSettings.targetValue = targetPosition;
                positionSettings.isAnimating = true;
                positionSettings.status = "移动中";
            }
        }
        else if (!positionSettings.isAnimating && !positionSettings.isStopped)
        {
            positionSettings.startValue = transform.position;
            positionSettings.targetValue = targetPosition;
            positionSettings.isAnimating = true;
            positionSettings.timer = 0f;
            positionSettings.status = "移动中";
        }

        // 更新缓动
        if (positionSettings.isAnimating)
        {
            // 检查duration是否为0
            if (positionSettings.duration <= MIN_DURATION_THRESHOLD)
            {
                transform.position = positionSettings.targetValue;
                positionSettings.isAnimating = false;
                positionSettings.status = "立即完成";
            }
            else
            {
                Vector3 newPosition = transform.position;
                UpdateEasing(positionSettings, ref newPosition, isEditor);
                transform.position = newPosition;

                // 更新状态
                positionSettings.currentDistance = Vector3.Distance(newPosition, targetPosition);
                positionSettings.status = $"移动中 (距离: {positionSettings.currentDistance:F4})";
            }
        }

        _lastTargetPosition = targetPosition;
    }

    Vector3 GetTargetPosition()
    {
        if (target == null)
        {
            return transform.position;
        }

        Vector3 offset = positionSettings.offset;

        if (positionSettings.useLocalOffset)
        {
            return target.TransformPoint(offset);
        }
        else
        {
            return target.position + offset;
        }
    }
    #endregion

    #region 旋转跟随
    void HandleRotation(bool isEditor = false)
    {
        if (target == null) return;

        Quaternion targetRotation = GetTargetRotation();
        float angleDifference = Quaternion.Angle(transform.rotation, targetRotation);
        rotationSettings.currentDistance = angleDifference;

        // 检查停止阈值
        if (angleDifference <= rotationSettings.stoppingThreshold)
        {
            if (rotationSettings.isAnimating)
            {
                // 直接设置到目标旋转
                transform.rotation = targetRotation;
                rotationSettings.isAnimating = false;
                rotationSettings.isStopped = true;
                rotationSettings.status = $"已停止 (角度差: {angleDifference:F2}°)";
            }
            else if (rotationSettings.isStopped)
            {
                // 保持停止状态
                rotationSettings.status = $"已停止 (角度差: {angleDifference:F2}°)";
                return;
            }
        }
        else
        {
            rotationSettings.isStopped = false;
        }

        // 检查是否需要重新开始缓动
        if (Quaternion.Angle(_lastTargetRotation, targetRotation) > ROTATION_CHANGE_THRESHOLD)
        {
            if (!rotationSettings.isAnimating || Quaternion.Angle(Quaternion.Euler(rotationSettings.targetValue), targetRotation) > ROTATION_CHANGE_THRESHOLD)
            {
                rotationSettings.timer = 0f;
                rotationSettings.startValue = transform.rotation.eulerAngles;
                rotationSettings.targetValue = targetRotation.eulerAngles;
                rotationSettings.isAnimating = true;
                rotationSettings.status = "旋转中";
            }
        }
        else if (!rotationSettings.isAnimating && !rotationSettings.isStopped)
        {
            rotationSettings.startValue = transform.rotation.eulerAngles;
            rotationSettings.targetValue = targetRotation.eulerAngles;
            rotationSettings.isAnimating = true;
            rotationSettings.timer = 0f;
            rotationSettings.status = "旋转中";
        }

        // 更新缓动
        if (rotationSettings.isAnimating)
        {
            // 检查duration是否为0
            if (rotationSettings.duration <= MIN_DURATION_THRESHOLD)
            {
                transform.rotation = Quaternion.Euler(rotationSettings.targetValue);
                rotationSettings.isAnimating = false;
                rotationSettings.status = "立即完成";
            }
            else
            {
                Vector3 newEuler = transform.rotation.eulerAngles;
                UpdateEasing(rotationSettings, ref newEuler, isEditor);

                // 处理角度环绕问题 (0-360)
                newEuler = NormalizeEulerAngles(newEuler);

                transform.rotation = Quaternion.Euler(newEuler);

                // 更新状态
                float currentAngle = Quaternion.Angle(transform.rotation, targetRotation);
                rotationSettings.currentDistance = currentAngle;
                rotationSettings.status = $"旋转中 (角度差: {currentAngle:F2}°)";
            }
        }

        _lastTargetRotation = targetRotation;
    }

    Quaternion GetTargetRotation()
    {
        if (target == null)
        {
            return transform.rotation;
        }

        Quaternion offsetRotation = Quaternion.Euler(rotationSettings.offset);

        if (rotationSettings.useLocalOffset)
        {
            return target.rotation * offsetRotation;
        }
        else
        {
            return offsetRotation * target.rotation;
        }
    }

    Vector3 NormalizeEulerAngles(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z)
        );
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0) angle += 360f;
        return angle;
    }
    #endregion

    #region 缩放跟随
    void HandleScale(bool isEditor = false)
    {
        if (target == null) return;

        Vector3 targetScale = GetTargetScale();
        float scaleDifference = Vector3.Distance(transform.lossyScale, targetScale);
        scaleSettings.currentDistance = scaleDifference;

        // 检查停止阈值
        if (scaleDifference <= scaleSettings.stoppingThreshold)
        {
            if (scaleSettings.isAnimating)
            {
                // 直接设置到目标缩放
                ApplyTargetScale(targetScale);
                scaleSettings.isAnimating = false;
                scaleSettings.isStopped = true;
                scaleSettings.status = $"已停止 (差值: {scaleDifference:F4})";
            }
            else if (scaleSettings.isStopped)
            {
                // 保持停止状态
                scaleSettings.status = $"已停止 (差值: {scaleDifference:F4})";
                return;
            }
        }
        else
        {
            scaleSettings.isStopped = false;
        }

        // 检查是否需要重新开始缓动
        if (Vector3.Distance(_lastTargetScale, targetScale) > SCALE_CHANGE_THRESHOLD)
        {
            if (!scaleSettings.isAnimating || Vector3.Distance(scaleSettings.targetValue, targetScale) > SCALE_CHANGE_THRESHOLD)
            {
                scaleSettings.timer = 0f;
                scaleSettings.startValue = transform.lossyScale;
                scaleSettings.targetValue = targetScale;
                scaleSettings.isAnimating = true;
                scaleSettings.status = "缩放中";
            }
        }
        else if (!scaleSettings.isAnimating && !scaleSettings.isStopped)
        {
            scaleSettings.startValue = transform.lossyScale;
            scaleSettings.targetValue = targetScale;
            scaleSettings.isAnimating = true;
            scaleSettings.timer = 0f;
            scaleSettings.status = "缩放中";
        }

        // 更新缓动
        if (scaleSettings.isAnimating)
        {
            // 检查duration是否为0
            if (scaleSettings.duration <= MIN_DURATION_THRESHOLD)
            {
                ApplyTargetScale(scaleSettings.targetValue);
                scaleSettings.isAnimating = false;
                scaleSettings.status = "立即完成";
            }
            else
            {
                Vector3 newWorldScale = transform.lossyScale;
                UpdateEasing(scaleSettings, ref newWorldScale, isEditor);
                ApplyTargetScale(newWorldScale);

                // 更新状态
                float currentDiff = Vector3.Distance(newWorldScale, targetScale);
                scaleSettings.currentDistance = currentDiff;
                scaleSettings.status = $"缩放中 (差值: {currentDiff:F4})";
            }
        }

        _lastTargetScale = targetScale;
    }

    Vector3 GetTargetScale()
    {
        if (target == null)
        {
            return transform.lossyScale;
        }

        Vector3 baseScale = target.lossyScale;
        Vector3 offset = scaleSettings.offset;

        if (scaleSettings.useLocalOffset)
        {
            return new Vector3(
                baseScale.x * offset.x,
                baseScale.y * offset.y,
                baseScale.z * offset.z
            );
        }
        else
        {
            return baseScale + offset;
        }
    }

    void ApplyTargetScale(Vector3 targetWorldScale)
    {
        if (transform.parent == null)
        {
            transform.localScale = targetWorldScale;
        }
        else
        {
            Vector3 parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                parentScale.x != 0 ? targetWorldScale.x / parentScale.x : targetWorldScale.x,
                parentScale.y != 0 ? targetWorldScale.y / parentScale.y : targetWorldScale.y,
                parentScale.z != 0 ? targetWorldScale.z / parentScale.z : targetWorldScale.z
            );
        }
    }
    #endregion

    #region 缓动核心函数
    void UpdateEasing(FollowSettings settings, ref Vector3 currentValue, bool isEditor = false)
    {
        // 检查持续时间是否为0
        if (settings.duration <= MIN_DURATION_THRESHOLD)
        {
            currentValue = settings.targetValue;
            settings.isAnimating = false;
            settings.timer = 0f;
            settings.isStopped = false;
            return;
        }

        // 获取时间增量
        float deltaTime = GetDeltaTime(isEditor);

        settings.timer += deltaTime;

        // 计算插值比例 (0-1)
        float t = Mathf.Clamp01(settings.timer / settings.duration);
        float easedT = CalculateEase(t, settings.easeType, settings.customCurve, settings.curvePosition, settings.intensity);

        // 应用插值
        currentValue = Vector3.Lerp(settings.startValue, settings.targetValue, easedT);

        // 检查是否完成
        if (t >= 1f)
        {
            settings.isAnimating = false;
            settings.timer = 0f;
        }
    }

    float GetDeltaTime(bool isEditor)
    {
        if (isEditor)
        {
            // 在编辑器模式下使用固定时间步长
            return EDITOR_DELTA_TIME;
        }

        if (updateTiming == UpdateTiming.FixedUpdate)
        {
            return Time.fixedDeltaTime;
        }

        return Time.deltaTime;
    }

    float GetCurrentTime()
    {
        if (updateTiming == UpdateTiming.FixedUpdate)
        {
            return Time.fixedTime;
        }

        return Time.time;
    }

    float CalculateEase(float t, EaseType easeType, AnimationCurve customCurve, float curvePosition = 0f, float intensity = 1f)
    {
        // 应用曲线位置偏移
        t = Mathf.Clamp01(t + curvePosition * 0.5f);

        float result = t;

        switch (easeType)
        {
            case EaseType.Linear:
                // 线性函数不受强度影响
                break;

            case EaseType.SmoothStep:
                result = t * t * (3f - 2f * t);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 二次缓动
            case EaseType.EaseInQuad:
                result = t * t;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutQuad:
                result = 1f - (1f - t) * (1f - t);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutQuad:
                result = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 三次缓动
            case EaseType.EaseInCubic:
                result = t * t * t;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutCubic:
                result = 1f - Mathf.Pow(1f - t, 3f);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutCubic:
                result = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 四次缓动
            case EaseType.EaseInQuart:
                result = t * t * t * t;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutQuart:
                result = 1f - Mathf.Pow(1f - t, 4f);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutQuart:
                result = t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 五次缓动
            case EaseType.EaseInQuint:
                result = t * t * t * t * t;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutQuint:
                result = 1f - Mathf.Pow(1f - t, 5f);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutQuint:
                result = t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 正弦缓动
            case EaseType.EaseInSine:
                result = 1f - Mathf.Cos((t * Mathf.PI) / 2f);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutSine:
                result = Mathf.Sin((t * Mathf.PI) / 2f);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutSine:
                result = -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 指数缓动
            case EaseType.EaseInExpo:
                result = t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutExpo:
                result = t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutExpo:
                result = t == 0f ? 0f :
                       t == 1f ? 1f :
                       t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f :
                       (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 圆形缓动
            case EaseType.EaseInCirc:
                result = 1f - Mathf.Sqrt(1f - Mathf.Pow(t, 2f));
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutCirc:
                result = Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutCirc:
                result = t < 0.5f
                    ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f
                    : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 回弹缓动
            case EaseType.EaseInBack:
                {
                    float c1 = 1.70158f * intensity; // 强度影响回弹幅度
                    result = (c1 + 1f) * t * t * t - c1 * t * t;
                }
                break;
            case EaseType.EaseOutBack:
                {
                    float c1 = 1.70158f * intensity; // 强度影响回弹幅度
                    float c3 = c1 + 1f;
                    result = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                break;
            case EaseType.EaseInOutBack:
                {
                    float c1 = 1.70158f * intensity; // 强度影响回弹幅度
                    float c2 = c1 * 1.525f;

                    result = t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
                }
                break;

            // 弹性缓动
            case EaseType.EaseInElastic:
                {
                    float c4 = (2f * Mathf.PI) / 3f;
                    // 强度影响振幅
                    float amplitude = intensity;
                    result = t == 0f ? 0f :
                           t == 1f ? 1f :
                           -amplitude * Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
                }
                break;
            case EaseType.EaseOutElastic:
                {
                    float c4 = (2f * Mathf.PI) / 3f;
                    // 强度影响振幅
                    float amplitude = intensity;
                    result = t == 0f ? 0f :
                           t == 1f ? 1f :
                           amplitude * Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                break;
            case EaseType.EaseInOutElastic:
                {
                    float c5 = (2f * Mathf.PI) / 4.5f;
                    // 强度影响振幅
                    float amplitude = intensity;
                    result = t == 0f ? 0f :
                           t == 1f ? 1f :
                           t < 0.5f
                               ? -(amplitude * Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f
                               : amplitude * Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * c5) / 2f + 1f;
                }
                break;

            // 反弹缓动
            case EaseType.EaseInBounce:
                result = 1f - CalculateBounceOut(1f - t);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseOutBounce:
                result = CalculateBounceOut(t);
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;
            case EaseType.EaseInOutBounce:
                result = t < 0.5f
                    ? (1f - CalculateBounceOut(1f - 2f * t)) / 2f
                    : (1f + CalculateBounceOut(2f * t - 1f)) / 2f;
                // 应用强度
                result = Mathf.Pow(result, intensity);
                break;

            // 自定义曲线
            case EaseType.CustomCurve:
                result = customCurve.Evaluate(t);
                break;

            default:
                result = t;
                break;
        }

        return result;
    }

    float CalculateBounceOut(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2f / d1)
        {
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        }
        else
        {
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }
    #endregion

    #region 公共方法
    /// <summary>
    /// 设置新的跟随目标
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        Initialize();
    }

    /// <summary>
    /// 立即跳跃到目标位置
    /// </summary>
    public void JumpToTarget()
    {
        if (target == null) return;

        if (positionSettings.enabled)
        {
            transform.position = GetTargetPosition();
            positionSettings.startValue = transform.position;
            positionSettings.targetValue = transform.position;
            positionSettings.isAnimating = false;
            positionSettings.isStopped = false;
        }

        if (rotationSettings.enabled)
        {
            transform.rotation = GetTargetRotation();
            rotationSettings.startValue = transform.rotation.eulerAngles;
            rotationSettings.targetValue = transform.rotation.eulerAngles;
            rotationSettings.isAnimating = false;
            rotationSettings.isStopped = false;
        }

        if (scaleSettings.enabled)
        {
            ApplyTargetScale(GetTargetScale());
            scaleSettings.startValue = transform.lossyScale;
            scaleSettings.targetValue = transform.lossyScale;
            scaleSettings.isAnimating = false;
            scaleSettings.isStopped = false;
        }

        ResetTimers();
    }

    /// <summary>
    /// 重置所有缓动计时器 - 停止当前动画并重置状态
    /// </summary>
    public void ResetTimers()
    {
        positionSettings.timer = 0f;
        rotationSettings.timer = 0f;
        scaleSettings.timer = 0f;

        positionSettings.isAnimating = false;
        rotationSettings.isAnimating = false;
        scaleSettings.isAnimating = false;

        positionSettings.isStopped = false;
        rotationSettings.isStopped = false;
        scaleSettings.isStopped = false;

        // 重置状态
        if (positionSettings.enabled) positionSettings.status = "已重置";
        if (rotationSettings.enabled) rotationSettings.status = "已重置";
        if (scaleSettings.enabled) scaleSettings.status = "已重置";
    }

    /// <summary>
    /// 强制重新开始缓动动画 - 从当前位置重新开始缓动到目标位置
    /// </summary>
    public void RestartEasing()
    {
        if (target == null) return;

        if (positionSettings.enabled)
        {
            positionSettings.startValue = transform.position;
            positionSettings.targetValue = GetTargetPosition();
            positionSettings.timer = 0f;
            positionSettings.isAnimating = true;
            positionSettings.isStopped = false;
            positionSettings.status = "移动中";
        }

        if (rotationSettings.enabled)
        {
            rotationSettings.startValue = transform.rotation.eulerAngles;
            rotationSettings.targetValue = GetTargetRotation().eulerAngles;
            rotationSettings.timer = 0f;
            rotationSettings.isAnimating = true;
            rotationSettings.isStopped = false;
            rotationSettings.status = "旋转中";
        }

        if (scaleSettings.enabled)
        {
            scaleSettings.startValue = transform.lossyScale;
            scaleSettings.targetValue = GetTargetScale();
            scaleSettings.timer = 0f;
            scaleSettings.isAnimating = true;
            scaleSettings.isStopped = false;
            scaleSettings.status = "缩放中";
        }
    }
    #endregion

    #region 编辑器辅助
#if UNITY_EDITOR
    void OnValidate()
    {
        // 确保自定义曲线有效
        if (positionSettings.customCurve == null || positionSettings.customCurve.length < 2)
            positionSettings.customCurve = AnimationCurve.Linear(0, 0, 1, 1);

        if (rotationSettings.customCurve == null || rotationSettings.customCurve.length < 2)
            rotationSettings.customCurve = AnimationCurve.Linear(0, 0, 1, 1);

        if (scaleSettings.customCurve == null || scaleSettings.customCurve.length < 2)
            scaleSettings.customCurve = AnimationCurve.Linear(0, 0, 1, 1);

        // 生成预览曲线
        GeneratePreviewCurves();

        // 在编辑器预览模式下，如果参数改变则重置状态
        if (!Application.isPlaying && enableEditorPreview)
        {
            Initialize();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (target == null || !enableEditorPreview) return;

        // 绘制位置偏移
        if (positionSettings.enabled)
        {
            Vector3 targetPos = GetTargetPosition();

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.1f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPos, 0.15f);
            Gizmos.DrawLine(target.position, targetPos);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(targetPos, transform.position);

            // 绘制停止阈值
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(targetPos, positionSettings.stoppingThreshold);
        }

        // 绘制旋转偏移
        if (rotationSettings.enabled && rotationSettings.offset != Vector3.zero)
        {
            Handles.color = new Color(1, 0.5f, 0, 0.5f);
            Quaternion baseRotation = target.rotation;
            Quaternion offsetRotation = GetTargetRotation();

            Vector3 pos = target.position;
            Handles.ArrowHandleCap(0, pos, baseRotation, 1f, EventType.Repaint);
            Handles.ArrowHandleCap(0, pos, offsetRotation, 1.2f, EventType.Repaint);

            // 绘制旋转差
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Handles.DrawDottedLine(transform.position, target.position, 2f);
        }

        // 绘制缩放指示
        if (scaleSettings.enabled)
        {
            Vector3 targetScale = GetTargetScale();
            Vector3 currentScale = transform.lossyScale;

            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.5f);
            Gizmos.DrawWireCube(target.position, targetScale);

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(transform.position, currentScale);
        }
    }
#endif
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(FollowTarget))]
public class FollowTargetEditor : Editor
{
    private bool showPositionSettings = true;
    private bool showRotationSettings = true;
    private bool showScaleSettings = true;
    private bool showStateInfo = true;

    public override void OnInspectorGUI()
    {
        FollowTarget follower = (FollowTarget)target;

        // 绘制默认检查器
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("控制", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("跳转到目标位置"))
        {
            follower.JumpToTarget();
        }

        if (GUILayout.Button("重置缓动"))
        {
            follower.ResetTimers();
        }

        if (GUILayout.Button("重新开始缓动"))
        {
            follower.RestartEasing();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("状态信息", EditorStyles.boldLabel);

        showStateInfo = EditorGUILayout.Foldout(showStateInfo, "当前状态");
        if (showStateInfo)
        {
            // 位置状态
            if (follower.positionSettings.enabled)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("位置:", GUILayout.Width(60));
                EditorGUILayout.LabelField(follower.positionSettings.status ?? "未活动");
                EditorGUILayout.EndHorizontal();
            }

            // 旋转状态
            if (follower.rotationSettings.enabled)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("旋转:", GUILayout.Width(60));
                EditorGUILayout.LabelField(follower.rotationSettings.status ?? "未活动");
                EditorGUILayout.EndHorizontal();
            }

            // 缩放状态
            if (follower.scaleSettings.enabled)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("缩放:", GUILayout.Width(60));
                EditorGUILayout.LabelField(follower.scaleSettings.status ?? "未活动");
                EditorGUILayout.EndHorizontal();
            }

            // 时间信息
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("更新时间:", GUILayout.Width(80));
            float timeSinceUpdate = Application.isPlaying ?
                Time.time - follower._lastUpdateTime :
                (float)EditorApplication.timeSinceStartup - follower._lastUpdateTime;
            EditorGUILayout.LabelField($"{timeSinceUpdate / 1000:F2}毫秒前");
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("预览曲线", EditorStyles.boldLabel);

        // 位置曲线预览
        if (follower.positionSettings.enabled)
        {
            showPositionSettings = EditorGUILayout.Foldout(showPositionSettings, "位置缓动曲线");
            if (showPositionSettings)
            {
                Vector2 range = follower.positionSettings.previewCurveRange;
                Rect curveRect = new Rect(0, range.x, 1, range.y - range.x);
                EditorGUILayout.CurveField(follower.positionSettings.previewCurve,
                    Color.green,
                    curveRect,
                    GUILayout.Height(100));
            }
        }

        // 旋转曲线预览
        if (follower.rotationSettings.enabled)
        {
            showRotationSettings = EditorGUILayout.Foldout(showRotationSettings, "旋转缓动曲线");
            if (showRotationSettings)
            {
                Vector2 range = follower.rotationSettings.previewCurveRange;
                Rect curveRect = new Rect(0, range.x, 1, range.y - range.x);
                EditorGUILayout.CurveField(follower.rotationSettings.previewCurve,
                    new Color(1f, 0.5f, 0f),
                    curveRect,
                    GUILayout.Height(100));
            }
        }

        // 缩放缓动曲线
        if (follower.scaleSettings.enabled)
        {
            showScaleSettings = EditorGUILayout.Foldout(showScaleSettings, "缩放缓动曲线");
            if (showScaleSettings)
            {
                Vector2 range = follower.scaleSettings.previewCurveRange;
                Rect curveRect = new Rect(0, range.x, 1, range.y - range.x);
                EditorGUILayout.CurveField(follower.scaleSettings.previewCurve,
                    new Color(0.8f, 0.2f, 0.8f),
                    curveRect,
                    GUILayout.Height(100));
            }
        }

        EditorGUILayout.Space();

        // 预览状态信息
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("运行中 - 预览功能已禁用", MessageType.Info);
        }
        else
        {
            if (follower.enableEditorPreview)
            {
                EditorGUILayout.HelpBox("编辑器预览已启用", MessageType.Info);

                if (follower.target == null)
                {
                    EditorGUILayout.HelpBox("警告: 未设置跟随目标!", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("编辑器预览已禁用", MessageType.Info);
            }
        }

        // 如果曲线设置改变，重新生成预览曲线
        if (GUI.changed)
        {
            follower.GeneratePreviewCurves();
        }
    }
}
#endif