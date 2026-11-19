using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Workflow;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>
    /// The allowed lab request transitions. A request can be cancelled until its result is issued (UC-L04);
    /// Cancelled and ResultIssued are terminal.
    /// </summary>
    public static class LabRequestWorkflow
    {
        public static readonly StateMachine<LabRequestStatus> Machine = new StateMachine<LabRequestStatus>()
            .Allow(LabRequestStatus.Requested, LabRequestStatus.Received, LabRequestStatus.ReturnedToDoctor, LabRequestStatus.Cancelled)
            .Allow(LabRequestStatus.ReturnedToDoctor, LabRequestStatus.Requested, LabRequestStatus.Cancelled)
            .Allow(LabRequestStatus.Received, LabRequestStatus.InProgress, LabRequestStatus.Cancelled)
            .Allow(LabRequestStatus.InProgress, LabRequestStatus.ResultIssued, LabRequestStatus.Cancelled);
    }
}
