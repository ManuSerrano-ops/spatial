using Xunit;

namespace PlanoOpenSpaceIT.Domain.Tests;

internal static class DomainTestSupport
{
    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message) =>
        Assert.True(expected.SequenceEqual(actual), message);

    public static T Single<T>(IEnumerable<T> values, Func<T, bool> predicate) =>
        Assert.Single(values, value => predicate(value));
}
