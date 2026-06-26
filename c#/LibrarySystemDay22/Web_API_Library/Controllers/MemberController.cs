using Microsoft.AspNetCore.Mvc;
using System;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Exceptions; 
namespace LibraryManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MemberController : ControllerBase
    {
        private readonly IMemberService _memberService;

        public MemberController(IMemberService memberService)
        {
            _memberService = memberService;
        }

        [HttpPost]
        public IActionResult AddMember([FromBody] Member member)
        {
            try
            {
                var newMember = _memberService.AddMember(member);
                if (newMember == null)
                {
                    return BadRequest();
                }
                return Ok(new {message = "Member added successfully"});
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetAllMembers()
        {
            var members = _memberService.GetAll();
            return Ok(members);
        }

        // GET: api/Member/5
        [HttpGet("{id}")]
        public IActionResult GetMemberById(int id)
        {
            var member = _memberService.GetById(id);
            if (member == null)
            {
                return NotFound(new { Message = $"Member with ID {id} not found." });
            }
            return Ok(member);
        }

        [HttpGet("contact/{contact}")]
        public IActionResult GetMemberByContact(string contact)
        {
            var member = _memberService.GetByContact(contact);
            if (member == null)
            {
                return NotFound(new { Message = $"No member found with contact {contact}." });
            }
            return Ok(member);
        }

        // DELETE: api/Member/5
        [HttpDelete("{id}")]
        public IActionResult RemoveMember(int id)
        {
            try
            {
                var removedMember = _memberService.RemoveMember(id);
                return Ok(new { Message = "Member successfully removed.", Member = removedMember });
            }
            catch (RecordNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An unexpected error occurred.", Details = ex.Message });
            }
        }
    }
}