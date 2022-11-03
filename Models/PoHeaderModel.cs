using System;

namespace WsiApi.Models
{
    public class PoHeaderModel
    {
        public string PoNumber { get; set; }

        public char Action { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
