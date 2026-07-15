using Relay.Execution;

namespace Relay.Tests;

public sealed class SynchronousDynamoEvaluatorTests
{
    [Fact]
    public void EvaluationRunsSynchronouslyAndRestoresSchedulerMode()
    {
        var model = new FakeModel();

        DynamoExecutionOutcome outcome = SynchronousDynamoEvaluator.Evaluate(model, "test");

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, model.ForceRunCount);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
        Assert.Equal(FakeProcessMode.Synchronous, model.ModeObservedDuringRun);
    }

    [Fact]
    public void InvocationFailureStillRestoresSchedulerMode()
    {
        var model = new FakeModel { RunException = new InvalidOperationException("node failure") };

        DynamoExecutionOutcome outcome = SynchronousDynamoEvaluator.Evaluate(model, "test");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Invocation, outcome.Stage);
        Assert.Contains("node failure", outcome.Diagnostic);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
    }

    [Fact]
    public void MissingSchedulerReportsCompatibilityFailure()
    {
        var outcome = SynchronousDynamoEvaluator.Evaluate(new ModelWithoutScheduler(), "test");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Compatibility, outcome.Stage);
        Assert.Contains("Scheduler", outcome.Diagnostic);
    }

    private enum FakeProcessMode
    {
        Synchronous,
        Asynchronous
    }

    private sealed class FakeScheduler
    {
        public FakeProcessMode ProcessMode { get; set; } = FakeProcessMode.Asynchronous;
    }

    private sealed class FakeModel
    {
        public FakeScheduler Scheduler { get; } = new();
        internal int ForceRunCount { get; private set; }
        internal FakeProcessMode? ModeObservedDuringRun { get; private set; }
        internal Exception RunException { get; init; }

        public void ForceRun()
        {
            ForceRunCount++;
            ModeObservedDuringRun = Scheduler.ProcessMode;
            if (RunException is not null) throw RunException;
        }
    }

    private sealed class ModelWithoutScheduler
    {
        public void ForceRun()
        {
        }
    }
}
