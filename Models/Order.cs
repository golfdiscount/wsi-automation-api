using System;
using System.Collections.Generic;

namespace wsi_triggers.Models
{
    public class Order
    {
        public string PickticketNumber { get; set; }
        public string OrderNumber { get; set; }
        public char Action { get; set; }
        public Store Store { get; set; }
        public Address Customer { get; set; }
        public Address Recipient { get; set; }
        public ShippingMethod ShippingMethod { get; set; }
        public List<Detail> LineItems { get; set; }
        public DateTime OrderDate { get; set; }
        public int Channel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
