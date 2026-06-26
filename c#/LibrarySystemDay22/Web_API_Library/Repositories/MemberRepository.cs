using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Interfaces;

namespace LibraryManagementSystem.Repositories
{
    // K becomes int (for MemberId) and T becomes Member
    public class MemberRepository : IMemberRepository<int, Member>
    {
        private readonly LibraryDbContext _context;

        public MemberRepository(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<Member?> AddMember(Member item)
        {
            await _context.Members.AddAsync(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public Member? GetById(int key)
        {
            return _context.Members.Find(key);
        }

        public IEnumerable<Member> GetAll()
        {
            return _context.Members.ToList();
        }

        public Member? GetByContact(string contact)
        {
            // Checks both Email and PhoneNumber
            return _context.Members.FirstOrDefault(m => 
                m.Email == contact || m.PhoneNumber == contact);
        }

        public Member? RemoveMember(int key)
        {
            var member = _context.Members.Find(key);
            if (member != null)
            {
                _context.Members.Remove(member);
                _context.SaveChanges();
            }
            return member;
        }
    }
}