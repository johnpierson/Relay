using Relay.Execution;

namespace Relay.Tests;

public sealed class DynamoExecutionCoordinatorTests
{
    [Fact]
    public void EmptyBindingsLoadPausedThenEvaluateExactlyOnce()
    {
        var runner = new FakeRunner();
        var outcome = Execute(runner, Array.Empty<DynamoNodeBinding>());
        Assert.True(outcome.Succeeded);
        Assert.Equal(1, runner.Session.BindCount);
        Assert.Equal(1, runner.Session.EvaluateCount);
        Assert.True(runner.Session.Disposed);
    }

    [Fact]
    public void BindingFailurePreventsEvaluation()
    {
        var runner = new FakeRunner { Session = new FakeSession { BindingSucceeds = false } };
        var binding = new DynamoNodeBinding(Guid.NewGuid(), "Expected.Node", 42);
        var outcome = Execute(runner, new[] { binding });
        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Binding, outcome.Stage);
        Assert.Equal(0, runner.Session.EvaluateCount);
    }

    [Fact]
    public void MissingCompatibilityBoundaryPreventsLoad()
    {
        var runner = new FakeRunner { Compatible = false };
        var outcome = Execute(runner, Array.Empty<DynamoNodeBinding>());
        Assert.Equal(DynamoExecutionStage.Compatibility, outcome.Stage);
        Assert.Equal(0, runner.LoadCount);
        Assert.Contains("ForceRun", outcome.Diagnostic);
    }

    [Fact]
    public void CancellationAfterLoadPreventsBindingAndEvaluationAndDisposes()
    {
        var runner = new FakeRunner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var coordinator = new DynamoExecutionCoordinator();
        var outcome = coordinator.Execute(runner, "graph.dyn", false, Array.Empty<DynamoNodeBinding>(), cancellation.Token, out _);
        Assert.True(outcome.Cancelled);
        Assert.Equal(0, runner.Session.BindCount);
        Assert.Equal(0, runner.Session.EvaluateCount);
        Assert.True(runner.Session.Disposed);
    }

    [Fact]
    public void CleanupFailureDoesNotReplaceSuccessfulOutcome()
    {
        var runner = new FakeRunner { Session = new FakeSession { CleanupFailureValue = "cleanup failed" } };
        var coordinator = new DynamoExecutionCoordinator();
        var outcome = coordinator.Execute(runner, "graph.dyn", false, Array.Empty<DynamoNodeBinding>(), CancellationToken.None, out string cleanup);
        Assert.True(outcome.Succeeded);
        Assert.Equal("cleanup failed", cleanup);
    }

    [Fact]
    public void TrackerTransitionsOnlyAfterExplicitSuccess()
    {
        var tracker = new SuccessfulDocumentTracker<object>();
        var first = new object();
        var second = new object();
        Assert.False(tracker.RequiresModelTransition(first));
        Assert.False(tracker.RequiresModelTransition(second));
        tracker.RecordSuccess(first);
        Assert.False(tracker.RequiresModelTransition(first));
        Assert.True(tracker.RequiresModelTransition(second));
    }

    private static DynamoExecutionOutcome Execute(FakeRunner runner, IReadOnlyCollection<DynamoNodeBinding> bindings)
    {
        var coordinator = new DynamoExecutionCoordinator();
        return coordinator.Execute(runner, "graph.dyn", false, bindings, CancellationToken.None, out _);
    }

    private sealed class FakeRunner : IDynamoRunner
    {
        internal bool Compatible { get; init; } = true;
        internal int LoadCount { get; private set; }
        internal FakeSession Session { get; init; } = new();
        public DynamoCompatibilityResult Validate() => Compatible
            ? DynamoCompatibilityResult.Compatible()
            : DynamoCompatibilityResult.Incompatible("ForceRun is missing");
        public IDynamoExecutionSession LoadPaused(string graphPath, bool shutdownExistingModel)
        {
            LoadCount++;
            return Session;
        }
    }

    private sealed class FakeSession : IDynamoExecutionSession
    {
        internal bool BindingSucceeds { get; init; } = true;
        internal int BindCount { get; private set; }
        internal int EvaluateCount { get; private set; }
        internal bool Disposed { get; private set; }
        internal string CleanupFailureValue { get; init; }
        public string CleanupFailure => CleanupFailureValue;
        public DynamoExecutionOutcome ApplyBindings(IReadOnlyCollection<DynamoNodeBinding> bindings)
        {
            BindCount++;
            return BindingSucceeds ? DynamoExecutionOutcome.Success() : DynamoExecutionOutcome.Failure(DynamoExecutionStage.Binding, "binding failed");
        }
        public DynamoExecutionOutcome EvaluateOnce()
        {
            EvaluateCount++;
            return DynamoExecutionOutcome.Success();
        }
        public void Dispose() => Disposed = true;
    }
}
