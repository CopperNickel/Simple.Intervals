using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Simple.Intervals;

/// <summary>
/// Interval
/// </summary>
/// <typeparam name="T">Type of end points</typeparam>
public sealed class Interval<T> : IEquatable<Interval<T>>, IFormattable {
  #region Internal classes

  /// <summary>
  /// Options for interval formatting and parsing
  /// </summary>
  /// <param name="delimiter">Delimiter for interval formatting</param>
  public sealed class Options(string? delimiter) {
    /// <summary>
    /// Default delimiter
    /// </summary>
    public const string DefaultDelimiter = "..";

    /// <summary>
    /// Delimiter for interval formatting
    /// </summary>
    public string Delimiter { get; } = string.IsNullOrWhiteSpace(delimiter) ? DefaultDelimiter : delimiter.Trim();

    /// <summary>
    /// Default options
    /// </summary>
    public static Options Default { get; } = new(default);
  }

  #endregion Internal classes

  #region Comparers

  /// <summary>
  /// All Left Point comparers
  /// </summary>
  private static readonly ConcurrentDictionary<IComparer<T>, IComparer<EndPoint<T>>> s_LeftComparers = [];

  /// <summary>
  /// All Right point comparers
  /// </summary>
  private static readonly ConcurrentDictionary<IComparer<T>, IComparer<EndPoint<T>>> s_RightComparers = [];

  /// <summary>
  /// Left end points comparer
  /// </summary>
  private sealed class LeftEndPointComparer(IComparer<T> comparer) : IComparer<EndPoint<T>> {
    public int Compare(EndPoint<T>? x, EndPoint<T>? y) {
      if (ReferenceEquals(x, y)) {
        return 0;
      }

      if (x is null)
        return -1;

      if (y is null)
        return +1;

      if (x.IsInfinite)
        return -1;

      if (y.IsInfinite)
        return +1;

      var result = comparer.Compare(x.Value, y.Value);

      if (result == 0 && x.IsIncluded != y.IsIncluded)
        result = x.IsIncluded ? -1 : +1;

      return result;
    }
  }

  /// <summary>
  /// Right end points comparer
  /// </summary>
  private sealed class RightEndPointComparer(IComparer<T> comparer) : IComparer<EndPoint<T>> {
    public int Compare(EndPoint<T>? x, EndPoint<T>? y) {
      if (ReferenceEquals(x, y)) {
        return 0;
      }

      if (x is null)
        return -1;

      if (y is null)
        return +1;

      if (x.IsInfinite)
        return +1;

      if (y.IsInfinite)
        return -1;

      var result = comparer.Compare(x.Value, y.Value);

      if (result == 0 && x.IsIncluded != y.IsIncluded)
        result = x.IsIncluded ? +1 : -1;

      return result;
    }
  }

  #endregion Comparers

  #region Constructors and factory methods

  /// <summary>
  /// Standard constructor
  /// </summary>
  /// <param name="left">Left endpoint</param>
  /// <param name="right">Right endpoint</param>
  /// <param name="comparer">Comparer to use</param>
  /// <param name="formatOptions">Format options to use</param>
  /// <exception cref="ArgumentException">When comparer is not provided and type doesn't provide default provider</exception>
  public Interval(EndPoint<T> left, EndPoint<T> right, IComparer<T>? comparer = default, Options? formatOptions = default) {
    Left = left;
    Right = right;

    ValueComparer = comparer ?? Comparer<T>.Default ?? throw new ArgumentException($"No default comparer for {typeof(T).Name}", nameof(comparer));

    LeftComparer = s_LeftComparers.GetOrAdd(ValueComparer, _ => new LeftEndPointComparer(ValueComparer));
    RightComparer = s_RightComparers.GetOrAdd(ValueComparer, _ => new RightEndPointComparer(ValueComparer));
    FormatOptions = formatOptions ?? Options.Default;
  }

  /// <summary>
  /// Empty interval
  /// </summary>
  /// <param name="comparer">Comparer to use</param>
  /// <param name="formatOptions">Format options to use</param>
  /// <returns>Empty interval</returns>
  public static Interval<T> Empty(IComparer<T>? comparer = default, Options? formatOptions = default) {
    return new(new EndPoint<T>(default, false), new EndPoint<T>(default, false), comparer, formatOptions);
  }

