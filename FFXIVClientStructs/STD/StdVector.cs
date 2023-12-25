using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.STD.StdHelpers;
using JetBrains.Annotations;
using static FFXIVClientStructs.STD.StdHelpers.StdImplHelpers;

namespace FFXIVClientStructs.STD;

/// <summary>
/// A <see cref="List{T}"/>-like view for <see href="https://en.cppreference.com/w/cpp/container/vector">std::vector</see>.
/// </summary>
/// <typeparam name="T">The type of element.</typeparam>
/// <typeparam name="TMemorySpace">The specifier for <see cref="IMemorySpace"/>, for vectors with different preferred memory space.</typeparam>
/// <typeparam name="TDisposable">The specifier for <see cref="IDisposable"/>, for element types that has its own destructor.</typeparam>
[StructLayout(LayoutKind.Sequential)]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public unsafe struct StdVector<T, TMemorySpace, TDisposable> : IStdVector<T>
    where T : unmanaged
    where TMemorySpace : IMemorySpaceStatic
    where TDisposable : IDisposableStatic<T> {

    /// <inheritdoc cref="IStdVector{T}.First"/>
    public T* First;

    /// <inheritdoc cref="IStdVector{T}.Last"/>
    public T* Last;

    /// <inheritdoc cref="IStdVector{T}.End"/>
    public T* End;

    /// <inheritdoc cref="LongCount"/>
    public int Count {
        readonly get => checked((int)LongCount);
        set => Resize(value);
    }

    /// <inheritdoc/>
    public long LongCount {
        readonly get => Last - First;
        set => Resize(value);
    }

    /// <inheritdoc cref="LongCapacity"/>
    public int Capacity {
        readonly get => checked((int)LongCapacity);
        set => SetCapacity(value);
    }

    /// <inheritdoc/>
    public long LongCapacity {
        readonly get => End - First;
        set => SetCapacity(value);
    }

    /// <inheritdoc/>
    readonly T* IStdVector<T>.First => First;

    /// <inheritdoc/>
    readonly T* IStdVector<T>.Last => Last;

    /// <inheritdoc/>
    readonly T* IStdVector<T>.End => End;

    /// <inheritdoc/>
    public readonly ref T this[long index] => ref First[CheckedIndex(index, false)];

    public static implicit operator Span<T>(in StdVector<T, TMemorySpace, TDisposable> value)
        => value.AsSpan();

    public static implicit operator ReadOnlySpan<T>(in StdVector<T, TMemorySpace, TDisposable> value)
        => value.AsSpan();
    
    /// <inheritdoc/>
    public void Dispose() {
        Resize(0);
        TrimExcess();
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((nint)First, (nint)Last, (nint)End);

    /// <inheritdoc/>
    public override string ToString() => $"{nameof(StdVector<T>)}<{typeof(T)}, {typeof(TMemorySpace)}, {typeof(TDisposable)}>({LongCount}/{LongCapacity})";

    /// <inheritdoc/>
    public readonly Span<T> AsSpan() => new(First, Count);

    /// <inheritdoc/>
    public readonly Span<T> AsSpan(long index) => new(
        First + CheckedIndex(index, true),
        checked((int)CheckedCount(index, LongCount - index)));

    /// <inheritdoc/>
    public readonly Span<T> AsSpan(long index, int count) => new(
        First + CheckedIndex(index, true),
        checked((int)CheckedCount(index, count)));

    /// <inheritdoc/>
    public void Add(in T item) {
        EnsureCapacity(LongCount + 1);
        *Last++ = item;
    }

    /// <inheritdoc/>
    public void AddRange(IEnumerable<T> collection) => InsertRange(LongCount, collection);

    /// <inheritdoc/>
    public void AddSpan(ReadOnlySpan<T> span) => InsertSpan(LongCount, span);

    /// <inheritdoc/>
    public readonly long BinarySearch(in T item) => BinarySearch(0, LongCount, item, null);

    /// <inheritdoc/>
    public readonly long BinarySearch(in T item, IComparer<T>? comparer) => BinarySearch(0, LongCount, item, comparer);

    /// <inheritdoc/>
    public readonly long BinarySearch(long index, long count, in T item, IComparer<T>? comparer) =>
        LongPointerSortHelper<T>.BinarySearch(
            First,
            CheckedIndex(index, true),
            CheckedCount(index, count),
            item,
            comparer);

    /// <inheritdoc cref="IStdVector{T}.Clear"/>
    public void Clear() {
        if (!TDisposable.IsDisposable) {
            Last = First;
            return;
        }

        while (Last != First)
            TDisposable.Dispose(ref *--Last);
    }

    /// <inheritdoc/>
    public readonly bool Contains(in T item) => LongIndexOf(item) != -1;

    /// <inheritdoc/>
    public readonly bool Exists(Predicate<T> match) => LongFindIndex(match) != -1;

    /// <inheritdoc/>
    public readonly T? Find(Predicate<T> match) {
        var index = LongFindIndex(match);
        return index == -1 ? default(T?) : First[index];
    }

    /// <inheritdoc/>
    public readonly int FindIndex(Predicate<T> match) => checked((int)LongFindIndex(match));

    /// <inheritdoc/>
    public readonly int FindIndex(int startIndex, Predicate<T> match) => checked((int)LongFindIndex(startIndex, match));

    /// <inheritdoc/>
    public readonly int FindIndex(int startIndex, int count, Predicate<T> match) => checked((int)LongFindIndex(startIndex, count, match));

    /// <inheritdoc/>
    public readonly int FindLastIndex(Predicate<T> match) => checked((int)LongFindLastIndex(match));

    /// <inheritdoc/>
    public readonly int FindLastIndex(int startIndex, Predicate<T> match) => checked((int)LongFindLastIndex(startIndex, match));

    /// <inheritdoc/>
    public readonly int FindLastIndex(int startIndex, int count, Predicate<T> match) => checked((int)LongFindLastIndex(startIndex, count, match));

    /// <inheritdoc/>
    public readonly void ForEach(Action<T> action) {
        for (var p = First; p < Last; p++)
            action(*p);
    }

    /// <inheritdoc/>
    public readonly int IndexOf(in T item) => checked((int)LongIndexOf(item));

    /// <inheritdoc/>
    public readonly int IndexOf(in T item, int index) => checked((int)LongIndexOf(item, index));

    /// <inheritdoc/>
    public readonly int IndexOf(in T item, int index, int count) => checked((int)LongIndexOf(item, index, count));

    /// <inheritdoc/>
    public void Insert(long index, in T item) {
        if (index < 0 || index > LongCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, null);

        EnsureCapacity(LongCount + 1);
        SpliceHole(index, 1);
        Last++;
        First[index] = item;
    }

    /// <inheritdoc/>
    public void InsertRange(long index, IEnumerable<T> collection) {
        var prevCount = LongCount;
        if (index < 0 || index > prevCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, null);

        switch (collection) {
            case IStdVector<T> isv when isv.PointerEquals(this):
                // We're inserting this vector into itself.
                EnsureCapacity(checked(prevCount * 2));
                CopyInside(index, index + prevCount, prevCount - index);
                CopyInside(0, index, prevCount);
                Last += prevCount;
                break;
            case List<T> list:
                InsertSpan(index, CollectionsMarshal.AsSpan(list));
                break;
            case T[] array:
                InsertSpan(index, array.AsSpan());
                break;
            case var _ when TryGetCountFromEnumerable(collection, out var count):
                EnsureCapacity(prevCount + count);
                SpliceHole(index, count);
                using (var enu = collection.GetEnumerator()) {
                    var p = First + index;
                    while (count-- > 0 && enu.MoveNext()) {
                        *p++ = enu.Current;
                        Last++;
                    }
                }
                break;
            default:
                foreach (var item in collection)
                    Insert(index++, item);
                break;
        }
    }

    /// <inheritdoc/>
    public void InsertSpan(long index, ReadOnlySpan<T> span) {
        var prevCount = LongCount;
        if (index < 0 || index > prevCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, null);

        EnsureCapacity(prevCount + span.Length);
        SpliceHole(index, span.Length);
        Last += span.Length;
        span.CopyTo(new Span<T>(First + index, span.Length));
    }

    /// <inheritdoc/>
    public readonly int LastIndexOf(in T item) => checked((int)LongLastIndexOf(item));

    /// <inheritdoc/>
    public readonly int LastIndexOf(in T item, int index) => checked((int)LongLastIndexOf(item, index));

    /// <inheritdoc/>
    public readonly int LastIndexOf(in T item, int index, int count) => checked((int)LongLastIndexOf(item, index, count));

    /// <inheritdoc/>
    public bool Remove(in T item) {
        var i = LongIndexOf(item);
        if (i == -1)
            return false;

        RemoveAt(i);
        return true;
    }

    /// <inheritdoc/>
    public long RemoveAll(Predicate<T> match) {
        long removed = 0;
        long index = -1;
        while (true) {
            index = LongFindIndex(index + 1, match);
            if (index == -1)
                break;
            removed++;
            RemoveAt(index);
            index--;
        }

        return removed;
    }

    /// <inheritdoc/>
    public void RemoveAt(long index) {
        _ = CheckedIndex(index, false);
        TDisposable.Dispose(ref First[index]);
        Last--;
        CopyInside(index + 1, index, LongCount - index);
    }

    /// <inheritdoc/>
    public void RemoveRange(long index, long count) {
        _ = CheckedIndex(index, true);
        _ = CheckedCount(index, count);
        if (TDisposable.IsDisposable) {
            var p = First + index;
            for (var remain = count; remain > 0; remain--)
                TDisposable.Dispose(ref *p);
        }

        Last -= count;
        CopyInside(index + count, index, LongCount - index);
    }

    /// <inheritdoc/>
    public void Reverse() => Reverse(0, LongCount);

    /// <inheritdoc/>
    public void Reverse(long index, long count) {
        _ = CheckedIndex(index, true);
        _ = CheckedCount(index, count);
        var l = First + index;
        var r = First + count - 1;
        for (; l < r; l++, r--) {
            var t = *l;
            *l = *r;
            *r = t;
        }
    }

    /// <inheritdoc/>
    public void Sort() => LongPointerSortHelper<T>.Sort(First, LongCount);

    /// <inheritdoc/>
    public void Sort(long index, long count) =>
        LongPointerSortHelper<T>.Sort(First + CheckedIndex(index, true), CheckedCount(index, count));

    /// <inheritdoc/>
    public void Sort(IComparer<T>? comparer) => LongPointerSortHelper<T>.Sort(First, LongCount, comparer);

    /// <inheritdoc/>
    public void Sort(long index, long count, IComparer<T>? comparer) =>
        LongPointerSortHelper<T>.Sort(First + CheckedIndex(index, true), CheckedCount(index, count), comparer);

    /// <inheritdoc/>
    public void Sort(Comparison<T> comparison) => Sort(0, LongCount, comparison);

    /// <inheritdoc/>
    public void Sort(long index, long count, Comparison<T> comparison) =>
        LongPointerSortHelper<T>.Sort(First + CheckedIndex(index, true), CheckedCount(index, count), comparison);

    /// <inheritdoc/>
    public readonly T[] ToArray() => ToArray(0, LongCount);

    /// <inheritdoc/>
    public readonly T[] ToArray(long index) => ToArray(index, LongCount - index);

    /// <inheritdoc/>
    public readonly T[] ToArray(long index, long count) {
        _ = CheckedIndex(index, true);
        _ = CheckedCount(index, count);
        if (count == 0)
            return Array.Empty<T>();

        var res = new T[count];
        fixed (T* p = res)
            Buffer.MemoryCopy(First + index, p, count * sizeof(T), count * sizeof(T));
        return res;
    }

    /// <inheritdoc/>
    public readonly long LongFindIndex(Predicate<T> match) => LongFindIndex(0, LongCount, match);

    /// <inheritdoc/>
    public readonly long LongFindIndex(long startIndex, Predicate<T> match) => LongFindIndex(startIndex, LongCount - startIndex, match);

    /// <inheritdoc/>
    public readonly long LongFindIndex(long startIndex, long count, Predicate<T> match) {
        if (startIndex < 0 || startIndex > LongCount)
            throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);

        if (count < 0 || startIndex > LongCount - count)
            throw new ArgumentOutOfRangeException(nameof(count), count, null);

        var end = First + startIndex + count;
        for (var p = First + startIndex; p < end; p++, startIndex++) {
            if (match(*p))
                return startIndex;
        }

        return -1;
    }

    /// <inheritdoc/>
    public readonly long LongFindLastIndex(Predicate<T> match) => LongFindLastIndex(LongCount - 1, LongCount, match);

    /// <inheritdoc/>
    public readonly long LongFindLastIndex(long startIndex, Predicate<T> match) => LongFindLastIndex(startIndex, startIndex + 1, match);

    /// <inheritdoc/>
    public readonly long LongFindLastIndex(long startIndex, long count, Predicate<T> match) {
        if (LongCount == 0) {
            if (startIndex != -1)
                throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);
        } else {
            if (startIndex < -1 || startIndex >= LongCount)
                throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);
        }

        if (count < 0 || startIndex > LongCount - count)
            throw new ArgumentOutOfRangeException(nameof(count), count, null);

        var end = First + startIndex - count;
        for (var p = First + startIndex; p >= end; p--, startIndex--) {
            if (match(*p))
                return startIndex;
        }

        return -1;
    }

    /// <inheritdoc/>
    public readonly long LongIndexOf(in T item) => LongIndexOf(item, 0, LongCount);

    /// <inheritdoc/>
    public readonly long LongIndexOf(in T item, long index) => LongIndexOf(item, index, LongCount - index);

    /// <inheritdoc/>
    public readonly long LongIndexOf(in T item, long index, long count) {
        if (index < 0 || index > LongCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, null);

        if (count < 0 || index > LongCount - count)
            throw new ArgumentOutOfRangeException(nameof(count), count, null);

        var end = First + index + count;
        for (var p = First + index; p < end; p++, index++) {
            if (DefaultEquals(*p, item))
                return index;
        }

        return -1;
    }

    /// <inheritdoc/>
    public readonly long LongLastIndexOf(in T item) => LongLastIndexOf(item, 0, LongCount);

    /// <inheritdoc/>
    public readonly long LongLastIndexOf(in T item, long index) => LongLastIndexOf(item, index, index + 1);

    /// <inheritdoc/>
    public readonly long LongLastIndexOf(in T item, long index, long count) {
        if (LongCount == 0) {
            if (index != -1)
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
        } else {
            if (index < -1 || index >= LongCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }

        if (count < 0 || index > LongCount - count)
            throw new ArgumentOutOfRangeException(nameof(count), count, null);

        var end = First + index - count;
        for (var p = First + index; p >= end; p--, index--) {
            if (DefaultEquals(item, *p))
                return index;
        }

        return -1;
    }

    /// <inheritdoc/>
    public long EnsureCapacity(long capacity) {
        if (capacity <= LongCapacity)
            return capacity;

        capacity = Math.Max(capacity, LongCount * 2);
        SetCapacity(capacity);
        return capacity;
    }

    /// <inheritdoc/>
    public long TrimExcess() => SetCapacity(LongCount);

    /// <inheritdoc/>
    public void Resize(long newSize) {
        var prevCount = LongCount;
        ResizeUndefined(newSize);

        if (newSize <= prevCount)
            return;

        ZeroMemory(First + prevCount, (nuint)(newSize - prevCount));
    }

    /// <inheritdoc/>
    public void Resize(long newSize, in T defaultValue) {
        var prevCount = LongCount;
        ResizeUndefined(newSize);

        if (newSize <= prevCount)
            return;

        FillMemory(First + prevCount, (nuint)(newSize - prevCount), defaultValue);
    }

    /// <inheritdoc/>
    public void ResizeUndefined(long newSize) {
        var prevCount = LongCount;
        EnsureCapacity(newSize);
        Last = First + newSize;

        if (newSize >= prevCount || !TDisposable.IsDisposable)
            return;
        for (var i = newSize; i < prevCount; i++)
            TDisposable.Dispose(ref First[i]);
    }

    /// <inheritdoc/>
    public long SetCapacity(long newCapacity) {
        var count = LongCount;
        var prevCapacity = LongCapacity;
        if (newCapacity < count || count < 0)
            throw new ArgumentOutOfRangeException(nameof(newCapacity), newCapacity, null);
        if (newCapacity == prevCapacity)
            return prevCapacity;

        var newAlloc =
            newCapacity == 0
                ? null
                : TMemorySpace.Allocate(checked((nuint)(newCapacity * sizeof(T))), 16);
        if (newAlloc == null && newCapacity > 0)
            throw new OutOfMemoryException();

        if (count != 0)
            Unsafe.CopyBlock(newAlloc, First, (uint)(count * sizeof(T)));

        if (First != null)
            IMemorySpace.Free(First, (nuint)(prevCapacity * sizeof(T)));

        First = (T*)newAlloc;
        End = First + newCapacity;
        Last = First + count;
        return newCapacity;
    }

    /// <inheritdoc/>
    public readonly IStdVector<T>.Enumerator GetEnumerator() => new(First, Last);

    [AssertionMethod]
    private readonly long CheckedIndex(long index, bool allowEnd) {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), index, null);
        if (index > LongCount || (index == LongCount && !allowEnd))
            throw new ArgumentOutOfRangeException(nameof(index), index, null);
        return index;
    }

    [AssertionMethod]
    private readonly long CheckedCount(long index, long count) {
        if (count < 0 || count > LongCount - index)
            throw new ArgumentOutOfRangeException(nameof(count), count, null);
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void CopyInside(long fromIndex, long toIndex, long count) =>
        Buffer.MemoryCopy(First + fromIndex, First + toIndex, count * sizeof(T), count * sizeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void SpliceHole(long index, long count) =>
        CopyInside(index, index + count, LongCount - index);
}
