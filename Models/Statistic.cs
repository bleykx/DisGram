using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisGram.Models
{
    public class Statistic
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Value { get; set; }
        public Category? Category { get; set; }
        public StaffMember? StaffMember { get; set; }
        public Customer? Customer { get; set; }
    }
}
