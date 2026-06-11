using ChatClient.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChatClient.Controllers;

public sealed class HomeController(IConfiguration configuration) : Controller
{
    public IActionResult Index()
    {
        var model = new ChatPageViewModel
        {
            DefaultServerUrl = configuration["ChatServer:BaseUrl"] ?? "http://localhost:5050",
            DefaultDisplayName = "Ban"
        };

        return View(model);
    }
}
