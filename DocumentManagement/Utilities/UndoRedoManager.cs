using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace DocumentManagement.Utilities;

public interface IUndoableAction
{
    void Undo();
    void Redo();
}

// Keeps a multi-cell paste or delete as one Excel-like undo step.
public sealed class CompositeUndoAction(IEnumerable<IUndoableAction> actions) : IUndoableAction
{
    private readonly List<IUndoableAction> _actions = actions.ToList();

    public bool HasActions => _actions.Count > 0;

    public void Undo()
    {
        for (var index = _actions.Count - 1; index >= 0; index--)
        {
            _actions[index].Undo();
        }
    }

    public void Redo()
    {
        foreach (var action in _actions)
        {
            action.Redo();
        }
    }
}

public class PropertyChangeAction(object target, string propertyName, object? oldValue, object? newValue, Action<string>? onExecuted = null) : IUndoableAction
{
    private readonly object _target = target;
    private readonly string _propertyName = propertyName;
    private readonly object? _oldValue = oldValue;
    private readonly object? _newValue = newValue;
    private readonly Action<string>? _onExecuted = onExecuted;

    public void Undo()
    {
        SetPropertyValue(_oldValue);
        _onExecuted?.Invoke(_propertyName);
    }

    public void Redo()
    {
        SetPropertyValue(_newValue);
        _onExecuted?.Invoke(_propertyName);
    }

    private void SetPropertyValue(object? value)
    {
        var prop = _target.GetType().GetProperty(_propertyName);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(_target, value);
        }
    }
}
public class CollectionChangeAction<T>(ObservableCollection<T> collection, T item, int index, bool isAddition) : IUndoableAction
{
    private readonly ObservableCollection<T> _collection = collection;
    private readonly T _item = item;
    private readonly int _index = index;
    private readonly bool _isAddition = isAddition;

    public void Undo()
    {
        if (_isAddition)
        {
            _collection.RemoveAt(_index);
        }
        else
        {
            if (_index >= 0 && _index <= _collection.Count)
                _collection.Insert(_index, _item);
            else
                _collection.Add(_item);
        }
    }

    public void Redo()
    {
        if (_isAddition)
        {
            if (_index >= 0 && _index <= _collection.Count)
                _collection.Insert(_index, _item);
            else
                _collection.Add(_item);
        }
        else
        {
            _collection.RemoveAt(_index);
        }
    }
}

public class ReorderCollectionAction<T>(ObservableCollection<T> collection, IReadOnlyList<T> oldOrder, IReadOnlyList<T> newOrder) : IUndoableAction
{
    private readonly ObservableCollection<T> _collection = collection;
    private readonly List<T> _oldOrder = oldOrder.ToList();
    private readonly List<T> _newOrder = newOrder.ToList();

    public void Undo()
    {
        ApplyOrder(_oldOrder);
    }

    public void Redo()
    {
        ApplyOrder(_newOrder);
    }

    private void ApplyOrder(IReadOnlyList<T> order)
    {
        for (var targetIndex = 0; targetIndex < order.Count; targetIndex++)
        {
            var item = order[targetIndex];
            var currentIndex = _collection.IndexOf(item);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                _collection.Move(currentIndex, targetIndex);
            }
        }
    }
}
