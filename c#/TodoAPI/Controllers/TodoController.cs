using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoAPI.Models;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoController : ControllerBase
    {
        private readonly TodoContext _context;

        // The database context is injected here
        public TodoController(TodoContext context)
        {
            _context = context;
        }

        // GET: api/todo
        [HttpGet]
        public ActionResult<IEnumerable<TodoItem>> GetTodos()
        {
            return  _context.Todos.ToList();
        }
        [HttpGet("{id}")]
        public ActionResult<TodoItem> GetTodo(int id)
        {
            var todo =  _context.Todos.Find(id);
            if (todo == null)
            {
                
            return NotFound();
            } 
            return todo;
        }
        //put
        [HttpPut]
        public  IActionResult PutTodo(int id,TodoItem todo)
        {
            if (id != todo.Id)
            {
                return BadRequest();
            }
            _context.Entry(todo).State=EntityState.Modified;
            try
            {
                 _context.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }
            return NoContent();
        }

        //patch
        [HttpPatch("{id}/status")]
        public  IActionResult PatchTodoStatus(int id, [FromBody] bool isComplete)
        {
            var todo =  _context.Todos.Find(id);
            if (todo == null)
            {
                return NotFound();
            }

            todo.IsCompleted = isComplete;
             _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public  IActionResult DeleteTodo(int id)
        {
            var todo =  _context.Todos.Find(id);
            if (todo == null)
            {
                return NotFound();
            }

            _context.Todos.Remove(todo);
             _context.SaveChangesAsync();

            return NoContent();
        }
        // POST: api/todo
        [HttpPost]
        public  ActionResult<TodoItem> PostTodo(TodoItem todo)
        {
            _context.Todos.Add(todo);
            _context.SaveChangesAsync();
            
            return CreatedAtAction(nameof(GetTodos), new { id = todo.Id }, todo);
        }

    }
}