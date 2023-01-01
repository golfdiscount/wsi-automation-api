using System;

namespace WsiApi.Models.PickTicket
{
    public class PickTicketDetailModel
    {
        public string PickTicketNumber { get; set; }
        public int LineNumber { get; set; }
        public char Action { get; set; }
        public string Sku { get; set; }
        public int Units { get; set; }
        public int UnitsToShip { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
