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
        public DateTime Created_at { get; set; }
        public DateTime Updated_at { get; set; }
    }
}
