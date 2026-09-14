namespace ColdNet.Core.Domain;

/// <summary>
/// Mirrors the job lifecycle states shown in d.cold admin's "Jobs" area
/// (Ready, Working, Error) plus an explicit Finished state for jobs that
/// have passed the last module of their process chain.
/// </summary>
public enum JobStatus
{
    Ready = 0,
    Working = 1,
    Error = 2,
    Finished = 3,
}
