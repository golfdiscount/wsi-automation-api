using System;
using System.Collections.Generic;
using wsi_triggers.Models.Address;
using wsi_triggers.Models.Detail;
using wsi_triggers.Models.ShippingMethod;

namespace wsi_triggers.Models.Order
{
    public class GetOrderModel : OrderModel
    {
        public char Action { get; set; }
        public StoreModel Store { get; set; }
        public AddressModel Customer { get; set; }
        public AddressModel Recipient { get; set; }
        public ShippingMethodModel ShippingMethod { get; set; }
        public List<GetDetailModel> LineItems { get; set; }
        public DateTime OrderDate { get; set; }
        public int Channel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
