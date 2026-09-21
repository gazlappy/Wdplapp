using Wdpl2.Services;

// Deliberately in the global namespace: the test classes that use this are
// split between the wdpl2.Tests and Wdpl2.Tests namespaces.

/// <summary>
/// Every test class that builds its own <see cref="SqliteDataStore"/>.
/// </summary>
/// <remarks>
/// <see cref="SqliteDataStore"/> caches its snapshot process-wide, which is
/// right for the app - every instance reads the same league file - and wrong
/// for a test suite, where each test has its own in-memory database. Run in
/// parallel, or one after another without clearing the cache, one test is
/// served another test's seasons ("Choose an existing destination season").
/// So these classes run on their own, and each test starts from an empty cache.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedDataStoreCollection
{
    public const string Name = "Shared data store";
}

/// <summary>
/// Base for tests in <see cref="SharedDataStoreCollection"/>. xUnit builds a
/// new instance for every test, so this constructor runs before each one.
/// </summary>
public abstract class SharedDataStoreTest
{
    protected SharedDataStoreTest() => SqliteDataStore.ResetSharedSnapshotForTests();
}
