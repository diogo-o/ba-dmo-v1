namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Raised by the persistence layer when a Job On create/duplicate hits the
/// partial unique index <c>uq_job_on_identity</c> (N25): another NON-canceled
/// Job On already exists for the same (production_code, machine_code). The
/// service maps this to a structured domain conflict (JOB_ON_IDENTITY_DUPLICATE)
/// instead of a raw 23505 (audit JA-03 / PC-12).
/// </summary>
public sealed class JobOnIdentityDuplicateException : Exception
{
    public JobOnIdentityDuplicateException(string message)
        : base(message)
    {
    }
}