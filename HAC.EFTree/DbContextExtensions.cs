using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace HAC.EFTree;

/// <summary>
/// DbSet extensions.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Adds an entity to tree beneath another one.
    /// </summary>
    /// <param name="source">DbSet instance.</param>
    /// <param name="entity">The entity to add.</param>
    /// <param name="parent">The entity entity which <paramref name="entity"/> will be placed beneath it as child subnode. 
    /// if set to <see langword="null"/> the <paramref name="entity"/> will be added to root of the tree.</param>
    public static void Add<TEntity>(this DbSet<TEntity> source, TEntity entity, TEntity? parent) where TEntity : class, ITreeEntity
    {
        long position;
        if (parent is not null)
        {
            source.CheckedDetached(parent, nameof(parent));
            position = source.Shift(parent.Right, 2);
        }
        else
            position = source.MaxRight() + 1;
        entity.Right = (entity.Left = position) + 1;
        source.Add(entity);
    }

    /// <summary>
    /// Inserts an entity in the position of the <paramref name="sibling"/> node, shifting the next elements forward.
    /// </summary>
    /// <param name="source">DbSet instance.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="sibling">An adjacent node that the entity would be inserted on the preceeding position.</param>
    public static void Insert<TEntity>(this DbSet<TEntity> source, TEntity entity, TEntity sibling) where TEntity : class, ITreeEntity
    {
        source.CheckedDetached(sibling, nameof(sibling));
        long position = source.Shift(sibling.Left, 2);
        entity.Right = (entity.Left = position) + 1;
        source.Add(entity);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="collection"></param>
    /// <param name="s"></param>
    /// <param name="t"></param>
    public static void Move<TEntity>(this DbSet<TEntity> collection, TEntity s, TEntity t) where TEntity : class, ITreeEntity
    {
        collection.CheckedDetached(s, nameof(s));
        collection.CheckedDetached(t, nameof(t));
        if (t.IsChildOf(s))
            throw new InvalidOperationException("Can not move a parent not under its child node");
        /*
         *  Case 1: Illegal
         *              S.L━━━━━━━━━━━━━S.R                    
         *                  T.L━━━━━T.R
         */

        /*
         *
         *  Case 2:
         *                     hole     gap
         *   ╋━━━━━━━╋━━━━━━━╋━━━━━━━╋━━━━━━━╋━━━━━━━╋
         *   ┃       ┃       ┃       ┃       ┃       ┃
         *   ╋       ╋       ╋   >   ╋   <   ╋       ╋
         *   ╋   >   ╋   >   ╋   >   ╋       ╋   >   ╋
         *   ╋   <   ╋   <   ╋       ╋   <   ╋   <   ╋
         *  min                                     max
         *                  S.L━━━━━S.R                    
         *          T.L━━━━━━━━━━━━━━━━━━━━━T.R
         */

        /*
        *  3. 
        *                   p
        *   ┣━━━━━━━╋━━━━━━━╋━━━━━━━╋━━━━━━━╋━━━━━━━┫
        *   ┃       ┃ hole  ┃  gap  ┃       ┃       ┃
        *   ┃     start     ┃     offset    ┃       ┃
        *   ┃       ┃   >   ┃   <   ┃   <   ┃       ┃
        *   ┃   <   ┃       ┃   <   ┃   <   ┃   <   ┃
        *  min                                     max
        *          S.L━━━━━S.R                    
        *                          T.L━━━━━T.R
        */
        var min = collection.MinLeft();
        var max = collection.MaxRight();
        var p = s.Right + 1;
        var start = p - min;
        var hole = p - s.Left;
        var offset = t.Right - p;
        //move hole out.
        collection.Shift(-start, s.Left, p);
        //move offset back.
        collection.Shift(-hole, p, t.Right);
        //move hole in.
        collection.Shift(offset + start, s.Left, s.Right + 1);
        /*
        *  Case 4:
        *                      gap     hole
        *   ╋━━━━━━━╋━━━━━━━╋━━━━━━━╋━━━━━━━╋━━━━━━━╋
        *   ╋       ╋       ╋   >   ╋   <   ╋       ╋
        *   ╋   >   ╋   >   ╋   >   ╋       ╋   >   ╋
        *  min                                     max
        *                          S.L━━━━━S.R                    
        *          T.L━━━━━T.R
        * **/
    }

    static void Shift<TEntity>(this DbSet<TEntity> source, long offset, long? from, long? to) where TEntity : class, ITreeEntity
    {
        Expression<Func<TEntity, bool>> left, right;
        if (to is not null)
        {
            if (from is null)
            {
                left = x => x.Left < to;
                right = x => x.Right < to;
            }
            else
            {
                left = x => from <= x.Left && x.Left < to;
                right = x => from <= x.Right && x.Right < to;
            }
        }
        else
        {
            if (from is not null)
            {
                left = x => from <= x.Left;
                right = x => from <= x.Right;
            }
            else
                throw new ArgumentException("Both from and to arguments could not be null.");
        }

        source.Where(left).ExecuteUpdate(setter => setter.SetProperty(x => x.Left, x => x.Left + offset));
        foreach (var entity in source.Local.Where(left.Compile())) entity.Left += offset;
        source.Where(right).ExecuteUpdate(setter => setter.SetProperty(x => x.Right, x => x.Right + offset));
        foreach (var entity in source.Local.Where(right.Compile())) entity.Right += offset;
    }

    /// <summary>
    /// Shift tree nodes, automatically decides which direction of shifting would be more effient.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source">DbSet instance.</param>
    /// <param name="position">The location in which shifting will starts from or ends to.</param>
    /// <param name="offset"></param>
    /// <returns>The actual position in which the starting starts.</returns>
    static long Shift<TEntity>(this DbSet<TEntity> source, long position, long offset) where TEntity : class, ITreeEntity
    {
        if (position - source.MinLeft() <= source.MaxRight() - position)
        {
            source.Shift(-offset, null, position);
            return position - offset;
        }
        else
        {
            source.Shift(offset, position, null);
            return position;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public static IEnumerable<TEntity> GetChildren<TEntity>(this DbSet<TEntity> source, TEntity entity) where TEntity : class, ITreeEntity
    {
        var table = source.EntityType.GetTableName();
        return source.FromSqlRaw($"""
        WITH GetSiblings AS 
        (
            SELECT *
            FROM {table}
            WHERE [Left] = {entity.Left + 1}

            UNION ALL

            SELECT t.*
            FROM {table} t
            INNER JOIN GetSiblings s 
        	ON t.[Left] = s.[Right] + 1
        )
        SELECT * FROM GetSiblings;
        """);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="source"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public static IEnumerable<TEntity> GetAllChildren<TEntity>(this DbSet<TEntity> source, TEntity entity) where TEntity : class, ITreeEntity
        => source.Where(x => entity.Left < x.Left && x.Right < entity.Right);

    /// <summary>
    /// Finds minimum left value of the tree.
    /// </summary>
    /// <param name="source">DbSet instance.</param>
    static long MinLeft<TEntity>(this DbSet<TEntity> source) where TEntity : class, ITreeEntity
        => source.Local.Select(x => (long?)x.Left).Append(source.Select(x => (long?)x.Left).Min()).Min() ?? 0;

    /// <summary>
    /// Finds maximum right value of the tree.
    /// </summary>
    /// <param name="source">DbSet instance.</param>
    static long MaxRight<TEntity>(this DbSet<TEntity> source) where TEntity : class, ITreeEntity
        => source.Local.Select(x => (long?)x.Right).Append(source.Select(x => (long?)x.Right).Max()).Max() ?? 0;

    static void CheckedDetached<TEntity>(this DbSet<TEntity> source, TEntity entity, string entityName) where TEntity : class, ITreeEntity
    {
        if (source.Entry(entity).State == EntityState.Detached)
            throw new ArgumentException($"{entityName} node has not been added yet.");
    }
}