namespace Tela.Library;

/// <summary>A lista do que se está a ver (normalmente, os vídeos da pasta) e onde se vai.</summary>
public sealed class PlayQueue
{
    private readonly List<string> _items = [];

    public IReadOnlyList<string> Items => _items;
    public int Index { get; private set; } = -1;
    public int Count => _items.Count;
    public string? Current => Index >= 0 && Index < _items.Count ? _items[Index] : null;
    public bool HasNext => Index >= 0 && Index + 1 < _items.Count;
    public bool HasPrevious => Index > 0;
    public string? NextItem => HasNext ? _items[Index + 1] : null;

    public void Set(IEnumerable<string> items, int index)
    {
        _items.Clear();
        _items.AddRange(items);
        Index = _items.Count == 0 ? -1 : Math.Clamp(index, 0, _items.Count - 1);
    }

    public void Clear() => Set([], -1);

    public bool MoveTo(int index)
    {
        if (index < 0 || index >= _items.Count)
            return false;
        Index = index;
        return true;
    }

    public bool MoveNext() => MoveTo(Index + 1);

    public bool MovePrevious() => MoveTo(Index - 1);

    /// <summary>Junta ao fim sem mudar o que está a tocar (ficheiros largados com Shift, por exemplo).</summary>
    public void Append(IEnumerable<string> items)
    {
        _items.AddRange(items);
        if (Index < 0 && _items.Count > 0)
            Index = 0;
    }
}
