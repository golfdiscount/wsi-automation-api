using System;
using System.Collections.Generic;

namespace Pgd.Wsi.Models.ShippingConfirmation
{
    public class ShippingConfirmationModel
    {
        public string PickTicketNumber { get; set; }

        public List<ShippingConfirmationDetailModel> LineItems { get; set; }

        public DateTime ShipDate { get; set; }

        public string TrackingNumber { get; set; }

        public string ShippingMethod { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
