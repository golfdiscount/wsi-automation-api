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
        public ShippingMethod Shipping_Method { get; set; }
        public List<LineItem> LineItems { get; set; }
        public DateOnly OrderDate { get; set; }
        public int Channel { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime Updated_at { get; set; }
    }
}
