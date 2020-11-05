﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Models;
using ELO.Preconditions;
using ELO.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ELO.Extensions;
using ELO.Services.Reactive;

namespace ELO.Modules
{
    [RequireContext(ContextType.Guild)]
    [RequirePermission(PermissionLevel.Registered)]
    public class GameInfo : ModuleBase<ShardedCommandContext>
    {
        private readonly ReactiveService _reactive;
        public GameService GameService { get; }

        public GameInfo(GameService gameService, ReactiveService reactive)
        {
            _reactive = reactive;
            GameService = gameService;
        }

        [Command("LastGame", RunMode = RunMode.Async)]
        [Alias("Last Game", "Latest Game", "LatestGame", "lg")]
        [Summary("Shows information about the most recent game in the current (or specified) lobby")]
        public virtual async Task LastGameAsync(SocketGuildChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketGuildChannel;
            }

            using (var db = new Database())
            {
                //return the result of the last game if it can be found.
                var lobby = db.Lobbies.Find(lobbyChannel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Specified channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GetLatestGame(lobby);
                if (game == null)
                {
                    await Context.SimpleEmbedAsync("Latest game is not available.", Color.Red);
                    return;
                }

                await DisplayGameAsync(game);
            }
        }

        public virtual async Task DisplayGameAsync(ManualGameResult game)
        {
            var embed = GameService.GetGameEmbed(game);
            await ReplyAsync(null, false, embed.Build());
        }

        public virtual async Task DisplayGameAsync(GameResult game)
        {
            var embed = GameService.GetGameEmbed(game);
            await ReplyAsync(null, false, embed.Build());
        }

        [Command("GameInfo", RunMode = RunMode.Async)]
        [Alias("Game Info", "Show Game", "ShowGame", "sg")]
        [Summary("Shows information about the specified game in the current (or specified) lobby")]
        public virtual async Task GameInfoAsync(SocketGuildChannel lobbyChannel, int gameNumber) //add functionality to specify lobby
        {
            await GameInfoAsync(gameNumber, lobbyChannel);
        }

        [Command("GameInfo", RunMode = RunMode.Async)]
        [Alias("Game Info", "Show Game", "ShowGame", "sg")]
        [Summary("Shows information about the specified game in the current (or specified) lobby")]
        public virtual async Task GameInfoAsync(int gameNumber, SocketGuildChannel lobbyChannel = null) //add functionality to specify lobby
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as SocketGuildChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.Lobbies.Find(lobbyChannel.Id);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Specified channel is not a lobby.", Color.Red);
                    return;
                }

                var game = db.GameResults.FirstOrDefault(x => x.LobbyId == lobby.ChannelId && x.GameId == gameNumber);
                if (game == null)
                {
                    await Context.SimpleEmbedAsync("Invalid Game Id.", Color.Red);
                    return;
                }

                await DisplayGameAsync(game);
            }
        }

        [Command("ManualGameInfo", RunMode = RunMode.Async)]
        [Alias("Manual Game Info", "Show Manual Game", "ShowManualGame", "smg")]
        [Summary("Shows information about the specified manual game")]
        public virtual async Task ManualGameInfoAsync(int gameNumber)
        {
            using (var db = new Database())
            {
                var game = db.ManualGameResults.FirstOrDefault(x => x.GuildId == Context.Guild.Id && x.GameId == gameNumber);
                if (game == null)
                {
                    await Context.SimpleEmbedAsync("Specified game number is invalid.", Color.Red);
                    return;
                }

                await DisplayGameAsync(game);
            }
        }

        [Command("ManualGameList", RunMode = RunMode.Async)]
        [Alias("Manual Game List", "ManualGamesList", "ShowManualGames", "ListManualGames")]
        [Summary("Displays statuses for the last 100 manual games in the server")]
        public virtual async Task ManualGameList()
        {
            using (var db = new Database())
            {
                var games = db.ManualGameResults.AsNoTracking().AsQueryable().Where(x => x.GuildId == Context.Guild.Id).OrderByDescending(x => x.GuildId).Take(100).ToList();

                if (games.Count == 0)
                {
                    await Context.SimpleEmbedAsync("There aren't any manual games in history.", Color.Blue);
                    return;
                }

                var gamePages = games.SplitList(5);
                var pages = new List<ReactivePage>();
                foreach (var page in gamePages)
                {
                    var content = page.Select(x =>
                    {
                        var scoreUpdates = db.ManualGameScoreUpdates.AsNoTracking().AsQueryable().Where(y => y.GuildId == Context.Guild.Id && y.ManualGameId == x.GameId).ToArray();
                        if (scoreUpdates.Length == 0) return null;

                        return new EmbedFieldBuilder()
                            .WithName($"#{x.GameId}: {x.GameState}")
                            .WithValue(string.Join("\n", scoreUpdates.Select(s => $"{MentionUtils.MentionUser(s.UserId)} - `{s.ModifyAmount}`") + $"\n **Submitted by: {MentionUtils.MentionUser(x.Submitter)}**"));
                    }).AsQueryable().Where(x => x != null).ToList();

                    if (content.Count == 0) continue;

                    pages.Add(new ReactivePage
                    {
                        Fields = content
                    });
                }

                await _reactive.SendPagedMessageAsync(Context, Context.Channel, new ReactivePager(pages)
                {
                    Color = Color.Blue
                }.ToCallBack().WithDefaultPagerCallbacks().WithJump());
            }
        }

        [Command("GameList", RunMode = RunMode.Async)]
        [Alias("Game List", "GamesList", "ShowGames", "ListGames")]
        [Summary("Displays statuses for the last 100 games in the lobby")]
        public virtual async Task GameListAsync(ISocketMessageChannel lobbyChannel = null)
        {
            if (lobbyChannel == null)
            {
                lobbyChannel = Context.Channel as ISocketMessageChannel;
            }

            using (var db = new Database())
            {
                var lobby = db.GetLobby(lobbyChannel);
                if (lobby == null)
                {
                    await Context.SimpleEmbedAsync("Specified channel is not a lobby.", Color.Red);
                    return;
                }

                var games = db.GameResults.AsNoTracking().AsQueryable().Where(x => x.GuildId == Context.Guild.Id && x.LobbyId == lobbyChannel.Id).OrderByDescending(x => x.GameId).Take(100);
                if (!games.Any())
                {
                    await Context.SimpleEmbedAsync("There aren't any games in history for the specified lobby.", Color.Blue);
                    return;
                }

                var gamePages = games.AsEnumerable().SplitList(20);
                var pages = new List<ReactivePage>();
                foreach (var page in gamePages)
                {
                    var content = page.Select(x =>
                    {
                        if (x.GameState == GameState.Decided)
                        {
                            return $"`#{x.GameId}:` Team {x.WinningTeam}";
                        }
                        return $"`#{x.GameId}:` {x.GameState}";
                    });
                    pages.Add(new ReactivePage
                    {
                        Description = string.Join("\n", content).FixLength(1023)
                    });
                }

                await _reactive.SendPagedMessageAsync(Context, Context.Channel, new ReactivePager(pages)
                {
                    Color = Color.Blue
                }.ToCallBack().WithDefaultPagerCallbacks().WithJump());
            }
        }
    }
}