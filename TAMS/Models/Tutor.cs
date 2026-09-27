using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MongoDB.Bson.Serialization.Attributes;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Tutor
    {
        [Key]
        [ForeignKey("Login")]
        public int TutorId { get; set; }

        [Required]
        [MaxLength(50)]
        public string FName { get; set; }

        [Required]
        [MaxLength(50)]
        public string LName { get; set; }

        [Required]
        [MaxLength(100)]
        public string Subject { get; set; }

        [Required]
        [MaxLength(13)]
        public string PhoneNumber { get; set; }

        [MaxLength(100)]
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        public string Email { get; set; }

        // Courses this tutor is able to tutor
        public List<string> TutoringCourseIds { get; set; } = new List<string>();

        [BindNever]
        public virtual Login? Login { get; set; }

    }
}