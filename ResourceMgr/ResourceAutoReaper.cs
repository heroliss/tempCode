using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityGameFramework.Runtime;

namespace Ddz.ResourceMgr
{
    /// <summary>
    /// 资源自动释放管理器
    /// <para>
    /// 使用注意事项：
    /// 1. 自定义清理操作应幂等（可安全多次调用）
    /// 2. 避免在清理操作中引用目标对象（会丢失引用）
    /// </para>
    /// <para>
    /// 设计思路：
    /// 1. 弱引用管理：使用ConditionalWeakTable确保不阻止托管对象被GC回收
    /// 2. 双重清理机制：
    ///    - 被动清理：托管对象被回收时自动触发清理
    ///    - 主动清理：通过Dispose()显式释放所有资源
    /// 3. 幂等性：Dispose()可安全多次调用
    /// 4. 安全隔离：收集-清理分离模式确保操作安全
    /// </para>
    /// <para>
    /// 推荐使用场景：
    /// 1. 游戏对象资源管理：GameObject销毁时自动释放关联资源
    /// 2. 脚本生命周期管理：MonoBehaviour销毁时释放非托管资源
    /// 3. 临时资源托管：确保遗漏的资源最终被释放
    /// 4. 复杂对象图管理：自动处理对象关联的多个资源
    ///    (它解决的核心问题是：当根对象被回收时，
    ///    如何自动清理其引用的所有子资源，
    ///    即使这些资源分布在多层嵌套结构中)
    /// </para>
    /// </summary>
    public class ResourceAutoReaper : IDisposable
    {
        /// <summary>
        /// 对象与终结器的弱引用映射表
        /// <para>当键对象被GC回收时，值条目自动移除</para>
        /// </summary>
        private readonly ConditionalWeakTable<object, ResourceFinalizer> _objectFinalizers = new();

        /// <summary>
        /// 添加可释放对象
        /// </summary>
        public void Add(IDisposable disposable)
        {
            if (disposable == null) throw new ArgumentNullException(nameof(disposable));
            AddInternal(disposable, disposable.Dispose);
        }

        /// <summary>
        /// 添加对象并指定清理操作
        /// </summary>
        public void Add(object target, Action cleanupAction)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (cleanupAction == null) throw new ArgumentNullException(nameof(cleanupAction));
            AddInternal(target, cleanupAction);
        }

        private void AddInternal(object target, Action cleanupAction)
        {
            // 避免重复注册同一对象
            if (!_objectFinalizers.TryGetValue(target, out _))
            {
                _objectFinalizers.Add(target, new ResourceFinalizer(cleanupAction));
            }
        }

        /// <summary>
        /// 取消对象托管
        /// </summary>
        public bool Remove(object target)
        {
            if (target == null)
                return false;

            return _objectFinalizers.Remove(target);
        }

        /// <summary>
        /// 释放所有托管资源（幂等操作）
        /// <para>关键步骤：</para>
        /// <para>1. 收集所有终结器到临时列表</para>
        /// <para>2. 清空原始映射表</para>
        /// <para>3. 执行清理操作</para>
        /// </summary>
        public void Dispose()
        {
            // 步骤1: 收集所有终结器
            // 此操作快速且安全，避免在清理过程中修改集合
            var finalizers = new List<ResourceFinalizer>();
            foreach (var pair in _objectFinalizers)
            {
                finalizers.Add(pair.Value);
            }

            // 步骤2: 立即清空映射表
            // 表明资源管理器已放弃所有权，防止后续访问
            _objectFinalizers.Clear();

            // 步骤3: 执行实际清理操作
            // 此时集合已清空，清理操作可安全执行
            foreach (var finalizer in finalizers)
            {
                try
                {
                    finalizer.ExecuteCleanup();
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断其他资源释放
                    BoloLog.LogError($"[ResourceAutoReaper] 资源释放异常: {ex}");
                }
            }
        }

        /// <summary>
        /// 资源终结器 - 封装清理操作
        /// </summary>
        private sealed class ResourceFinalizer
        {
            private Action _cleanupAction;

            public ResourceFinalizer(Action cleanupAction)
            {
                _cleanupAction = cleanupAction;
            }

            /// <summary>
            /// 执行资源清理（幂等操作）
            /// </summary>
            public void ExecuteCleanup()
            {
                var action = _cleanupAction;
                if (action == null)
                    return;

                // 清理前先置空，确保幂等性
                _cleanupAction = null;

                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    // 记录但不重新抛出，保持清理流程稳定
                    BoloLog.LogError($"[ResourceFinalizer] 清理操作异常: {ex}");
                }
            }

            ~ResourceFinalizer()
            {
                ExecuteCleanup();
            }
        }
    }
}