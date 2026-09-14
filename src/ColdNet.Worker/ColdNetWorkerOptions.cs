namespace ColdNet.Worker;

public class ColdNetWorkerOptions
{
    public string WorkerName { get; set; } = "default";

    public int PollIntervalSeconds { get; set; } = 5;
}
