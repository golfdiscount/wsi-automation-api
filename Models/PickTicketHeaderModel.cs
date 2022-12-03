using System;

namespace WsiApi.Models
{
    public class PickTicketHeaderModel
    {
        public string PickTicketNumber { get; set; }
        public string OrderNumber { get; set; }
        public char Action { get; set; }
        public int Store { get; set; }
        public int Customer { get; set; }
        public int Recipient { get; set; }
        public string ShippingMethod { get; set; }
        public DateTime OrderDate { get; set; }
        public int Channel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