  /// <summary>
  /// Entire interval
  /// </summary>
  /// <param name="comparer">Comparer to use</param>
  /// <param name="formatOptions">Format options to use</param>
  /// <returns>Entire interval</returns>
  public static Interval<T> Entire(IComparer<T>? comparer = default, Options? formatOptions = default) {
    return new(EndPoint<T>.Infinity, EndPoint<T>.Infinity, comparer, formatOptions);
  }

  /// <summary>
  /// One point interval
  /// </summary>
  /// <param name="value">Value</param>
  /// <param name="comparer">Comparer</param>
  /// <param name="formatOptions">Format options to use</param>
  /// <returns>One point interval</returns>
  public static Interval<T> IntervalFromPoint(T value, IComparer<T>? comparer = default, Options? formatOptions = default) {
    return new(new EndPoint<T>(value, true), new EndPoint<T>(value, true), comparer, formatOptions);
  }

  /// <summary>
  /// Create closed interval
  /// </summary>
  /// <param name="left">Left point</param>
  /// <param name="right">Right point</param>
  /// <param name="comparer">Comparer</param>
  /// <param name="formatOptions">Format options to use</param>
  /// <returns>Closed interval</returns>
  public static Interval<T> ClosedInterval(T left, T right, IComparer<T>? comparer = default, Options? formatOptions = default) {
    return new Interval<T>(new(left, true), new(right, true), comparer, formatOptions);
  }

  /// <summary>
  /// Create opened interval
  /// </summary>
  /// <param name="left">Left point</param>
  /// <param name="right">Right point</param>
  /// <param name="comparer">Comparer</param>
  /// <param name="formatOptions">Format options to use</param>
  /// <returns>Opened interval</returns>
  public static Interval<T> OpenedInterval(T left, T right, IComparer<T>? comparer = default, Options? formatOptions = default) {
    return new Interval<T>(new(left, false), new(right, false), comparer, formatOptions);
  }

  /// <summary>
  /// Try parse string into interval
  /// </summary>
  /// <param name="s">Text to parse</param>
  /// <param name="result">Parsed interval or null</param>
  /// <param name="options">Options to use</param>
  /// <param name="provider">Format provider to use</param>
  /// <param name="parser">Parser to use</param>
  /// <param name="comparer">Comparer to use</param>
  /// <returns>True, if parsed; false otherwise</returns>
  public static bool TryParse(
     ReadOnlySpan<char> s,
     [MaybeNullWhen(false)] out Interval<T> result,
     Options? options = default,
     IFormatProvider? provider = default,
     Func<string, IFormatProvider?, (bool valid, T? value)>? parser = default,
     IComparer<T>? comparer = default) {
    result = null;

    provider ??= CultureInfo.InvariantCulture;

    parser ??= GetParser();

    comparer ??= Comparer<T>.Default;

    if (comparer is null || parser is null) {
      return false;
    }

    options ??= Options.Default;

    using var parts = s.Split(options.Delimiter);

    var index = 0;

    EndPoint<T>? left = null;
    EndPoint<T>? right = null;

    while (parts.MoveNext()) {
      var span = s[parts.Current.Start.Value..parts.Current.End.Value].Trim();

      if (index == 0) {
        var (valid, prefix, value) = ParseValue(span.ToString(), true, provider, parser);

        if (!valid)
          return false;

        if (prefix != '(' && prefix != '[' && prefix != ']')
          return false;

        left = span.Length == 1 ? EndPoint<T>.Infinity : new EndPoint<T>(value, prefix == '[');
      }
      else if (index == 1) {
        var (valid, prefix, value) = ParseValue(span.ToString(), false, provider, parser);

        if (!valid)
          return false;

        if (prefix != ')' && prefix != ']' && prefix != '[')
          return false;

        right = span.Length == 1 ? EndPoint<T>.Infinity : new EndPoint<T>(value, prefix == ']');
      }
      else
        return false;

      index += 1;
    }

    if (left is not null && right is not null) {
      result = new Interval<T>(left, right, comparer, options);

      return true;
    }

    return false;
  }

