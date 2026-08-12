namespace Simple.Intervals;

/// <summary>
/// End Point
/// </summary>
/// <typeparam name="T">Value</typeparam>
/// <remarks>
/// Creates end point
/// </remarks>
/// <param name="value">Value</param>
/// <param name="isIncluded">If included into interval</param>
public sealed class EndPoint<T>(T? value, bool isIncluded) : IFormattable, IEquatable<EndPoint<T>> {
  #region Constructors and predefined values

  /// <summary>
  /// Positive infinity
  /// </summary>
  public static EndPoint<T> Infinity { get; } = new(default, false);

  #endregion Constructors and predefined values

  #region Public properties

  /// <summary>
  /// Value
  /// </summary>
  public T? Value { get; } = value;

  /// <summary>
  /// Is included
  /// </summary>
  public bool IsIncluded { get; } = isIncluded;

  /// <summary>
  /// Is finite
  /// </summary>
  public bool IsFinite => !ReferenceEquals(this, Infinity);

  /// <summary>
  /// Is infinite
  /// </summary>
  public bool IsInfinite => ReferenceEquals(this, Infinity);

  #endregion Public properties

  #region IFormattable

  /// <summary>
  /// To String
  /// </summary>
  /// <param name="format"></param>
  /// <param name="formatProvider"></param>
  /// <returns></returns>
  public string ToString(string? format, IFormatProvider? formatProvider) {
    if (IsInfinite || Value is null) {
      return "";
    }

    return Value is IFormattable formattable
        ? formattable.ToString(format, formatProvider)
        : Value.ToString() ?? "";
  }

  /// <summary>
  /// To String
  /// </summary>
  /// <returns>Debug information</returns>
  public override string ToString() {
    return IsFinite
        ? $"{Value} ({(IsIncluded ? "Included" : "Excluded")})"
        : "Infinity";
  }

  #endregion IFormattable

  #region IEquatable<EndPoint<T>>

  /// <summary>
  /// Equals
  /// </summary>
  /// <param name="other">Object to compare with</param>
  /// <returns>True if objects are equal, false otherwise</returns>
  public bool Equals(EndPoint<T>? other) {
    if (ReferenceEquals(other, this))
      return true;

    if (other is null)
      return false;

    return
        IsFinite == other.IsFinite &&
        Equals(Value, other.Value) &&
        IsIncluded == other.IsIncluded;
  }

  /// <summary>
  /// Equals
  /// </summary>
  /// <param name="obj">Object to compare with</param>
  /// <returns>True if objects are equal, false otherwise</returns>
  public override bool Equals(object? obj) => (obj is EndPoint<T> other) && Equals(other);

  /// <summary>
  /// Hash code
  /// </summary>
  /// <returns>Hash Code</returns>
  public override int GetHashCode() {
    return IsInfinite
        ? -1
        : HashCode.Combine(Value?.GetHashCode() ?? 0, IsIncluded);
  }

  #endregion IEquatable<EndPoint<T>>
}
