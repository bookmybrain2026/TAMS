//connects QueuedEmailService.cs and SmtpEmailService.cs
//Stores pending email jobs in a bounded channel
using System.Threading.Channels;

namespace TAMS.Services;
public sealed class EmailQueue
{
    private readonly Channel<Func<IEmailService, Task>> _channel = Channel.CreateBounded<Func<IEmailService, Task>>(
            //Each item in the queue is a function that accepts an IEmailService and returns a Task
            new BoundedChannelOptions(1000) //queue can hold up to 1,000 pending jobs
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,   //allows multiple workers to consume jobs concurrently
                SingleWriter = false    //allows multiple controllers and other services to enqueue jobs concurrently
            });

    public ValueTask EnqueueAsync(Func<IEmailService, Task> job, CancellationToken cancellationToken = default)
        //puts a job into the queue, waiting if necessary for capacity
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public IAsyncEnumerable<Func<IEmailService, Task>> ReadAllAsync(CancellationToken cancellationToken)
        //lets workers asynchronously wait for the next available job
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

}
