using System;

namespace Pgd.Wsi.Models
{
    public class ShippingMethodModel
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime Updated_at { get; set; }
    }
}
