using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MongoDB.Bson.Serialization.Attributes;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Admin
    {
        [Key]
        [ForeignKey("Login")]
        public int AdminId { get; set; }

        [Required]
        [MaxLength(50)]
        public string FName { get; set; }

        [Required]
        [MaxLength(50)]
        public string LName { get; set; }

        [Required]
        [MaxLength(13)]
        public string PhoneNumber { get; set; }

        [MaxLength(100)]
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        public string Email { get; set; }

        public virtual Login Login { get; set; }
    }
}