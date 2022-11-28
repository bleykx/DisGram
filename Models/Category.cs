using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisGram.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ButtonLabel { get; set; }
        public string? ButtonReply { get; set; }
        public bool Enabled { get; set; }
    }
}
