using System.Collections.Generic;
using System.Linq;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.Repositories
{
    public class MemberRepository : IMemberRepository<int, Member>
    {
        private readonly LibraryDbContext _context;

        public MemberRepository(LibraryDbContext context)
        {
            _context = context;
        }

        public Member? AddMember(Member item)
        {
            _context.Members.Add(item);
            _context.SaveChanges();
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
            return _context.Members.FirstOrDefault(m => m.Email == contact || m.Phone == contact);
        }

        public Member? UpdateMembership(Member item)
        {
            _context.Members.Update(item);
            _context.SaveChanges();
            return item;
        }

        public Member? DeactivateMember(int key)
        {
            var member = _context.Members.Find(key);
            if (member == null) return null;

            member.IsActive = false;
            _context.SaveChanges();
            return member;
        }
    }
}