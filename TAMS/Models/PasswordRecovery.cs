using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TAMS.Models
{
    public class PasswordRecovery
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public int UserId { get; set; }
        public string RecoveryCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }
}