using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace McpIt.Generator.Internal;

public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(IEnumerable<T> items) => _array = items.ToImmutableArray();
    public EquatableArray(ImmutableArray<T> items) => _array = items;

    public int Count => _array.IsDefault ? 0 : _array.Length;
    public T this[int index] => _array[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.IsDefault && other._array.IsDefault) return true;
        if (_array.IsDefault || other._array.IsDefault) return false;
        return _array.SequenceEqual(other._array);
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array.IsDefault) return 0;
        var hash = 17;
        foreach (var item in _array)
            hash = hash * 31 + (item?.GetHashCode() ?? 0);
        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        (_array.IsDefault ? Enumerable.Empty<T>() : _array).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
