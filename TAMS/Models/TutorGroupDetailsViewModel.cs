
using TAMS.Models;

namespace TAMS.Models.ViewModels
{
    public class TutorGroupDetailsViewModel
    {
        public GroupAppointment Group { get; set; } = new();

        public List<Student> Students { get; set; } = new();

        public int CompletedCount { get; set; }

        public int AbsentCount { get; set; }

        public int PendingCount { get; set; }
    }
}
