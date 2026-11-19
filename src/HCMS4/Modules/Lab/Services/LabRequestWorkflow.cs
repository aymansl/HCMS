using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Workflow;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>
    /// The allowed lab request transitions. Cancellation (L4) and results (Increment 3) add their
    /// transitions when they are built.
    /// </summary>
    public static class LabRequestWorkflow
    {
        public static readonly StateMachine<LabRequestStatus> Machine = new StateMachine<LabRequestStatus>()
            .Allow(LabRequestStatus.Requested, LabRequestStatus.Received, LabRequestStatus.ReturnedToDoctor)
            .Allow(LabRequestStatus.ReturnedToDoctor, LabRequestStatus.Requested)
            .Allow(LabRequestStatus.Received, LabRequestStatus.InProgress)
            .Allow(LabRequestStatus.InProgress, LabRequestStatus.ResultIssued);
    }
}
