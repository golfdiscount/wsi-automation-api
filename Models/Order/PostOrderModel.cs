using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WsiApi.Models.Address;

namespace WsiApi.Models.Order
{
    public class PostOrderModel : OrderModel
    {
        [Required]
        [Range(1, 10)]
        public int Store { get; set; }

        [Required]
        public AddressModel Customer { get; set; }

        [Required]
        public AddressModel Recipient { get; set; }

        [Required]
        public string ShippingMethod { get; set; }

        [Required]
        public DateTime OrderDate { get; set; }

        [Required]
        [MinLength(1)]
        public List<PostLineItem> Products { get; set; }
    }

    public class PostLineItem
    {
        [Required]
        public string Sku { get; set; }

        [Required]
        public int Quantity { get; set; }
    }
}
