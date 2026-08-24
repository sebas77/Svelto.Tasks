using System;

public ref struct SpanList<T>
{
    Span<T> _buffer;
    int _count;

    public SpanList(Span<T> buffer)
    {
        _buffer = buffer;
        _count = 0;
    }

    // Gets the current number of elements
    public int count => _count;

    // Gets the fixed capacity
    public int Capacity => _buffer.Length;

    // Exposes the span for the currently used elements
    public Span<T> AsSpan() => _buffer.Slice(0, _count);
    
    // Adds an element if there is room
    public void Add(T item)
    {
        if (_count == _buffer.Length)
            throw new InvalidOperationException("Span is full; cannot add more elements.");
        _buffer[_count++] = item;
    }

    // Indexer to access elements
    public ref T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref _buffer[index];
        }
    }

    public void AddRange(Span<T> span)
    {
        if (_count + span.Length > _buffer.Length)
            throw new InvalidOperationException("Span is full; cannot add more elements.");
        
        span.CopyTo(_buffer.Slice(_count));
        _count += span.Length;
    }
}
