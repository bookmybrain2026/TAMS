using MongoDB.Bson.Serialization.Attributes;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Schedule
    {
        [Key]
        public int ScheduleId { get; set; }

        [Required]
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }

        public virtual Tutor Tutor { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public bool IsGroup { get; set; } = false;

        public int? GroupId { get; set; }

    }
}