using LibraryManagementSystem.Models;
using LibraryManagementSystem.Enums;

namespace LibraryManagementSystem.Interfaces
{
    public interface IMemberService
    {
        //service layer that used to check the valid checkpoints before adding into the db
        bool RegisterMember(string name, string email, string phone, MembershipType type);
        IEnumerable<Member> ViewAllMembers();
        Member? SearchMember(string contact);
        bool UpdateMembershipStatus(int memberId, bool isActive);
    }
}