using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace TAMS.Models
{
    public class Review
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public int? AppointmentId { get; set; }

        public int StudentId { get; set; }

        public int TutorId { get; set; }

        public string StudentName { get; set; } = string.Empty;
        public string TutorName { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Rating { get; set; }

        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime AppointmentDate { get; set; }

        public int? GroupId { get; set; }

        public bool IsGroupReview { get; set; } = false;

    }
}