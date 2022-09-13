using System;
using System.ComponentModel.DataAnnotations;
using wsi_triggers.Models.Address;

namespace wsi_triggers.Models.Order
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
    }
}
