
using System;
using System.Threading.Tasks;
// allows controllers to work without knowing that a queue exists
namespace TAMS.Services
{
    public class QueuedEmailService : IEmailService
    {
        private readonly EmailQueue _queue;

        public QueuedEmailService(EmailQueue queue)
        {
            _queue = queue;
        }
        //_queue is the EmailQueue instance injected into QueuedEmailService. EnqueueAsync adds a job to the queue
        //sender => sender. .....) defines the job to be executed by the email sender. takes an IEmailSender instance (sender) and calls the method on it 
        //.AsTask() converts the returned ValueTask into a Task, allowing this method to satisfy the interface's return type

        public Task SendPasswordRecoveryCodeAsync(string userEmail, string userName, string recoveryCode)
        {            
            return _queue.EnqueueAsync(sender => 
                sender.SendPasswordRecoveryCodeAsync(userEmail, userName, recoveryCode)).AsTask();      
        }

        public Task SendStudentAppointmentConfirmationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendStudentAppointmentConfirmationAsync(studentEmail, studentName, tutorName, appointmentDate)).AsTask();
        }

        public Task SendTutorAppointmentNotificationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorAppointmentNotificationAsync(tutorEmail, tutorName, studentName, appointmentDate)).AsTask();
        }

        public Task SendStudentAppointmentCancellationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendStudentAppointmentCancellationAsync(studentEmail, studentName, tutorName, appointmentDate)).AsTask();
        }

        public Task SendTutorAppointmentCancellationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorAppointmentCancellationAsync(tutorEmail, tutorName, studentName, appointmentDate)).AsTask();
        }

        public Task SendStudentAppointmentCancelledByTutorAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendStudentAppointmentCancelledByTutorAsync(studentEmail, studentName, tutorName, appointmentDate)).AsTask();
        }

        public Task SendTutorCancellationConfirmationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorCancellationConfirmationAsync(tutorEmail, tutorName, studentName, appointmentDate)).AsTask();
        }

        public Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType, string adminName)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendAdminCancelledAppointmentAsync(recipientEmail, recipientName, otherPersonName, appointmentDate, recipientType, adminName)).AsTask();
        }

        public Task SendReviewReminderAsync(string studentEmail, string studentName, string tutorName)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendReviewReminderAsync(studentEmail, studentName, tutorName)).AsTask();
        }

        public Task SendTutorAppointmentCompletionReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int hoursAfterEnd)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorAppointmentCompletionReminderAsync(tutorEmail, tutorName, studentName, appointmentDate, endDate, hoursAfterEnd)).AsTask();
        }

        public Task SendTutorAppointmentAdminFollowUpAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorAppointmentAdminFollowUpAsync(tutorEmail, tutorName, studentName, appointmentDate, endDate)).AsTask();
        }

        public Task SendAdminAppointmentFollowUpAsync(string adminEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int appointmentId)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendAdminAppointmentFollowUpAsync(adminEmail, tutorName, studentName, appointmentDate, endDate, appointmentId)).AsTask();
        }

        public Task SendTutorAppointmentRescheduledAsync(string tutorEmail, string tutorName, string studentName, DateTime newAppointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorAppointmentRescheduledAsync(tutorEmail, tutorName, studentName, newAppointmentDate)).AsTask();
        }

        public Task SendStudentAppointmentRescheduledAsync(string studentEmail, string studentName, string tutorName, DateTime newAppointmentDate)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendStudentAppointmentRescheduledAsync(studentEmail, studentName, tutorName, newAppointmentDate)).AsTask();
        }

        public Task SendStudentUpcomingAppointmentReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, int hoursBefore, string? tutorNotes)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendStudentUpcomingAppointmentReminderAsync(studentEmail, studentName, tutorName, appointmentDate, hoursBefore, tutorNotes)).AsTask();
        }

        public Task SendTutorUpcomingAppointmentReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, int hoursBefore)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorUpcomingAppointmentReminderAsync(tutorEmail, tutorName, studentName, appointmentDate, hoursBefore)).AsTask();
        }

        public Task SendGroupAppointmentConfirmationToStudentAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendGroupAppointmentConfirmationToStudentAsync(studentEmail, studentName, tutorName, appointmentDate, startTime, endTime)).AsTask();
        }

        public Task SendGroupAppointmentConfirmationToTutorAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendGroupAppointmentConfirmationToTutorAsync(tutorEmail, tutorName, studentName, appointmentDate, startTime, endTime)).AsTask();
        }

        public Task SendGroupAppointmentCancellationToStudentAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendGroupAppointmentCancellationToStudentAsync(studentEmail, studentName, tutorName, appointmentDate, startTime, endTime)).AsTask();
        }

        public Task SendGroupAppointmentCancellationToTutorAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendGroupAppointmentCancellationToTutorAsync(tutorEmail, tutorName, studentName, appointmentDate, startTime, endTime)).AsTask();
        }

        public Task SendStudentUpcomingGroupReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int hoursBefore)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendStudentUpcomingGroupReminderAsync(studentEmail, studentName, tutorName, appointmentDate, startTime, endTime, hoursBefore)).AsTask();
        }

        public Task SendTutorUpcomingGroupReminderAsync(string tutorEmail, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int hoursBefore, int enrolledCount)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorUpcomingGroupReminderAsync(tutorEmail, tutorName, appointmentDate, startTime, endTime, hoursBefore, enrolledCount)).AsTask();
        }

        public Task SendAdminCancelledGroupToStudentAsync(string studentEmail, string studentName, string tutorName, string adminName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendAdminCancelledGroupToStudentAsync(studentEmail, studentName, tutorName, adminName, groupId, appointmentDate, startTime, endTime)).AsTask();
        }

        public Task SendAdminCancelledGroupToTutorAsync(string tutorEmail, string tutorName, string adminName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int enrolledCount)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendAdminCancelledGroupToTutorAsync(tutorEmail, tutorName,  adminName, groupId, appointmentDate, startTime, endTime, enrolledCount)).AsTask();
        }

        public Task SendTutorGroupApprovedAsync(string tutorEmail, string tutorName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int capacity)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorGroupApprovedAsync(tutorEmail, tutorName, groupId, appointmentDate, startTime, endTime, capacity)).AsTask();
        }

        public Task SendTutorGroupRejectedAsync(string tutorEmail, string tutorName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int capacity)
        {
            return _queue.EnqueueAsync(sender =>
                sender.SendTutorGroupRejectedAsync(tutorEmail, tutorName, groupId, appointmentDate, startTime, endTime, capacity)).AsTask();
        }


    }
}
