using MongoDB.Bson.Serialization.Attributes;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Appointment
    {
        [Key]
        public int AppointmentId { get; set; }

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }         // End time

        public int ScheduleId { get; set; } // Exact schedule being booked

        //[Required]
        [ForeignKey("Student")]
        public int? StudentId { get; set; }
        public virtual Student Student { get; set; }

        [Required]
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public virtual Tutor Tutor  { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled

        [BsonElement("Reminder1Sent")]
        public bool Reminder1Sent { get; set; } = false;

        [BsonElement("Reminder2Sent")]
        public bool Reminder2Sent { get; set; } = false;

        [BsonElement("NeedsAdminFollowUp")]
        public bool NeedsAdminFollowUp { get; set; } = false;

        [BsonElement("AdminNotificationSent")]
        public bool AdminNotificationSent { get; set; } = false;

        [BsonElement("TutorReminder1Acknowledged")]
        public bool TutorReminder1Acknowledged { get; set; } = false;

        [BsonElement("TutorReminder2Acknowledged")]
        public bool TutorReminder2Acknowledged { get; set; } = false;

        [BsonElement("TutorReminder3Acknowledged")]
        public bool TutorReminder3Acknowledged { get; set; } = false;

        [BsonElement("Reminder24HoursBeforeSent")]
        public bool Reminder24HoursBeforeSent { get; set; } = false;

        [BsonElement("Reminder1HourBeforeSent")]
        public bool Reminder1HourBeforeSent { get; set; } = false;

        [BsonElement("StudentReminder24HoursAcknowledged")]
        public bool StudentReminder24HoursAcknowledged { get; set; } = false;

        [BsonElement("StudentReminder1HourAcknowledged")]
        public bool StudentReminder1HourAcknowledged { get; set; } = false;

        [BsonElement("TutorAppointmentReminder24HoursAcknowledged")]
        public bool TutorAppointmentReminder24HoursAcknowledged { get; set; } = false;

        [BsonElement("TutorAppointmentReminder1HourAcknowledged")]
        public bool TutorAppointmentReminder1HourAcknowledged { get; set; } = false;

        [BsonElement("CancelledByAdminId")]
        public int? CancelledByAdminId { get; set; }

        [BsonElement("CancelledByAdminName")]
        public string? CancelledByAdminName { get; set; }

        [BsonElement("TutorNotes")]
        [StringLength(2000)]
        public string TutorNotes { get; set; } = string.Empty;

        public bool IsGroup { get; set; } = false;

    }
}
