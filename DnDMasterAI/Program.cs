
using DnDGame.Models;
using DnDGame.Services;

var player = Character.Load("Hero");
var engine = new GameEngine(player);
engine.Run();
