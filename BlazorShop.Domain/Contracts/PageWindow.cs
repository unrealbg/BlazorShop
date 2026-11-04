namespace BlazorShop.Domain.Contracts
{
    /// <summary>
    /// Overflow-safe arithmetic for 1-based page number / page size windows.
    /// EF Core Skip/Take accept <see cref="int"/>, so a window is representable only
    /// when its zero-based offset fits in <see cref="int"/>.
    /// </summary>
    public static class PageWindow
    {
        /// <summary>
        /// Returns the zero-based row offset of a page using 64-bit arithmetic.
        /// Values below one are treated as one.
        /// </summary>
        public static long GetOffset(int pageNumber, int pageSize)
        {
            var safePageNumber = Math.Max(1, pageNumber);
            var safePageSize = Math.Max(1, pageSize);
            return ((long)safePageNumber - 1) * safePageSize;
        }

        /// <summary>
        /// Returns whether the page window can be passed to an <see cref="int"/>-based Skip.
        /// </summary>
        public static bool IsRepresentable(int pageNumber, int pageSize) =>
            GetOffset(pageNumber, pageSize) <= int.MaxValue;

        /// <summary>
        /// Returns a non-negative Skip value. Unrepresentable windows saturate at
        /// <see cref="int.MaxValue"/>, which yields an empty page instead of an overflow.
        /// </summary>
        public static int GetSkip(int pageNumber, int pageSize) =>
            (int)Math.Min(GetOffset(pageNumber, pageSize), int.MaxValue);
    }
}
