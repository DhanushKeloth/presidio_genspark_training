using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IFineRepository<K,T> where T:class
    {
        T? AddMember(T item);
        T? GetMemberById(int finePaymentId); 
        IEnumerable<FinePayment> GetHistoryByMemberId(int memberId);
        IEnumerable<FinePayment> GetUnpaidFines();
        T? UpdateMember(T item);
        
        decimal GetTotalUnpaidFine(int memberId);
    }
}