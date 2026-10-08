using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TAMS.Models
{
    public class TutorReview
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string TutorId { get; set; }

        public int Rating { get; set; }

        public string Comment { get; set; }
    }
}