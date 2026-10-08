using MongoDB.Driver;
using TAMS.Models;

namespace TAMS.Data
{
    //Provides a connection to the MongoDb database for TAMS
    public class MongoDBContext
    {
        //Stores the MongoDB database instance
        private readonly IMongoDatabase _database;

        //Constructor that initializes the MongoDBContext with the provided configuration
        public MongoDBContext(IConfiguration configuration)
        {
            //Gets the MongoDB connection string from the appsetting.json
            var connectionString = configuration["MongoDBSettings:ConnectionString"];

            //Gets the MongoDB database name from the appsetting.json
            var databaseName = configuration["MongoDBSettings:DatabaseName"];

            //Creates a MongoDB client using the connection string
            var client = new MongoClient(connectionString);

            //Connects to the specfied MongoDB database
            _database = client.GetDatabase(databaseName);

            // Delete recovery codes marked as used whenever the application starts.
            DeleteUsedRecoveryCodes();
        }

        //Provides access to the MongoDB database instance
        public IMongoDatabase Database => _database;
        public IMongoCollection<Login> Login => _database.GetCollection<Login>("Login");
        public IMongoCollection<Student> Student => _database.GetCollection<Student>("Student");
        public IMongoCollection<Tutor> Tutor => _database.GetCollection<Tutor>("Tutor");
        public IMongoCollection<Admin> Admin => _database.GetCollection<Admin>("Admin");
        public IMongoCollection<Subject> Subject => _database.GetCollection<Subject>("Subject");
        public IMongoCollection<Course> Course => _database.GetCollection<Course>("Course");
        public IMongoCollection<PasswordRecovery> PasswordRecoveries => _database.GetCollection<PasswordRecovery>("PasswordRecoveries");
        public IMongoCollection<Appointment> Appointments => _database.GetCollection<Appointment>("Appointments");

        private void DeleteUsedRecoveryCodes()
        {
            var result = PasswordRecoveries.DeleteMany(
                recovery => recovery.IsUsed == true
            );

            Console.WriteLine(
                $"Deleted {result.DeletedCount} used recovery code(s) at startup."
            );
        }
    }
}