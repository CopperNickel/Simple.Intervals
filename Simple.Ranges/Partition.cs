using System.Globalization;
using System.Text;

namespace Simple.Intervals;

/// <summary>
/// Partition (set of intervals)
/// </summary>
public sealed class Partition<T> : IFormattable {
  #region Comparers

  private sealed class EdgePointComparer(IComparer<T> comparer) : IComparer<(EndPoint<T> point, sbyte sign)> {
    private readonly IComparer<T> m_Comparer = comparer;

    public int Compare((EndPoint<T> point, sbyte sign) left, (EndPoint<T> point, sbyte sign) right) {
      if (left.point.IsInfinite && right.point.IsInfinite) {
        return left.sign.CompareTo(right.sign);
      }

      if (left.point.IsInfinite) {
        return left.sign;
      }

      if (right.point.IsInfinite) {
        return right.sign;
      }

      var result = m_Comparer.Compare(left.point.Value, right.point.Value);

      if (result != 0)
        return result;

      if (left.sign == right.sign && left.point.IsIncluded == right.point.IsIncluded)
        return 0;

      // Compare (
      if (!left.point.IsIncluded && left.sign > 0)
        return right.point.IsIncluded && right.sign < 0 ? -1 : +1;

      // Compare )
      if (!left.point.IsIncluded && left.sign < 0)
        return right.point.IsIncluded && right.sign > 0 ? +1 : -1;

      // Compare [
      if (left.point.IsIncluded && left.sign > 0)
        return -1;

      // Compare ]
      return +1;
    }
  }

  #endregion Comparers

  #region Fields Properties

  private readonly List<(EndPoint<T> point, sbyte sign)> m_Points = [];

  private IComparer<(EndPoint<T> point, sbyte sign)> PointComparer => field;

  #endregion Fields Properties

  #region Create

  /// <summary>
  /// Standard constructor
  /// </summary>
  /// <param name="comparer">Comparer to use</param>
  public Partition(IComparer<T>? comparer = default) {
    ValueComparer = comparer ?? Comparer<T>.Default ?? throw new ArgumentException($"No default comparer for {typeof(T).Name}", nameof(comparer));

    PointComparer = new EdgePointComparer(ValueComparer);
  }

  #endregion Create

  #region Public Properties and Methods 

  /// <summary>
  /// Value comparer
  /// </summary>
  public IComparer<T> ValueComparer { get; }

  /// <summary>
  /// Is Empty
  /// </summary>
  public bool IsEmpty => m_Points.Count == 0;

  /// <summary>
  /// Is Entire range
  /// </summary>
  public bool IsEntire => m_Points.Count == 0 && m_Points[0].point.IsInfinite && m_Points[1].point.IsInfinite;

  /// <summary>
  /// Add interval
  /// </summary>
  /// <param name="interval">Interval to add</param>
  /// <returns>True if interval has been added</returns>
  /// <exception cref="ArgumentException">When interval can't be added</exception>
  public bool Add(Interval<T> interval) {
    ArgumentNullException.ThrowIfNull(interval);

    if (!interval.ValueComparer.Equals(ValueComparer)) {
      throw new ArgumentException($"Incompatible interval", nameof(interval));
    }

    return AddRange(interval.Left, interval.Right);
  }

  #endregion Public Properties and Methods

  #region Private Methods

  private bool AddRange(EndPoint<T> left, EndPoint<T> right) {
    var leftPoint = (point: left, sign: (sbyte)+1);
    var rightPoint = (point: right, sign: (sbyte)-1);

    if (PointComparer.Compare(leftPoint, rightPoint) >= 0) {
      return false;
    }

    var indexLeft = m_Points.BinarySearch(leftPoint, PointComparer);

    if (indexLeft < 0) {
      indexLeft = ~indexLeft;

      m_Points.Insert(indexLeft, leftPoint);
    }
    else {
      m_Points[indexLeft] = leftPoint;
    }

    var indexRight = m_Points.BinarySearch(rightPoint, PointComparer);

    if (indexRight < 0) {
      indexRight = ~indexRight;

      m_Points.Insert(indexRight, rightPoint);
    }
    else {
      m_Points[indexRight] = rightPoint;
    }

    var removeLeft = indexLeft > 0 && m_Points[indexLeft - 1].sign > 0 ? indexLeft : indexLeft + 1;
    var removeRight = indexRight < m_Points.Count - 1 && m_Points[indexRight + 1].sign < 0 ? indexRight : indexRight - 1;

    m_Points.RemoveRange(removeLeft, removeRight - removeLeft + 1);

    return true;
  }

  #endregion Private Methods

  #region Formattable

  /// <summary>
  /// Partition as string
  /// </summary>
  /// <param name="format">Format to use for endpoints</param>
  /// <param name="formatProvider">Format provider</param>
  /// <returns>Partition as string</returns>
  public string ToString(string? format, IFormatProvider? formatProvider) {
    // return string.Join(" u ", m_Points.Select(MakePoint));
    var sb = new StringBuilder();

    foreach (var point in m_Points) {
      if (sb.Length > 0) {
        if (point.sign > 0)
          sb.Append(" u ");
        else
          sb.Append(" .. ");
      }

      sb.Append(MakePoint(point));
    }

    return sb.ToString();

    string MakePoint((EndPoint<T> point, sbyte sign) point) {
      return
          point.point.IsIncluded && point.sign > 0 ? "[" + point.point.ToString(format, formatProvider)
        : point.point.IsIncluded && point.sign < 0 ? point.point.ToString(format, formatProvider) + "]"
        : !point.point.IsIncluded && point.sign > 0 ? "(" + point.point.ToString(format, formatProvider)
        : !point.point.IsIncluded && point.sign < 0 ? point.point.ToString(format, formatProvider) + ")"
        : "<! " + point.point.ToString(format, formatProvider) + " !>";
    }
  }

  /// <summary>
  /// Interval as string
  /// </summary>
  /// <returns>Interval as string under invariant culture</returns>
  public override string ToString() {
    return ToString(null, CultureInfo.InvariantCulture);
  }

  #endregion Formattable
}


