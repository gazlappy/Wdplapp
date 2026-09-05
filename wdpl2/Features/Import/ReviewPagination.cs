namespace Wdpl2.Services.Import;

public sealed class ReviewPagination<T>
{
    public const int SeasonPageSize = 5;
    public const int FilePageSize = 20;

    public ReviewPagination(IReadOnlyList<T> source, int pageIndex, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        PageCount = Math.Max(1, (int)Math.Ceiling(source.Count / (double)pageSize));
        PageIndex = Math.Clamp(pageIndex, 0, PageCount - 1);
        Items = source.Skip(PageIndex * pageSize).Take(pageSize).ToList();
    }

    public int PageIndex { get; }
    public int PageCount { get; }
    public IReadOnlyList<T> Items { get; }
    public bool HasPrevious => PageIndex > 0;
    public bool HasNext => PageIndex < PageCount - 1;
}