  /// <summary>
  /// Try parse string into interval
  /// </summary>
  /// <param name="s">Text to parse</param>
  /// <param name="result">Parsed interval or null</param>
  /// <param name="options">Options to use</param>
  /// <param name="provider">Format provider to use</param>
  /// <param name="parser">Parser to use</param>
  /// <param name="comparer">Comparer to use</param>
  /// <returns>True, if parsed; false otherwise</returns>
  public static bool TryParse(
    string s,
    [MaybeNullWhen(false)] out Interval<T> result,
    Options? options = default,
    IFormatProvider? provider = default,
    Func<string, IFormatProvider?, (bool valid, T? value)>? parser = default,
    IComparer<T>? comparer = default) {
    return TryParse(s.AsSpan(), out result, options, provider, parser, comparer);
  }

  /// <summary>
  /// Parse string into interval
  /// </summary>
  /// <param name="s">Text to parse</param>
  /// <param name="options">Options to use</param>
  /// <param name="provider">Format provider to use</param>
  /// <param name="parser">Parser to use</param>
  /// <param name="comparer">Comparer to use</param>
  /// <returns>Parsed interval</returns>
  /// <exception cref="FormatException">When text can't be parsed</exception>
  public static Interval<T> Parse(
    ReadOnlySpan<char> s,
    Options? options = default,
    IFormatProvider? provider = default,
    Func<string, IFormatProvider?, (bool valid, T? value)>? parser = default,
    IComparer<T>? comparer = default) {
    return TryParse(s, out var result, options, provider, parser, comparer)
        ? result
        : throw new FormatException("Text can't be parsed into interval");
  }

  /// <summary>
  /// Parse string into interval
  /// </summary>
  /// <param name="s">Text to parse</param>
  /// <param name="options">Options to use</param>
  /// <param name="provider">Format provider to use</param>
  /// <param name="parser">Parser to use</param>
  /// <param name="comparer">Comparer to use</param>
  /// <returns>Parsed interval</returns>
  /// <exception cref="FormatException">When text can't be parsed</exception>
  public static Interval<T> Parse(
    string s,
    Options? options = default,
    IFormatProvider? provider = default,
    Func<string, IFormatProvider?, (bool valid, T? value)>? parser = default,
    IComparer<T>? comparer = default) {
    return Parse(s.AsSpan(), options, provider, parser, comparer);
  }

  #endregion Constructors and factory methods

  #region Public Properties and methods

  /// <summary>
  /// Format Options
  /// </summary>
  public Options FormatOptions { get; } = Options.Default;

  /// <summary>
  /// Left end point
  /// </summary>
  public EndPoint<T> Left { get; }

  /// <summary>
  /// Right end point
  /// </summary>
  public EndPoint<T> Right { get; }

  /// <summary>
  /// Value comparer
  /// </summary>
  public IComparer<T> ValueComparer { get; }

  /// <summary>
  /// Left endpoint comparer
  /// </summary>
  public IComparer<EndPoint<T>> LeftComparer { get; }

  /// <summary>
  /// Right endpoint comparer
  /// </summary>
  public IComparer<EndPoint<T>> RightComparer { get; }

  /// <summary>
  /// Is Empty
  /// </summary>
  public bool IsEmpty {
    get {
      if (Left.IsInfinite || Right.IsInfinite)
        return false;

      var compare = ValueComparer.Compare(Left.Value, Right.Value);

      if (compare < 0)
        return false;

      if (compare > 0)
        return true;

      return !Left.IsIncluded || !Right.IsIncluded;
    }
  }

  /// <summary>
  /// Is Entire interval
  /// </summary>
  public bool IsEntire => Left.IsInfinite && Right.IsInfinite;

  /// <summary>
  /// Point in relation to interval
  /// </summary>
  /// <param name="value">Point to test</param>
  /// <returns>-1 if point is in the left to the interval, 0 if point is within interval, +1 if point is to the right to interval</returns>
  public int Relation(T value) {
    var leftBorder = true;

    if (!Left.IsInfinite) {
      var compare = ValueComparer.Compare(Left.Value, value);

      leftBorder = compare < 0 || compare == 0 && Left.IsIncluded;
    }

    if (!leftBorder)
      return -1;

    var rightBorder = true;

    if (!Right.IsInfinite) {
      var compare = ValueComparer.Compare(Right.Value, value);

      rightBorder = compare > 0 || compare == 0 && Right.IsIncluded;
    }

    if (!rightBorder)
      return +1;

    return 0;
  }

  /// <summary>
  /// If interval contains the point
  /// </summary>
  /// <param name="value">Point to test</param>
  /// <returns>True, if point is within the interval</returns>
  public bool Contains(T value) => Relation(value) == 0;

