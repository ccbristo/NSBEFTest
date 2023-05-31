using System.Diagnostics;
using System.Transactions;
using Microsoft.AspNetCore.Mvc;
using NSBEFTest.MessageHandlers;
using NSBEFTest.Models;

namespace NSBEFTest.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IMessageSession _messageSession;

    public HomeController(
        AppDbContext dbContext,
        IMessageSession messageSession)
    {
        _dbContext = dbContext;
        _messageSession = messageSession;
    }

    public async Task<IActionResult> Index()
    {
        // this shows the pattern i would like to be able to follow
        // create some data, 
        var myData = new MyData
        {
            Status = MyDataStatus.Pending
        };

        using var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        
        await _dbContext.Data.AddAsync(myData);
        
        await _dbContext.SaveChangesAsync(); // this allows us to get the real id for myData (db generated key)

        await _messageSession.Send(new UpdateMyDataMessage
        {
            MyDataId = myData.Id
        });
        
        await _dbContext.SaveChangesAsync();
        
        transactionScope.Complete();

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
