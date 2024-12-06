namespace HAC.EFTree;

/// <summary>
/// The inteface each entity should implements in order to add to a tree.
/// </summary>
public interface ITreeEntity
{
    /// <summary>
    /// Left value that use to manage the position of the entity in the tree.
    /// </summary>
    public long Left { get; set; }

    /// <summary>
    /// Right value that use to manage the position of the entity in the tree.
    /// </summary>
    public long Right { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool HasChild() => Right - Left > 2;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parent"></param>
    /// <returns></returns>
    public bool IsChildOf(ITreeEntity parent) => parent.Left < Left && Right < parent.Right;
}
