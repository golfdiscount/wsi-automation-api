using System;

namespace WsiApi.Models
{
    public class PoDetailModel
    {
        public string PoNumber { get; set; }

        public int LineNumber { get; set; }

        public char Action { get; set; }

        public string Sku { get; set; }

        public int Units { get; set; }

        public DateTime CreatedAt { get; set; } 

        public DateTime UpdatedAt { get; set; }
    }
}
