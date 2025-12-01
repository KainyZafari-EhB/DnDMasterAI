using System;
using System.Text;
using DnDGame.Models;
using DnDGame.Services;

Console.OutputEncoding = Encoding.UTF8;
Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.Gray;
Console.Clear();

var player = Character.Load("Hero");
var engine = new GameEngine(player);
engine.Run();
