using Xunit;

// WebApplicationFactory<Program> with top-level statements is not safe to build concurrently
// across multiple factories in the same process: the HostFactoryResolver runs the shared
// entry point, and parallel builds intermittently fail with
// "The entry point exited without ever building an IHost." Run test collections sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
