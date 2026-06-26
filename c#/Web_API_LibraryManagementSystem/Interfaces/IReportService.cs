namespace LibraryManagementSystem.Interfaces
{
    public interface IReportService
    {
        void GenerateCurrentlyBorrowedReport();
        void GenerateOverdueReport();
        void GeneratePendingFinesReport();
        void GenerateMostBorrowedReport();
        void GenerateCategoryAvailabilityReport();
        void GenerateMemberHistoryReport(int memberId);
    }
}