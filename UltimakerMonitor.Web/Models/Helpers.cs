namespace UltimakerMonitor.Web.Models
{
    public static class Helpers
    {
        public static string GetJobStateLabel(JobState state) =>
            state switch
            {
                JobState.Preparing => "Preparing",
                JobState.Printing => "Printing",
                JobState.Pausing => "Pausing",
                JobState.Paused => "Paused",
                JobState.Resuming => "Resuming",
                JobState.PostPrint => "Finishing",
                JobState.WaitCleanup => "Awaiting Cleanup",
                JobState.NoJob => "No Job",
                _ => "Unknown"
            };

        public static string GetStatusColor(PrinterStatus status) =>
            status switch
            {
                PrinterStatus.Idle => "success",
                PrinterStatus.Printing => "primary",
                PrinterStatus.Paused => "warning",
                PrinterStatus.Error => "danger",
                PrinterStatus.Offline => "secondary",
                PrinterStatus.Maintenance => "info",
                _ => "secondary"
            };

        public static string GetJobStateColor(JobState status) =>
            status switch
            {
                JobState.Preparing => "info",
                JobState.Printing => "primary",
                JobState.Pausing => "success",
                JobState.Paused => "warning",
                JobState.Resuming => "success",
                JobState.PostPrint => "success",
                JobState.WaitCleanup => "info",
                _ => "secondary"
            };
    }
}
