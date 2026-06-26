using LibraryManagementSystem.Interfaces;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using System;

namespace LibraryManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly IBookService _bookService;

        //declare the logger with Ilogger 
        private readonly ILogger<BooksController> _logger;

        public BooksController(IBookService bookService,ILogger<BooksController> logger)
        {
            _bookService = bookService;
            _logger=logger;
        }

        [HttpGet]
        public IActionResult GetBooks()
        {
            try
            {
                _logger.LogInformation("Attempting to get the books");
                var books = _bookService.GetBooks();
                _logger.LogInformation($"retrieved all the books from the database");
                return Ok(books);

            }
            catch (Exception ex)
            {
                _logger.LogWarning("failed to get the  books from the database");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("id")]
        public IActionResult GetBookById(int id)
        {
            try
            {
                var book = _bookService.GetBookById(id);
                if (book == null)
                {
                    return NotFound();
                }
                return Ok(book);

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public IActionResult AddBook([FromBody] Book book)
        {
            try
            {
                _logger.LogInformation($"attempting to add new book with name {book.Title}");
                var newBook = _bookService.AddBook(book);
                _logger.LogInformation($"added {book.Title} successfully in the database ");
                return Ok(new { message = "Book Added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to add the book with book id {book.BookId} and title {book.Title}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("search")]
        public IActionResult SearchBooks([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Search query parameter cannot be empty." });
            }
            try
            {
                var books = _bookService.SearchBooks(query);

                return Ok(books);

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }

}