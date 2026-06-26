using System;
using System.Collections.Generic;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.BusinessLayer; // For UserValidator, EmailValidator, etc.
using LibraryManagementSystem.Exceptions;    // For ValidException

namespace LibraryManagementSystem.Services
{
    public class MemberService : IMemberService
    {
        private readonly IMemberRepository<int, Member> _memberRepo;

        public MemberService(IMemberRepository<int, Member> memberRepo)
        {
            _memberRepo = memberRepo;
        }

        public async Task<Member?> AddMember(Member item)
        {
            // 1. Text Validations
            UserValidator.ValidateName(item.FullName);
            EmailValidator.Validate(item.Email);
            PhoneValidator.Validate(item.PhoneNumber);

            // 2. Business Logic: Check for duplicates
            var existingByEmail = _memberRepo.GetByContact(item.Email);
            var existingByPhone = _memberRepo.GetByContact(item.PhoneNumber);

            if (existingByEmail != null || existingByPhone != null)
            {
                throw new ValidException("A member with this email or phone number already exists.");
            }

            if (item.MembershipDate == default)
            {
                item.MembershipDate = DateTime.UtcNow;
            }

            await _memberRepo.AddMember(item);
            return item;
        }

        public Member? GetById(int key)
        {
            return _memberRepo.GetById(key);
        }

        public IEnumerable<Member> GetAll()
        {
            return _memberRepo.GetAll();
        }

        public Member? GetByContact(string contact)
        {
            // Guard against null/empty queries
            if (string.IsNullOrWhiteSpace(contact))
            {
                return null;
            }

            return _memberRepo.GetByContact(contact.Trim());
        }

        public Member? RemoveMember(int key)
        {
            // Check if member exists before attempting removal
            var existingMember = _memberRepo.GetById(key);
            if (existingMember == null)
            {
                throw new RecordNotFoundException($"Member ID {key} does not exist.");
            }

            return _memberRepo.RemoveMember(key);
        }
    }
}