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
        Assert.Equal(1, model.CurrentWorkspace.RunCount);
        Assert.Equal(1, model.ResetEngineCount);
        Assert.False(model.MarkNodesAsDirtyDuringReset);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
        Assert.Equal(FakeProcessMode.Synchronous, model.ModeObservedDuringRun);
        Assert.Equal(1, model.CurrentWorkspace.EvaluationCount);
        Assert.All(model.CurrentWorkspace.Nodes, node => Assert.True(node.ForceExecute));
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

    [Fact]
    public void EvaluationThatDoesNotRunReportsRunGates()
    {
        var model = new FakeModel { SkipEvaluation = true };

        DynamoExecutionOutcome outcome = SynchronousDynamoEvaluator.Evaluate(model, "test");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Invocation, outcome.Stage);
        Assert.Contains("did not evaluate", outcome.Diagnostic);
        Assert.Contains("RunEnabled=True", outcome.Diagnostic);
        Assert.Contains("ForceBlockRun=False", outcome.Diagnostic);
        Assert.Contains("Nodes=2, Modified=2, ForceExecution=2, InError=0", outcome.Diagnostic);
        Assert.Contains("No Dynamo/ProtoCore first-chance exception", outcome.Diagnostic);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
    }

    [Fact]
    public void MissingNodeForceExecutionSurfaceReportsCompatibilityFailure()
    {
        var outcome = SynchronousDynamoEvaluator.Evaluate(new ModelWithIncompatibleNode(), "test");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Compatibility, outcome.Stage);
        Assert.Contains("MarkNodeAsModified", outcome.Diagnostic);
    }

    [Fact]
    public void GraphIsOpenedPausedWhileTheSchedulerIsSynchronous()
    {
        var model = new FakeModel();

        DynamoExecutionOutcome outcome = SynchronousDynamoEvaluator.Evaluate(model, "test", @"C:\graphs\a.dyn");

        Assert.True(outcome.Succeeded);
        Assert.Equal(@"C:\graphs\a.dyn", model.OpenedPath);
        // Paused: Relay runs the graph itself once the engine is attached.
        Assert.True(model.OpenedForceManual);
        // Under the asynchronous scheduler the open is queued for Revit's Idling loop,
        // which never runs while the command holds the UI thread.
        Assert.Equal(FakeProcessMode.Synchronous, model.ModeObservedDuringOpen);
        Assert.Equal(1, model.CurrentWorkspace.RunCount);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
    }

    [Fact]
    public void EmptyWorkspaceAfterOpenIsReportedAsAFailedLoad()
    {
        // The Revit 2027 failure: DynamoRevit ignored the dynPath journal key, so the
        // model sat on an empty workspace and "evaluated" nothing.
        var model = new FakeModel { OpenLeavesWorkspaceEmpty = true };

        DynamoExecutionOutcome outcome = SynchronousDynamoEvaluator.Evaluate(model, "test", @"C:\graphs\a.dyn");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Load, outcome.Stage);
        Assert.Contains("contains no nodes", outcome.Diagnostic);
        Assert.Contains(@"C:\graphs\a.dyn", outcome.Diagnostic);
        Assert.Equal(0, model.CurrentWorkspace.RunCount);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
    }

    [Fact]
    public void OpenFailureRestoresSchedulerModeAndReportsTheLoadStage()
    {
        var model = new FakeModel { OpenException = new IOException("the file is locked") };

        DynamoExecutionOutcome outcome = SynchronousDynamoEvaluator.Evaluate(model, "test", @"C:\graphs\a.dyn");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Load, outcome.Stage);
        Assert.Contains("the file is locked", outcome.Diagnostic);
        Assert.Equal(FakeProcessMode.Asynchronous, model.Scheduler.ProcessMode);
    }

    [Fact]
    public void MissingOpenFileSurfaceReportsCompatibilityFailure()
    {
        var outcome = SynchronousDynamoEvaluator.Evaluate(new ModelWithIncompatibleNode(), "test", @"C:\graphs\a.dyn");

        Assert.False(outcome.Succeeded);
        Assert.Equal(DynamoExecutionStage.Compatibility, outcome.Stage);
        Assert.Contains("OpenFileFromPath", outcome.Diagnostic);
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

    private sealed class FakeRunSettings
    {
        public bool RunEnabled { get; set; } = true;
        internal static bool ForceBlockRun { get; set; }
    }

    private sealed class FakeNode
    {
        public bool ForceExecute { get; private set; }
        public bool IsModified => ForceExecute;
        public bool NeedsForceExecution => ForceExecute;
        public bool IsInErrorState => false;
        public bool IsFrozen => false;
        public bool IsTransient => false;
        public string State => "Active";
        public void MarkNodeAsModified(bool forceExecute) => ForceExecute = forceExecute;
    }

    private sealed class FakeWorkspace
    {
        private readonly FakeModel model;

        internal FakeWorkspace(FakeModel model) => this.model = model;

        public long EvaluationCount { get; private set; }
        public bool HasRunWithoutCrash { get; private set; } = true;
        public bool GraphRunInProgress { get; private set; }
        public object EngineController { get; internal set; }
        internal bool IsEvaluationPending => false;
        public FakeRunSettings RunSettings { get; } = new();
        public FakeNode[] Nodes { get; internal set; } = new[] { new FakeNode(), new FakeNode() };
        internal int RunCount { get; private set; }

        public void Run()
        {
            RunCount++;
            model.ModeObservedDuringRun = model.Scheduler.ProcessMode;
            if (model.RunException is not null) throw model.RunException;
            if (!model.SkipEvaluation) EvaluationCount++;
        }
    }

    private sealed class FakeModel
    {
        internal FakeModel() => CurrentWorkspace = new FakeWorkspace(this);

        public FakeScheduler Scheduler { get; } = new();
        public FakeWorkspace CurrentWorkspace { get; }
        internal int ResetEngineCount { get; private set; }
        internal bool MarkNodesAsDirtyDuringReset { get; private set; }
        internal FakeProcessMode? ModeObservedDuringRun { get; set; }
        internal Exception RunException { get; init; }
        internal bool SkipEvaluation { get; init; }

        internal Exception OpenException { get; init; }
        internal bool OpenLeavesWorkspaceEmpty { get; init; }
        internal string OpenedPath { get; private set; }
        internal bool OpenedForceManual { get; private set; }
        internal FakeProcessMode? ModeObservedDuringOpen { get; private set; }

        public void OpenFileFromPath(string filePath, bool forceManualExecutionMode)
        {
            OpenedPath = filePath;
            OpenedForceManual = forceManualExecutionMode;
            ModeObservedDuringOpen = Scheduler.ProcessMode;
            if (OpenException is not null) throw OpenException;
            if (OpenLeavesWorkspaceEmpty) CurrentWorkspace.Nodes = Array.Empty<FakeNode>();
        }

        public void ResetEngine(bool markNodesAsDirty)
        {
            ResetEngineCount++;
            MarkNodesAsDirtyDuringReset = markNodesAsDirty;
            CurrentWorkspace.EngineController = new object();
        }
    }

    private sealed class ModelWithoutScheduler
    {
        public object CurrentWorkspace { get; } = new();
    }

    private sealed class ModelWithIncompatibleNode
    {
        public FakeScheduler Scheduler { get; } = new();
        public IncompatibleWorkspace CurrentWorkspace { get; } = new();
        public void ResetEngine(bool markNodesAsDirty)
        {
        }
    }

    private sealed class IncompatibleWorkspace
    {
        public long EvaluationCount => 0;
        public FakeRunSettings RunSettings { get; } = new();
        public object[] Nodes { get; } = new object[] { new IncompatibleNode() };
        public void Run()
        {
        }
    }

    private sealed class IncompatibleNode
    {
    }
}
