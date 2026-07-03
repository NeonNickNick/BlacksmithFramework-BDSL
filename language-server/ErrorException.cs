namespace BdslValidator
{
    internal record struct ErrorSpan(int Line, int StartColumn, int EndColumn);
    internal record struct Error(List<ErrorSpan> ErrorSpans, string Message);
    internal class ErrorException(Error error) : Exception
    {
        public Error Error { get; } = error;
    }
}
