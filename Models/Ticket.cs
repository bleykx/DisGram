using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisGram.Models
{
    public class Ticket
    {
        public int Id { get; set; }
        public Customer? Customer { get; set; }
        public StaffMember? StaffMember { get; set; }
        public ulong DiscordChannelId { get; set; }
        public Category? Category { get; set; }
        public TicketState? State { get; set; }
        public DateTime? CreatedAt { get; set; }

        public enum TicketState : int
        {
            Unclaimed = 0,
            Claimed = 1,
            Closed = 2,
        }
    }
}
