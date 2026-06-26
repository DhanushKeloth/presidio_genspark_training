using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Repositories;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework.Constraints;

namespace LibrarySystem.Tests;

public class Tests
{
    IMemberRepository<int, Member> memberRepository;
    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>().UseInMemoryDatabase("librarydb").Options;
        LibraryDbContext libraryDbContext = new LibraryDbContext(options);
        memberRepository = new MemberRepository(libraryDbContext);

    }

    [Test]
    public async Task AddMemberPassTest()
    {
        Member member = new Member
        {
            FullName = "ramu keloth",
            Email = "ramu@mail.com",
            PhoneNumber = "9876543210",
            MembershipDate = new DateTime(2000, 1, 1)
        };
        var result = await memberRepository.AddMember(member);
        Assert.That(result.MemberId, Is.EqualTo(1));
    }
    [Test]
    public async Task GetById_WhenMemberDoesNotExist_ReturnsNull()
    {
        // Arrange
        // (We don't seed anything, so the database is empty)

        // Act
        var result = memberRepository.GetById(999);

        // Assert
        Assert.That(result, Is.Null);
    }
    [Test]
    public async Task GetMemberPassTest()
    {
        Member member = new Member
        {
            MemberId = 1,
            FullName = "ramu keloth",
            Email = "ramu@mail.com",
            PhoneNumber = "9876543210",
            MembershipDate = new DateTime(2000, 1, 1)
        };
        // var member1 = memberRepository.AddMember(member);
        var result = memberRepository.GetById(1);
        Assert.That(result.FullName, Is.EqualTo(member.FullName));
    }
    [Test]
    public void GetById_WhenMemberExists_ReturnsMemberFromDatabase()
    {
     
        var seededMember = new Member { MemberId = 5, FullName = "Bruce Wayne" };
        memberRepository.AddMember(seededMember);
      
        var result = memberRepository.GetById(5);

   
        Assert.That(result, Is.Not.Null);
        Assert.That(result.MemberId, Is.EqualTo(5));
        Assert.That(result.FullName, Is.EqualTo("Bruce Wayne"));
    }
    [Test]
    public async Task GetByContactReturnsMemberTest()
    {
        Member member = new Member
        {
            
            FullName = "somu",
            Email = "somu@mail.com",
            PhoneNumber = "9000987654",
            MembershipDate = new DateTime(2001, 1, 1)
        
        };
        await memberRepository.AddMember(member);
        var result = memberRepository.GetByContact("somu@mail.com");
        Assert.That(result.FullName,Is.EqualTo(member.FullName));
    }


}
