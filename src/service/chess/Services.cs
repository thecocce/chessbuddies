using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ChessDotNet;
using Discord;
using Newtonsoft.Json;

namespace src
{
    public class ChessChallenge
    {
        [JsonIgnore]
        public IUser Challenger {get; set;}
        [JsonIgnore]
        public IUser Challenged {get; set;}
        public ulong ChallengerId { get { return Challenger.Id; } }
        public ulong ChallengedId { get { return Challenger.Id; } }
        public ulong Channel {get; set;}
        public DateTime ChallengeDate {get; set;}
    }
    public class ChessMatch
    {
        public ChessGame Game {get; set;}
        [JsonIgnore]
        public IUser Challenger {get; set;}
        [JsonIgnore]
        public IUser Challenged {get; set;}
        [JsonIgnore]
        public IUser[] Players { get { return new[] { Challenger, Challenged }; } }
        public ulong ChallengerId {get { return Challenger.Id; } }
        public ulong ChallengedId {get { return Challenger.Id; } }
        public ulong Channel {get; set;}
    }
    public interface IChessService
    {
        Task<ChessMove> Move(string rawMove);
        List<ChessChallenge> Challenges { get; }
        List<ChessMatch> Matches { get; }
        Task<ChessChallenge> Challenge(ulong channel, IUser player1, IUser player2);
        Task<ChessMatch> AcceptChallenge(ulong channel, IUser player);
    }
    public class ChessService : IChessService
    {
        public const int ChallengeTimeout = 30000;
        private List<ChessMatch> _chessMatches = new List<ChessMatch>();
        private List<ChessChallenge> _challenges = new List<ChessChallenge>();
        public List<ChessChallenge> Challenges { get { return _challenges; } }
        public List<ChessMatch> Matches { get { return _chessMatches; } }

        public async Task<ChessChallenge> Challenge(ulong channel, IUser player1, IUser player2)
        {
            if(await PlayerIsInGame(channel, player1))
                throw new ChessException($"{player1.Mention} is currently in a game.");

            if(await PlayerIsInGame(channel, player2))
                throw new ChessException($"{player2.Mention} is currently in a game.");

            if(_challenges.Any(x => x.Channel == channel && x.Challenged == player1 && x.Challenger == player2))
                throw new ChessException($"{player1.Mention} has already challenged {player2.Mention}.");

            var challenge = new ChessChallenge { ChallengeDate = DateTime.UtcNow, Channel = channel, Challenger = player1, Challenged = player2 };
            
            _challenges.Add(challenge);
            
            RemoveChallenge(challenge);

            return challenge;
        }

        public async Task<ChessMatch> AcceptChallenge(ulong channel, IUser player)
        {
            if(await PlayerIsInGame(channel, player))
                throw new ChessException($"{player.Mention} is currently in a game.");

            var challenge = _challenges.Where(x => x.Channel == channel && x.Challenged == player).OrderBy(x => x.ChallengeDate).FirstOrDefault();

            if(challenge == null)
                throw new ChessException($"No challenge exists for you to accept.");

            if(await PlayerIsInGame(channel, challenge.Challenger))
                throw new ChessException($"{challenge.Challenger.Mention} is currently in a game.");

            var chessGame = new ChessGame();
            var chessMatch = new ChessMatch { Channel = channel, Game = chessGame, Challenger = challenge.Challenger, Challenged = challenge.Challenged };

            _challenges.Remove(challenge);
            _chessMatches.Add(chessMatch);

            return await Task.FromResult<ChessMatch>(chessMatch);
        }

        public async Task<ChessMove> Move(string rawMove)
        {
            var move = rawMove.Replace(" ", "");

            if(!Regex.IsMatch(move, "[a-h][1-8][a-h][1-8]"))
                throw new ChessException("Error parsing move. Example move: a2a4");

            var sourceX = move[0];
            var sourceY = move[1];
            var destX = move[2];
            var destY = move[3];

            return await Task.FromResult(new ChessMove { Source = new Position { X = sourceX, Y = sourceY }, Destination = new Position { X = destX, Y = destY } });
        }

        private async Task<bool> PlayerIsInGame(ulong channel, IUser player)
        {
            return await Task.FromResult<bool>(_chessMatches.Any(x => x.Channel == channel && x.Players.Contains(player)));
        }

        private async void RemoveChallenge(ChessChallenge challenge)
        {
            await Task.Delay(ChallengeTimeout);

            if(_challenges.Contains(challenge))
                _challenges.Remove(challenge);
        }
    }    
}