using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DSA.Tests.Infrastructure;

public sealed class AlphabeticalOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        return testCases.OrderBy(tc => tc.TestMethod.Method.Name, StringComparer.OrdinalIgnoreCase);
    }
}
