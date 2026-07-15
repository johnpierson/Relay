using System.Reflection;

namespace Relay.Execution;

internal static class SynchronousDynamoEvaluator
{
    internal static DynamoExecutionOutcome Evaluate(object model, string adapterName)
    {
        ArgumentNullException.ThrowIfNull(model);
        object scheduler = model.GetType().GetProperty("Scheduler")?.GetValue(model);
        if (scheduler is null)
            return CompatibilityFailure(adapterName, "DynamoModel.Scheduler is unavailable.");

        PropertyInfo processModeProperty = scheduler.GetType().GetProperty("ProcessMode");
        if (processModeProperty?.CanRead != true || processModeProperty.CanWrite != true)
            return CompatibilityFailure(adapterName, "DynamoScheduler.ProcessMode is missing or not writable.");

        MethodInfo forceRun = model.GetType().GetMethod("ForceRun", Type.EmptyTypes);
        if (forceRun is null)
            return CompatibilityFailure(adapterName, "DynamoModel.ForceRun() is missing.");

        object previousMode = processModeProperty.GetValue(scheduler);
        object synchronousMode;
        try
        {
            synchronousMode = Enum.Parse(processModeProperty.PropertyType, "Synchronous");
        }
        catch (Exception exception)
        {
            return CompatibilityFailure(adapterName, $"DynamoScheduler.ProcessMode has no Synchronous value: {ExceptionDiagnostics.Describe(exception)}");
        }

        Exception invocationFailure = null;
        Exception restorationFailure = null;
        try
        {
            processModeProperty.SetValue(scheduler, synchronousMode);
            forceRun.Invoke(model, null);
        }
        catch (Exception exception)
        {
            invocationFailure = exception;
        }
        finally
        {
            try
            {
                processModeProperty.SetValue(scheduler, previousMode);
            }
            catch (Exception exception)
            {
                restorationFailure = exception;
            }
        }

        if (invocationFailure is not null)
        {
            string diagnostic = $"Dynamo invocation failed in the {adapterName} adapter: {ExceptionDiagnostics.Describe(invocationFailure)}";
            if (restorationFailure is not null)
                diagnostic += $" Scheduler restoration also failed: {ExceptionDiagnostics.Describe(restorationFailure)}";
            return DynamoExecutionOutcome.Failure(DynamoExecutionStage.Invocation, diagnostic);
        }

        if (restorationFailure is not null)
        {
            return DynamoExecutionOutcome.Failure(
                DynamoExecutionStage.Cleanup,
                $"Dynamo evaluation completed, but scheduler restoration failed in the {adapterName} adapter: {ExceptionDiagnostics.Describe(restorationFailure)}");
        }

        return DynamoExecutionOutcome.Success();
    }

    private static DynamoExecutionOutcome CompatibilityFailure(string adapterName, string diagnostic) =>
        DynamoExecutionOutcome.Failure(DynamoExecutionStage.Compatibility, $"Dynamo compatibility failure in the {adapterName} scheduler boundary: {diagnostic}");
}
