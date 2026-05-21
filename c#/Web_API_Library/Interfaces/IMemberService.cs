using System.Collections.Generic;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Interfaces
{
    public interface IMemberService
    {
        Member? AddMember(Member item);
        Member? GetById(int key);
        IEnumerable<Member> GetAll();
        Member? GetByContact(string contact);
        Member? RemoveMember(int key);
    }
}