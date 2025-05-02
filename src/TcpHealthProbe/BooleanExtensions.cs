namespace Innago.Shared.HealthChecks.TcpHealthProbe;

using System;
using System.Threading.Tasks;

internal static class BooleanExtensions
{
    public static Task MatchAsync(this bool value, Func<Task> onTrue, Func<Task> onFalse)
    {
        return FunctionToRun()();
        
        Func<Task> FunctionToRun()
        {
            return value ? onTrue : onFalse;
        }
    }
}