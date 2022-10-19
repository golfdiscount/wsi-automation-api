using System;
using System.Collections.Generic;
using WsiApi.Models.Address;
using WsiApi.Models.Detail;
using WsiApi.Models.ShippingMethod;

namespace WsiApi.Models.Order
{
    public class GetOrderModel : OrderModel
    {
        public string PickticketNumber { get; set; }
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
