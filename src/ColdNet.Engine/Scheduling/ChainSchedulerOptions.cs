namespace ColdNet.Engine.Scheduling;

/// <summary>
/// Bound from configuration section "ColdNet:Worker". <see cref="WorkerName"/> assigns chains to
/// this worker process: it only picks up chains whose <c>ProcessChain.WorkerName</c> matches, so
/// several worker processes/hosts can share the load without two of them ever touching the same
/// chain.
/// </summary>
public class ChainSchedulerOptions
{
    public string WorkerName { get; set; } = "default";
}
