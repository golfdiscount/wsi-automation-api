using Pgd.Wsi.Models.ShippingConfirmation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Pgd.Wsi.Models.PickTicket
{
    public class PickTicketModel
    {
        public string PickTicketNumber { get; set; }

        [Required]
        public string OrderNumber { get; set; }

        [Required]
        public char Action { get; set; }

        [Required]
        public int Store { get; set; }

        [Required]
        public AddressModel Customer { get; set; }

        [Required]
        public AddressModel Recipient { get; set; }

        [Required]
        public string ShippingMethod { get; set; }

        [Required]
        public List<PickTicketDetailModel> LineItems { get; set; }

        [Required]
        public DateTime OrderDate { get; set; }

        public int Channel { get; set; }

        public ShippingConfirmationModel ShippingConfirmation { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Creates a deep clone of the current <c>PickTicketModel</c> object with a blank list of <c>LineItems</c>
        /// </summary>
        /// <returns>Cloned instance of current <c>PickTicketModel</c></returns>
        public PickTicketModel DeepClone()
        {
            PickTicketModel order = (PickTicketModel)MemberwiseClone();
            order.LineItems = new();

            return order;
        }
    }
}
