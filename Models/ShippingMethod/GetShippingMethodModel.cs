using System;

namespace WsiApi.Models.ShippingMethod
{
    public class GetShippingMethodModel : ShippingMethodModel
    {
        public DateTime Created_at { get; set; }
        public DateTime Updated_at { get; set; }
    }
}
