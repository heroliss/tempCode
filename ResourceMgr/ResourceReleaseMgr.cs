using Ddz.BoloExtensions.EventMgr;
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityGameFramework.Runtime;

namespace Ddz.ResourceMgr
{
    /// <summary>
    /// 资源统一释放管理器
    /// </summary>
    public class ResourceReleaseMgr : IDisposable
    {
        /// <summary>
        /// 管理的可释放对象集合
        /// </summary>
        private HashSet<IDisposable> _disposableSet = new();


        #region 基础方法
        /// <summary>
        /// 释放所有被管理的资源
        /// </summary>
        public void Dispose()
        {
            foreach (var item in _disposableSet)
            {
                try
                {
                    item?.Dispose();
                }
                catch (Exception e)
                {
                    BoloLog.LogError("释放资源时发生异常:" + e.Message);
                }
            }
            _disposableSet.Clear();
        }

        /// <summary>
        /// 将可释放对象添加到集合中统一管理
        /// </summary>
        /// <param name="disposable"></param>
        public void Add(IDisposable disposable)
        {
            if (disposable == null)
            {
                BoloLog.LogWarning("添加到DisposableSet中的值为null");
                return;
            }
            _disposableSet.Add(disposable);
        }

        /// <summary>
        /// 从统一释放集合中移除可释放对象（不再管理）
        /// </summary>
        /// <param name="disposable"></param>
        public void Remove(IDisposable disposable)
        {
            _disposableSet.Remove(disposable);
        }
        #endregion


        #region 注册类型事件
        /// <summary>
        /// 注册类型事件，并添加到统一释放集合
        /// </summary>
        public void RegisterEvent<T>(Action handler) where T : IEvent
        {
            var unsubscriber = GameGlobal.EventMgr.On<T>(handler);
            Add(unsubscriber);
        }

        /// <summary>
        /// 注册类型事件，并添加到统一释放集合
        /// </summary>
        public void RegisterEvent<T>(Action<T> handler) where T : IEvent
        {
            var unsubscriber = GameGlobal.EventMgr.On<T>(handler);
            Add(unsubscriber);
        }
        #endregion


        #region 注册枚举事件
        /// <summary>
        /// 注册枚举事件，并添加到统一释放集合
        /// </summary>
        public void RegisterEvent(Enum eventType, Action handle) => RegisterEvent(eventType, (e, obj) => { handle(); });

        /// <summary>
        /// 注册枚举事件，并添加到统一释放集合
        /// </summary>
        public void RegisterEvent(Enum eventType, EventTargetEnum handle)
        {
            GameGlobal.EventMgr.On(eventType, handle);
            Add(new EnumEventUnregister(eventType, handle));
        }

        class EnumEventUnregister : IDisposable
        {
            public readonly Enum EventType;
            public readonly EventTargetEnum Handler;
            public EnumEventUnregister(Enum eventType, EventTargetEnum handler)
            {
                EventType = eventType;
                Handler = handler;
            }
            public void Dispose() => GameGlobal.EventMgr.Off(EventType, Handler);
        }
        #endregion


        #region 注册Unity事件
        /// <summary>
        /// 注册Unity事件，并添加到统一释放集合
        /// </summary>
        /// <param name="unityEvent"></param>
        /// <param name="handler"></param>
        public void RegisterEvent(UnityEvent unityEvent, UnityAction handler)
        {
            unityEvent.AddListener(handler);
            Add(new UnityEventUnregister(unityEvent, handler));
        }

        class UnityEventUnregister : IDisposable
        {
            public readonly UnityEvent UnityEvent;
            public readonly UnityAction Handler;
            public UnityEventUnregister(UnityEvent unityEvent, UnityAction handler)
            {
                UnityEvent = unityEvent;
                Handler = handler;
            }
            public void Dispose() => UnityEvent.RemoveListener(Handler);
        }
        #endregion

        #region 注册Unity事件（一个参数）
        /// <summary>
        /// 注册Unity事件，并添加到统一释放集合
        /// </summary>
        /// <param name="unityEvent"></param>
        /// <param name="handler"></param>
        public void RegisterEvent<T>(UnityEvent<T> unityEvent, UnityAction<T> handler)
        {
            unityEvent.AddListener(handler);
            Add(new UnityEventUnregister1<T>(unityEvent, handler));
        }

        class UnityEventUnregister1<T> : IDisposable
        {
            public readonly UnityEvent<T> UnityEvent;
            public readonly UnityAction<T> Handler;
            public UnityEventUnregister1(UnityEvent<T> unityEvent, UnityAction<T> handler)
            {
                UnityEvent = unityEvent;
                Handler = handler;
            }
            public void Dispose() => UnityEvent.RemoveListener(Handler);
        }
        #endregion


        #region 注册Unity事件（两个参数）
        /// <summary>
        /// 注册Unity事件，并添加到统一释放集合
        /// </summary>
        /// <param name="unityEvent"></param>
        /// <param name="handler"></param>
        public void RegisterEvent<T1, T2>(UnityEvent<T1, T2> unityEvent, UnityAction<T1, T2> handler)
        {
            unityEvent.AddListener(handler);
            Add(new UnityEventUnregister2<T1, T2>(unityEvent, handler));
        }

        class UnityEventUnregister2<T1, T2> : IDisposable
        {
            public readonly UnityEvent<T1, T2> UnityEvent;
            public readonly UnityAction<T1, T2> Handler;
            public UnityEventUnregister2(UnityEvent<T1, T2> unityEvent, UnityAction<T1, T2> handler)
            {
                UnityEvent = unityEvent;
                Handler = handler;
            }
            public void Dispose() => UnityEvent.RemoveListener(Handler);
        }
        #endregion


        #region 注册多播委托事件
        /// <summary>
        /// 注册多播委托事件，并添加到统一释放集合
        /// </summary>
        /// <param name="registerAction">注册事件的方法</param>
        /// <param name="unregisterAction">反注册事件的方法</param>
        /// <param name="handler"></param>
        public void RegisterEvent<T>(Action<T> registerAction, Action<T> unregisterAction, T handler) where T : Delegate
        {
            registerAction(handler);
            Add(new DelegateEventUnregister(() => unregisterAction(handler)));
        }

        class DelegateEventUnregister : IDisposable
        {
            private readonly Action _unregisterAction;
            public DelegateEventUnregister(Action unregisterAction)
            {
                _unregisterAction = unregisterAction;
            }
            public void Dispose() => _unregisterAction();
        }
        #endregion


        #region 打开UI子视图
        /// <summary>
        /// 打开子视图，并添加到统一释放集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public void OpenSubView(eUIViewID id, object userData = null)
        {
            int serialId = GameGlobal.UIMgr.OpenView(id, userData);
            if (serialId > 0)//若有效打开了UI
            {
                Add(new UIViewCloser(serialId));
            }
        }
        class UIViewCloser : IDisposable
        {
            public readonly int SerialId;
            public UIViewCloser(int serialId) => SerialId = serialId;
            public void Dispose()
            {
                //try
                {
                    GameGlobal.UIMgr.CloseView(SerialId);
                }
                //catch
                {
                    //TODO：这里暂时吞掉异常，因为停止运行游戏时会找不到窗口
                }
            }
        }
        #endregion
    }
}