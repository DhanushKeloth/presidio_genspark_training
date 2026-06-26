using System;
using System.Collections.Generic;

namespace LibraryManagementSystem.Models;

public partial class BookCopy
{
    public int BookId { get; set; }

    public string Title { get; set; } = null!;

    public string Author { get; set; } = null!;

    public int CategoryId { get; set; }
    
    public int AvailableCopies{get;set;}

}
