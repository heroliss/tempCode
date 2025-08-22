using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ddz.BoloExtensions.EventMgr
{
    /// <summary>
    /// 事件标记接口（所有自定义事件类型必须实现此接口）
    /// </summary>
    public interface IEvent { }

    /// <summary>
    /// 订阅令牌（用于取消事件订阅）
    /// <para>实现 IDisposable 接口，应在不再需要时调用 Dispose() 方法</para>
    /// </summary>
    public sealed class Unsubscriber : IDisposable
    {
        private Action _unsubscribeAction;

        /// <summary>
        /// 创建反订阅器实例
        /// </summary>
        /// <param name="unsubscribeAction">取消订阅时执行的操作</param>
        public Unsubscriber(Action unsubscribeAction) =>
            _unsubscribeAction = unsubscribeAction;

        /// <summary>
        /// 执行取消订阅操作
        /// <para>此方法可安全多次调用，仅第一次调用有效</para>
        /// </summary>
        public void Dispose()
        {
            if (_unsubscribeAction == null) return;

            // 执行取消订阅操作
            _unsubscribeAction.Invoke();

            // 清除引用，防止重复执行
            _unsubscribeAction = null;
        }
    }

    /// <summary>
    /// 事件管理器核心类
    /// <para>设计特点：</para>
    /// <list type="bullet">
    ///   <item>类型安全的事件系统</item>
    ///   <item>高效的事件订阅与触发</item>
    ///   <item>禁止重复订阅</item>
    /// </list>
    /// </summary>
    public partial class EventMgr
    {
        /// <summary>
        /// 事件处理器存储结构（泛型）
        /// <para>为每个事件类型提供独立的处理器存储</para>
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        private class EventHandlers<T> where T : IEvent
        {
            /// <summary>
            /// 无参数事件处理器链表
            /// <para>使用链表确保高效添加/移除操作</para>
            /// </summary>
            public LinkedList<Action> NoParamHandlers = new();

            /// <summary>
            /// 带参数事件处理器链表
            /// </summary>
            public LinkedList<Action<T>> ParamHandlers = new();

            /// <summary>
            /// 无参数委托到节点的映射
            /// <para>用于快速取消订阅</para>
            /// </summary>
            public Dictionary<Delegate, LinkedListNode<Action>> NoParamNodes = new();

            /// <summary>
            /// 带参数委托到节点的映射
            /// <para>用于快速取消订阅</para>
            /// </summary>
            public Dictionary<Delegate, LinkedListNode<Action<T>>> ParamNodes = new();
        }

        /// <summary>
        /// 事件订阅主存储
        /// <para>Key: 事件类型 (Type)</para>
        /// <para>Value: 该类型的事件处理器集合</para>
        /// </summary>
        private readonly Dictionary<Type, object> _typeHandlers = new();

        #region 事件订阅

        /// <summary>
        /// 订阅事件（无参数版本）
        /// </summary>
        /// <typeparam name="T">事件类型（必须实现IEvent接口）</typeparam>
        /// <param name="handler">事件处理程序</param>
        /// <returns>反订阅令牌，用于取消订阅</returns>
        /// <exception cref="ArgumentNullException">handler为null时记录警告</exception>
        public Unsubscriber On<T>(Action handler) where T : IEvent
        {
            // 空处理器检查
            if (handler == null)
            {
                LogWarning("订阅事件时传入空处理器");
                return new Unsubscriber(() => { });
            }

            // 获取或创建事件处理器集合
            var handlers = GetOrCreateHandlers<T>();

            // 检查重复订阅
            if (handlers.NoParamNodes.ContainsKey(handler))
            {
                LogWarning($"重复订阅: {typeof(T).Name} - {handler.Method.Name}");
                return new Unsubscriber(() => { });
            }

            // 添加到链表并记录节点
            var node = handlers.NoParamHandlers.AddLast(handler);
            handlers.NoParamNodes[handler] = node;

            // 返回带取消订阅操作的令牌
            return new Unsubscriber(() => Off<T>(handler));
        }

        /// <summary>
        /// 订阅事件（带参数版本）
        /// </summary>
        /// <typeparam name="T">事件类型（必须实现IEvent接口）</typeparam>
        /// <param name="handler">事件处理程序</param>
        /// <returns>反订阅令牌，用于取消订阅</returns>
        /// <exception cref="ArgumentNullException">handler为null时记录警告</exception>
        public Unsubscriber On<T>(Action<T> handler) where T : IEvent
        {
            // 空处理器检查
            if (handler == null)
            {
                LogWarning("订阅事件时传入空处理器");
                return new Unsubscriber(() => { });
            }

            // 获取或创建事件处理器集合
            var handlers = GetOrCreateHandlers<T>();

            // 检查重复订阅
            if (handlers.ParamNodes.ContainsKey(handler))
            {
                LogWarning($"重复订阅: {typeof(T).Name} - {handler.Method.Name}");
                return new Unsubscriber(() => { });
            }

            // 添加到链表并记录节点
            var node = handlers.ParamHandlers.AddLast(handler);
            handlers.ParamNodes[handler] = node;

            // 返回带取消订阅操作的令牌
            return new Unsubscriber(() => Off<T>(handler));
        }

        /// <summary>
        /// 获取或创建指定事件类型的处理器集合
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <returns>事件处理器集合</returns>
        private EventHandlers<T> GetOrCreateHandlers<T>() where T : IEvent
        {
            Type eventType = typeof(T);

            // 检查是否已有该类型的处理器
            if (!_typeHandlers.TryGetValue(eventType, out var handlersObj))
            {
                // 创建新的事件处理器集合
                var newHandlers = new EventHandlers<T>();
                _typeHandlers[eventType] = newHandlers;
                return newHandlers;
            }

            // 返回现有处理器集合
            return (EventHandlers<T>)handlersObj;
        }

        /// <summary>
        /// 尝试清理空的事件处理器集合
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handlers">事件处理器集合</param>
        private void TryCleanupHandlers<T>(EventHandlers<T> handlers) where T : IEvent
        {
            // 当无任何订阅者时，从主存储中移除
            if (handlers.NoParamHandlers.Count == 0 &&
                handlers.ParamHandlers.Count == 0)
            {
                _typeHandlers.Remove(typeof(T));
            }
        }

        #endregion

        #region 事件取消订阅

        /// <summary>
        /// 取消订阅事件（无参数版本）
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">要取消的事件处理程序</param>
        public void Off<T>(Action handler) where T : IEvent
        {
            if (handler == null) return;

            Type eventType = typeof(T);

            // 检查是否存在该事件类型的处理器
            if (!_typeHandlers.TryGetValue(eventType, out var handlersObj)) return;

            var handlers = (EventHandlers<T>)handlersObj;

            // 从映射中获取节点并移除
            if (handlers.NoParamNodes.TryGetValue(handler, out var node))
            {
                handlers.NoParamHandlers.Remove(node);
                handlers.NoParamNodes.Remove(handler);

                // 检查是否需要清理
                TryCleanupHandlers(handlers);
            }
        }

        /// <summary>
        /// 取消订阅事件（带参数版本）
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">要取消的事件处理程序</param>
        public void Off<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null) return;

            Type eventType = typeof(T);

            // 检查是否存在该事件类型的处理器
            if (!_typeHandlers.TryGetValue(eventType, out var handlersObj)) return;

            var handlers = (EventHandlers<T>)handlersObj;

            // 从映射中获取节点并移除
            if (handlers.ParamNodes.TryGetValue(handler, out var node))
            {
                handlers.ParamHandlers.Remove(node);
                handlers.ParamNodes.Remove(handler);

                // 检查是否需要清理
                TryCleanupHandlers(handlers);
            }
        }

        /// <summary>
        /// 移除所有事件的所有订阅
        /// <para>清空整个事件系统</para>
        /// </summary>
        public void OffAllTypeEvent() => _typeHandlers.Clear();

        #endregion

        #region 事件触发

        /// <summary>
        /// 触发指定类型的事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据（默认为default）</param>
        public void Emit<T>(T eventData = default) where T : IEvent
        {
            Type eventType = typeof(T);

            // 检查是否有订阅者
            if (!_typeHandlers.TryGetValue(eventType, out var handlersObj))
            {
                // 调试模式下记录无订阅者日志
#if EVENT_MGR_DEBUG
                LogMessage($"[事件系统] 事件触发但无订阅者: {eventType.Name}");
#endif
                return;
            }

            var handlers = (EventHandlers<T>)handlersObj;

            // 处理无参数订阅者
            if (handlers.NoParamHandlers.Count > 0)
            {
                ExecuteHandlers(handlers.NoParamHandlers);
            }

            // 处理带参数订阅者
            if (handlers.ParamHandlers.Count > 0)
            {
                ExecuteHandlers(handlers.ParamHandlers, eventData);
            }
        }

        /// <summary>
        /// 执行无参数事件处理器
        /// </summary>
        /// <param name="handlers">处理器链表</param>
        private void ExecuteHandlers(LinkedList<Action> handlers)
        {
            var node = handlers.First;
            while (node != null)
            {
                var next = node.Next; // 预先获取下一个节点
                try
                {
                    // 安全调用处理器
                    node.Value?.Invoke();
                }
                catch (Exception ex)
                {
                    // 捕获并记录异常，不影响其他处理器
                    LogError($"无参数事件处理失败: {ex}");
                }
                node = next;
            }
        }

        /// <summary>
        /// 执行带参数事件处理器
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handlers">处理器链表</param>
        /// <param name="eventData">事件数据</param>
        private void ExecuteHandlers<T>(LinkedList<Action<T>> handlers, T eventData)
        {
            var node = handlers.First;
            while (node != null)
            {
                var next = node.Next; // 预先获取下一个节点
                try
                {
                    // 安全调用处理器
                    node.Value?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    // 捕获并记录异常，不影响其他处理器
                    LogError($"带参数事件处理失败: {ex}");
                }
                node = next;
            }
        }

        #endregion

        #region 日志工具

        /// <summary>
        /// 记录普通信息（调试用）
        /// </summary>
        private void LogMessage(string message) =>
            Debug.Log($"[事件系统] {message}");

        /// <summary>
        /// 记录警告信息
        /// </summary>
        private void LogWarning(string message) =>
            Debug.LogWarning($"[事件系统] {message}");

        /// <summary>
        /// 记录错误信息
        /// </summary>
        private void LogError(string message) =>
            Debug.LogError($"[事件系统] {message}");

        #endregion
    }
}