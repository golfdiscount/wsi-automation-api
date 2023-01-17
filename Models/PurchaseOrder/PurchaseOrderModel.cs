using System;
using System.Collections.Generic;

namespace Pgd.Wsi.Models.PurchaseOrder
{
    public class PurchaseOrderModel
    {
        public string PoNumber { get; set; }

        public char Action { get; set; }

        public List<PurchaseOrderDetailModel> LineItems { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
