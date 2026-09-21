using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Course
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? CourseId { get; set; }

        [Required]
        public string SubjectCode { get; set; }

        [Required]
        public string CourseCode { get; set; }

        [Required]
        public string CourseName { get; set; }
    }
}