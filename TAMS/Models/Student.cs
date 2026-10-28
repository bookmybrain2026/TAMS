using AMS.Models;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Student
    {
        [Key]
        [ForeignKey("Login")]
        public int StudentId { get; set; }

        [Required]
        [MaxLength(50)]
        public string FName { get; set; }

        [Required]
        [MaxLength(50)]
        public string LName { get; set; }

        [Required]
        public DateTime DOB { get; set; }

        [Required]
        [MaxLength(13)]
        public string PhoneNumber { get; set; }

        [MaxLength(100)]
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        public string Email { get; set; }

        public virtual Login Login { get; set; }

        public virtual List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public List<Review> AwaitingReviews { get; set; } = new List<Review>();

        public bool DisableEmailNotifications { get; set; } = false;

    }
}