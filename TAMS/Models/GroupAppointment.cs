
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class GroupAppointment
    {
        [Key]
        public int GroupId { get; set; }

        // Linked appointment created after admin approval
        public int? AppointmentId { get; set; }

        // Tutor submitting the request
        [Required]
        public int TutorId { get; set; }

        // Requested appointment date
        [Required]
        public DateTime AppointmentDate { get; set; }

        // Requested start and end times
        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        // Maximum number of students allowed
        [Range(2, 100)]
        public int Capacity { get; set; }

        // Students enrolled in the group
        public List<int> StudentIds { get; set; } = new();

        // Pending, Open, Full, Rejected,
        // Cancelled, Completed
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public bool Reminder24HoursBeforeSent { get; set; } = false;

        public bool Reminder1HourBeforeSent { get; set; } = false;

        public List<int> CompletedStudentIds { get; set; } = new();

        public List<int> AbsentStudentIds { get; set; } = new();

        [BsonElement("TutorNotes")]
        [StringLength(2000)]
        public string TutorNotes { get; set; } = string.Empty;


    }
}
