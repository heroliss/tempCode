using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

public class ObservableList<T> : IList<T>, IReadOnlyList<T>, ICollection<T>, IEnumerable<T>
{
    public enum ChangeType
    {
        ItemAdded,
        ItemRemoved,
        ItemChanged,
        ListCleared,
        ListSorted
    }

    public class ListChangedEventArgs : EventArgs
    {
        public ChangeType ChangeType { get; }
        public int Index { get; }
        public T NewItem { get; }
        public T OldItem { get; }

        public ListChangedEventArgs(ChangeType changeType, int index = -1, T newItem = default, T oldItem = default)
        {
            ChangeType = changeType;
            Index = index;
            NewItem = newItem;
            OldItem = oldItem;
        }
    }

    private readonly List<T> _items = new List<T>();

    public readonly UnityEvent<ListChangedEventArgs> OnListChanged = new UnityEvent<ListChangedEventArgs>();

    public T this[int index]
    {
        get => _items[index];
        set
        {
            T oldItem = _items[index];
            _items[index] = value;
            OnListChanged.Invoke(new ListChangedEventArgs(
                ChangeType.ItemChanged,
                index,
                value,
                oldItem
            ));
        }
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    // 添加单个项
    public void Add(T item)
    {
        _items.Add(item);
        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ItemAdded,
            _items.Count - 1,
            item
        ));
    }

    // 插入单个项
    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ItemAdded,
            index,
            item
        ));
    }

    // 移除单个项
    public bool Remove(T item)
    {
        int index = _items.IndexOf(item);
        if (index >= 0)
        {
            T oldItem = _items[index];
            _items.RemoveAt(index);
            OnListChanged.Invoke(new ListChangedEventArgs(
                ChangeType.ItemRemoved,
                index,
                default,
                oldItem
            ));
            return true;
        }
        return false;
    }

    // 按索引移除单个项
    public void RemoveAt(int index)
    {
        T oldItem = _items[index];
        _items.RemoveAt(index);
        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ItemRemoved,
            index,
            default,
            oldItem
        ));
    }

    // 清空列表
    public void Clear()
    {
        _items.Clear();
        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ListCleared
        ));
    }

    // 排序列表
    public void Sort()
    {
        // 保存旧列表状态以便比较
        //List<T> oldList = new List<T>(_items);

        _items.Sort();

        // 检查哪些项的位置发生了变化
        //for (int i = 0; i < _items.Count; i++)
        //{
        //    if (!EqualityComparer<T>.Default.Equals(_items[i], oldList[i]))
        //    {
        //        OnListChanged.Invoke(new ListChangedEventArgs(
        //            ListChangeType.ItemChanged,
        //            i,
        //            _items[i],
        //            oldList[i]
        //        ));
        //    }
        //}

        // 触发排序完成事件
        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ListSorted
        ));
    }

    // 使用比较器排序
    public void Sort(IComparer<T> comparer)
    {
        //List<T> oldList = new List<T>(_items);

        _items.Sort(comparer);

        //for (int i = 0; i < _items.Count; i++)
        //{
        //    if (!EqualityComparer<T>.Default.Equals(_items[i], oldList[i]))
        //    {
        //        OnListChanged.Invoke(new ListChangedEventArgs(
        //            ListChangeType.ItemChanged,
        //            i,
        //            _items[i],
        //            oldList[i]
        //        ));
        //    }
        //}

        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ListSorted
        ));
    }

    // 使用比较委托排序
    public void Sort(Comparison<T> comparison)
    {
        //List<T> oldList = new List<T>(_items);

        _items.Sort(comparison);

        //for (int i = 0; i < _items.Count; i++)
        //{
        //    if (!EqualityComparer<T>.Default.Equals(_items[i], oldList[i]))
        //    {
        //        OnListChanged.Invoke(new ListChangedEventArgs(
        //            ListChangeType.ItemChanged,
        //            i,
        //            _items[i],
        //            oldList[i]
        //        ));
        //    }
        //}

        OnListChanged.Invoke(new ListChangedEventArgs(
            ChangeType.ListSorted
        ));
    }

    // 以下是所有查找方法，它们不会触发事件

    public bool Contains(T item) => _items.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public int IndexOf(T item) => _items.IndexOf(item);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    // 提供LINQ支持
    public IEnumerable<T> Where(Func<T, bool> predicate) => _items.Where(predicate);

    public T Find(Predicate<T> match) => _items.Find(match);

    public List<T> FindAll(Predicate<T> match) => _items.FindAll(match);

    public int FindIndex(Predicate<T> match) => _items.FindIndex(match);

    public int FindIndex(int startIndex, Predicate<T> match) => _items.FindIndex(startIndex, match);

    public int FindIndex(int startIndex, int count, Predicate<T> match) => _items.FindIndex(startIndex, count, match);

    public T FindLast(Predicate<T> match) => _items.FindLast(match);

    public int FindLastIndex(Predicate<T> match) => _items.FindLastIndex(match);

    public int FindLastIndex(int startIndex, Predicate<T> match) => _items.FindLastIndex(startIndex, match);

    public int FindLastIndex(int startIndex, int count, Predicate<T> match) => _items.FindLastIndex(startIndex, count, match);

    public bool Exists(Predicate<T> match) => _items.Exists(match);

    public T[] ToArray() => _items.ToArray();

    public List<T> ToList() => new List<T>(_items);

    // 批量操作方法（不触发事件）
    public void AddRangeSilent(IEnumerable<T> collection) => _items.AddRange(collection);

    public void InsertRangeSilent(int index, IEnumerable<T> collection) => _items.InsertRange(index, collection);
}