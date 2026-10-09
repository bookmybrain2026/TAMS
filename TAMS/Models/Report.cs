using MongoDB.Bson.Serialization.Attributes;
using System.Numerics;
using TAMS.Models;
namespace TAMS.Models
{
    [BsonIgnoreExtraElements]
    public class Report
    {
        public int ReportId { get; set; }

        public int? StudentId { get; set; }
        public Student? Student { get; set; }

        public int? TutorId { get; set; }
        public Tutor? Tutor { get; set; }

        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public string ReportType { get; set; } = "";

        public DateTime DateGenerated { get; set; }

        public string? GeneratedByRole { get; set; }

        public string? FilePath { get; set; }

        public string? Summary { get; set; }
    }

}
