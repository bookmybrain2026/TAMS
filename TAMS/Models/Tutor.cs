using AMS.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public virtual List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

        [BindNever]
        public virtual Login? Login { get; set; }

        [NotMapped]
        //public List<TutorReview> Reviews { get; set; } = new List<TutorReview>();
        public List<Review> Reviews { get; set; } = new List<Review>();
        public double AverageRating { get; set; }

        public int ReviewCount { get; set; }

        public bool DisableEmailNotifications { get; set; } = false;

    }
}