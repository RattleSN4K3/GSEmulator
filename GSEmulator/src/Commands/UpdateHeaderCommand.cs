﻿using System;
using GSEmulator.Model;

namespace GSEmulator.Commands
{
    class UpdateHeaderCommand : Command
    {
        private int mPlayerIdx;
        private string mKey;
        private string mValue;

        // player_u \00 <id> \00 <key> \00 <value> \0A
        public UpdateHeaderCommand(string[] tokens)
        {
            if (tokens.Length != 4)
                throw new ArgumentOutOfRangeException("UpdateHeaderCommand expects 4 tokens 'player_u <id> <key> <value>'");

            mPlayerIdx = Int32.Parse(tokens[1]);
            mKey = tokens[2];
            mValue = tokens[3];
        }

        public override void Execute(ref Server server)
        {
            var players = server.GetPlayers();

            if (players.Length > mPlayerIdx)
            {
                Player player = players[mPlayerIdx];

                for (int i = 0; i < Player.NumFields; i++)
                {
                    if (Player.GetFieldName(i) == mKey)
                    {
                        player.SetFieldValue(i, mValue);
                        break;
                    }
                }
            }
        }
    }
}
