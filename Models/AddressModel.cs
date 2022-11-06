using System.ComponentModel.DataAnnotations;

namespace WsiApi.Models
{
    public class AddressModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Street { get; set; }

        [Required]
        public string City { get; set; }

        [Required]
        public string State { get; set; }

        [Required]
        public string Country { get; set; }

        [Required]
        public string Zip { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            var address = (AddressModel)obj;

            return Name.Equals(address.Name)
                && Street.Equals(address.Street)
                && City.Equals(address.City)
                && State.Equals(address.State)
                && Country.Equals(address.Country)
                && Zip.Equals(address.Zip);
        }
    }
}
