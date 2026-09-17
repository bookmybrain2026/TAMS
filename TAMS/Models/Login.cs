using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Login
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public UserRole Role { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; }

        [Required]
        [StringLength(100)]
        public string Password { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Required]
        public DateTime LastLogin { get; set; } = DateTime.Now;

        public bool isActive { get; set; } = true;
    }
    public enum UserRole
    {
        Student,
        Tutor,
        Administrator
    }
}