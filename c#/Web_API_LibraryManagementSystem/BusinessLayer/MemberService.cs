using System;
using System.Collections.Generic;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Enums;
using LibraryManagementSystem.BusinessLayer;
using LibraryManagementSystem.Exceptions;

namespace LibraryManagementSystem.BusinessLayer
{
    public class MemberService : IMemberService
    {
        private readonly IMemberRepository<int, Member> _memberRepo;

        public MemberService(IMemberRepository<int, Member> memberRepo)
        {
            _memberRepo = memberRepo;
        }

        public bool RegisterMember(string name, string email, string phone, MembershipType type)
        {
            UserValidator.ValidateName(name);
            EmailValidator.Validate(email);
            PhoneValidator.Validate(phone);

            var existingMember = _memberRepo.GetByContact(email) ?? _memberRepo.GetByContact(phone);
            if (existingMember != null)
            {
                throw new ValidException("A member with this email or phone number already exists.");
            }

            var newMember = new Member
            {
                Name = name,
                Email = email,
                Phone = phone,
                Membership = type,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var savedMember = _memberRepo.AddMember(newMember);
            return savedMember != null;
        }

        public IEnumerable<Member> ViewAllMembers()
        {
            return _memberRepo.GetAll();
        }

        public Member? SearchMember(string contact)
        {
            return _memberRepo.GetByContact(contact);
        }

        public bool UpdateMembershipStatus(int memberId, bool isActive)
        {
            if (!isActive)
            {
                var deactivated = _memberRepo.DeactivateMember(memberId);
                return deactivated != null;
            }

            var member = _memberRepo.GetById(memberId);
            if (member == null)
            {
                return false;
            }

            member.IsActive = true;
            var updated = _memberRepo.UpdateMembership(member);
            return updated != null;
        }
    }
}