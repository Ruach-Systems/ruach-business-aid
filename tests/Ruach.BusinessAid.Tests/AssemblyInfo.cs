using Xunit;

// SQL integration tests deliberately share one dedicated database. Keep the
// complete acceptance suite deterministic and free of cross-test schema locks.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
