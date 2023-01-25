using System.Collections.Generic;

namespace Pgd.Wsi.Models.PickTicket
{
    public class PickTicketCollection
    {
        public List<PickTicketModel> PickTickets { get; set; }

        public int PageSize { get; set; }

        public int Page { get; set; }
    }
}
