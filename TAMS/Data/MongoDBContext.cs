using MongoDB.Driver;

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
        }

        //Provides access to the MongoDB database instance
        public IMongoDatabase Database => _database;
    }
}