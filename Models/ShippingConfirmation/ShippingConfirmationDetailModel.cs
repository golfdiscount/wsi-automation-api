using System;

namespace Pgd.Wsi.Models.ShippingConfirmation
{
    public class ShippingConfirmationDetailModel
    {
        public int LineNumber { get; set; }
        public string Sku { get; set; }
        public int Units { get; set; }
    }
}
