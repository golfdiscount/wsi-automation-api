using System;

namespace WsiApi.Models.Detail
{
    public class DetailModel
    {
        public string PickticketNumber { get; set; }
        public int LineNumber { get; set; }
        public char Action { get; set; }
        public string Sku { get; set; }
        public int Units { get; set; }
        public int UnitsToShip { get; set; }
    }
}
