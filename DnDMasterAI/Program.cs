using System;
using System.Text;
using DnDGame.Models;
using DnDGame.Services;

Console.OutputEncoding = Encoding.UTF8;
Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.Gray;
Console.Clear();

var loginService = new LoginService();
var player = loginService.ShowLoginMenu();
if (player == null)
    return;

var engine = new GameEngine(player);
engine.Run();
