namespace LibraryManagementSystem.Interfaces
{
    public interface IBorrowingService
    {
        bool BorrowBook(int memberId, int bookId);
        bool ReturnBook(int bookCopyId);
    }
}