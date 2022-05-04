namespace Microsoft.AspNetCore.Http;

// Working aronud missing Results.Empty in .NET 6
public static class ResultExtensions
{
    private static readonly EmptyResult _empty = new();
    public static IResult Empty(this IResultExtensions extensions) => _empty;

    private class EmptyResult : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
    }
}
