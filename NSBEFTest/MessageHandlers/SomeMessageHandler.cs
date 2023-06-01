namespace NSBEFTest.MessageHandlers;

public class SomeMessageHandler : IHandleMessages<UpdateMyDataMessage>
{
    private readonly AppDbContext _dbContext;

    public SomeMessageHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public Task Handle(UpdateMyDataMessage message, IMessageHandlerContext context)
    {
        var record = _dbContext.Data.Single(d => d.Id == message.MyDataId);
        record.Status = MyDataStatus.Complete;
        return Task.CompletedTask;
    }
}

public class UpdateMyDataMessage : IMessage
{
    public int MyDataId { get; set; }
    
}