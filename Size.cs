namespace Alienlab.NetExtensions
{
  public struct Size
  {
    private const long Kilobyte = 1024;

    private const long Megabyte = 1024 * Kilobyte;

    private const long Gigabyte = 1024 * Megabyte;

    private Size(long bytes)
    {
      this.Bytes = bytes;
    }

    public long Bytes { get; }

    public long Kilobytes => this.Bytes / Kilobyte;

    public long Megabytes => this.Bytes / Megabyte;

    public long Gigabytes => this.Bytes / Gigabyte;

    public static Size FromBytes(long bytes) => new Size(bytes);

    public static Size FromKilobytes(long kilobytes) => new Size(kilobytes * Kilobyte);

    public static Size FromMegabytes(long megabytes) => new Size(megabytes * Megabyte);

    public static Size FromGigabytes(long gigabytes) => new Size(gigabytes * Gigabyte);

    public static Size FromBytes([NotNull] string bytes) => FromBytes(long.Parse(bytes));

    public static Size FromKilobytes([NotNull] string kilobytes) => FromKilobytes(long.Parse(kilobytes));

    public static Size FromMegabytes([NotNull] string megabytes) => FromMegabytes(long.Parse(megabytes));

    public static Size FromGigabytes([NotNull] string gigabytes) => FromGigabytes(long.Parse(gigabytes));

    public static Size operator + (Size left, Size right) => new Size(left.Bytes + right.Bytes);

    public static Size operator -(Size left, Size right) => new Size(left.Bytes - right.Bytes);

    public static implicit operator long (Size size) => size.Bytes;

    /// <summary>
    /// Returns the fully qualified type name of this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.String"/> containing a fully qualified type name.
    /// </returns>
    public override string ToString()
    {
      if (this.Gigabytes > 0)
      {
        return string.Format("{0:F1}GB", (double)this.Bytes / Gigabyte);
      }

      if (this.Megabytes > 0)
      {
        return string.Format("{0:F1}MB", (double)this.Bytes / Megabyte);
      }

      if (this.Kilobytes > 0)
      {
        return string.Format("{0:F1}KB", (double)this.Bytes / Kilobyte);
      }

      return string.Format("{0}B", this.Bytes);
    }
  }
}