  /// <summary>
  /// Regex matching 
  /// </summary>
  /// <returns>Regex to match intervals within text</returns>
  public Regex Matcher() {
    return new Regex(@$"[\(\[]\.*{Regex.Escape(FormatOptions.Delimiter)}\.*[\)\]]", RegexOptions.NonBacktracking);
  }

  #endregion Public Properties  and methods

  #region IEquatable<Interval<T>>

  /// <summary>
  /// Equals
  /// </summary>
  /// <param name="other">Other interval to compare with</param>
  /// <returns>True, if intervals are equal, false otherwise</returns>
  public bool Equals(Interval<T>? other) {
    if (ReferenceEquals(this, other))
      return true;

    if (other is null)
      return false;

    return Equals(ValueComparer, other.ValueComparer) && Equals(Left, other.Left) && Equals(Right, other.Right);
  }

  /// <summary>
  /// Equals
  /// </summary>
  /// <param name="obj">Other interval to compare with</param>
  /// <returns>True, if intervals are equal, false otherwise</returns>
  public override bool Equals(object? obj) => (obj is Interval<T> other) && Equals(other);

  /// <summary>
  /// Compute hash code
  /// </summary>
  /// <returns>Hash Code</returns>
  public override int GetHashCode() => HashCode.Combine(ValueComparer, Left, Right);

  #endregion IEquatable<Interval<T>>

  #region IFormattable

  /// <summary>
  /// To String
  /// </summary>
  /// <param name="format">Format</param>
  /// <param name="formatProvider">Format provider</param>
  /// <returns>Interval as string</returns>
  public string ToString(string? format, IFormatProvider? formatProvider) {
    var sb = new StringBuilder();

    sb.Append(Left.IsIncluded ? '[' : '(');

    sb.Append(Left.ToString(format, formatProvider));

    sb.Append(FormatOptions.Delimiter);

    sb.Append(Right.ToString(format, formatProvider));

    sb.Append(Right.IsIncluded ? ']' : ')');

    return sb.ToString();
  }

  /// <summary>
  /// Interval as string
  /// </summary>
  /// <returns>Interval as string under invariant culture</returns>
  public override string ToString() {
    return ToString(null, CultureInfo.InvariantCulture);
  }

  #endregion IFormattable

  #region Private methods

  private static Func<string, IFormatProvider?, (bool valid, T? value)>? GetParser() {
    var type = typeof(T);

    var tryParseMethod = type.GetMethod(
        "TryParse",
        BindingFlags.Public | BindingFlags.Static,
        binder: null,
        types: [typeof(string), typeof(IFormatProvider), type.MakeByRefType()],
        modifiers: null);

    if (tryParseMethod is null)
      return GetParserNoProvider();

    return (text, provider) => {
      object?[] args = [text, provider, null];

      var success = (bool)tryParseMethod.Invoke(null, args)!;
      var parsedValue = args[2];

      return success
          ? (true, (T?)parsedValue)
          : (false, default);
    };
  }

  private static Func<string, IFormatProvider?, (bool valid, T? value)>? GetParserNoProvider() {
    var type = typeof(T);

    var tryParseMethod = type.GetMethod(
        "TryParse",
        BindingFlags.Public | BindingFlags.Static,
        binder: null,
        types: [typeof(string), type.MakeByRefType()],
        modifiers: null);

    if (tryParseMethod is null)
      return null;

    return (text, _) => {
      object?[] args = [text, null];

      var success = (bool)tryParseMethod.Invoke(null, args)!;
      var parsedValue = args[1];

      return success
          ? (true, (T?)parsedValue)
          : (false, default);
    };
  }

  private static (bool valid, char prefix, T? value) ParseValue(
    string text,
    bool isFirst,
    IFormatProvider? provider,
    Func<string, IFormatProvider?, (bool valid, T? value)> parser) {

    if (text.Length == 0) {
      return (false, '\0', default);
    }

    var point = isFirst
        ? text[1..]
        : text[..^1];

    var prefix = isFirst
        ? text[0]
        : text[^1];

    if (point.Length == 0) {
      return (true, prefix, default);
    }

    var (valid, value) = parser(point, provider);

    return (valid, prefix, value);
  }

  #endregion Private methods
}

