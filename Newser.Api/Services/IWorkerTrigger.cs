namespace Newser.Api.Services;

public interface IWorkerTrigger
{
    bool ShouldRun { get; set; }
}

public class WorkerTrigger : IWorkerTrigger
{
    public bool ShouldRun { get; set; }
}