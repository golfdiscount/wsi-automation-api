using System;

namespace Pgd.Wsi.Models
{
    public class ShippingConfirmationModel
    {
        public string PickTicketNumber { get; set; }

        public int LineNumber { get; set; }

        public DateTime ShipDate { get; set; }

        public string TrackingNumber { get; set; }

        public string ShippingMethod { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
