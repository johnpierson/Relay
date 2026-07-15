using Relay.Execution;

namespace Relay.Tests;

public sealed class DynamoExecutionCoordinatorTests
{
    [Fact]
    public void EmptyBindingsExecuteSynchronouslyWithoutCreatingPausedSession()
    {
        var runner = new FakeRunner();
        var outcome = Execute(runner, Array.Empty<DynamoNodeBinding>());
        Assert.True(outcome.Succeeded);
        Assert.Equal(1, runner.DirectExecuteCount);
        Assert.Equal(0, runner.LoadCount);
        Assert.False(runner.Session.Disposed);
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
    public void LoadFailureReportsDeepestExceptionInsteadOfInvocationWrapper()
    {
        var runner = new FakeRunner
        {
            LoadException = new System.Reflection.TargetInvocationException(
                new UnauthorizedAccessException("runtime detail"))
        };

        var binding = new DynamoNodeBinding(Guid.NewGuid(), "Expected.Node", 42);
        var outcome = Execute(runner, new[] { binding });

        Assert.Equal(DynamoExecutionStage.Load, outcome.Stage);
        Assert.Contains("UnauthorizedAccessException", outcome.Diagnostic);
        Assert.Contains("runtime detail", outcome.Diagnostic);
        Assert.DoesNotContain("target of an invocation", outcome.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CancellationBeforeExecutionDoesNotCreateSession()
    {
        var runner = new FakeRunner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var coordinator = new DynamoExecutionCoordinator();
        var outcome = coordinator.Execute(runner, "graph.dyn", false, Array.Empty<DynamoNodeBinding>(), cancellation.Token, out _);
        Assert.True(outcome.Cancelled);
        Assert.Equal(0, runner.Session.BindCount);
        Assert.Equal(0, runner.Session.EvaluateCount);
        Assert.False(runner.Session.Disposed);
        Assert.Equal(0, runner.LoadCount);
        Assert.Equal(0, runner.DirectExecuteCount);
    }

    [Fact]
    public void CleanupFailureDoesNotReplaceSuccessfulOutcome()
    {
        var runner = new FakeRunner { Session = new FakeSession { CleanupFailureValue = "cleanup failed" } };
        var coordinator = new DynamoExecutionCoordinator();
        var binding = new DynamoNodeBinding(Guid.NewGuid(), "Expected.Node", 42);
        var outcome = coordinator.Execute(runner, "graph.dyn", false, new[] { binding }, CancellationToken.None, out string cleanup);
        Assert.True(outcome.Succeeded);
        Assert.Equal("cleanup failed", cleanup);
    }

    [Fact]
    public void DirectExecutionFailureReportsDeepestException()
    {
        var runner = new FakeRunner
        {
            DirectException = new System.Reflection.TargetInvocationException(
                new InvalidOperationException("evaluation detail"))
        };

        var outcome = Execute(runner, Array.Empty<DynamoNodeBinding>());

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Invocation, outcome.Stage);
        Assert.Contains("InvalidOperationException", outcome.Diagnostic);
        Assert.Contains("evaluation detail", outcome.Diagnostic);
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
        internal int DirectExecuteCount { get; private set; }
        internal int LoadCount { get; private set; }
        internal FakeSession Session { get; init; } = new();
        internal Exception LoadException { get; init; }
        internal Exception DirectException { get; init; }
        public DynamoCompatibilityResult Validate() => Compatible
            ? DynamoCompatibilityResult.Compatible()
            : DynamoCompatibilityResult.Incompatible("ForceRun is missing");
        public DynamoExecutionOutcome ExecuteDirect(string graphPath, bool shutdownExistingModel)
        {
            DirectExecuteCount++;
            if (DirectException is not null) throw DirectException;
            return DynamoExecutionOutcome.Success();
        }
        public IDynamoExecutionSession LoadPaused(string graphPath, bool shutdownExistingModel)
        {
            LoadCount++;
            if (LoadException is not null) throw LoadException;
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
