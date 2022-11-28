using UnityEngine;

namespace Platinum.Settings
{
    public class GameSettings
    {
        // ROOM or SCENE

        public const string MAINMENU_SCENE = "NewMainMenu";
        public const string DEFAULT_GAME_SCENE = "NewMiningPlanet";

        public const byte MIN_TEAMS = 2;
        public const byte MAX_TEAMS = 4;
        public const byte MIN_PLAYERS_PER_TEAM = 1;
        public const byte MAX_PLAYERS_PER_TEAM = 10;

        public const int TEAM_SCORE_FOR_KILL = 1;
        public const int COUNT_KILLS_TO_TEAM_WIN = 5;
        public const int MAX_TIMES_MATCH_WIN = 300;

        // LEVEL

        public const float MATCH_RESPAWN_TIME = 3.0f;
        public const float LOOTS_RESPAWN_TIME = 15.0f;

        public const int MAX_COUNT_ON_SCENE = 5;
        public const float MIN_SPAWN_TIME = 5.0f;
        public const float MAX_SPAWN_TIME = 10.0f;

        public const float DISTANCE_OPEN_SELECT_WEAPON = 5f;
        public const int MAX_PAID_WEAPONS = 1;
        public const float TIME_TO_BLOCK_SELECTION = 15f;

        // PLAYER

        public const float PLAYER_PASSIVE_REGENERATION_AMOUNT = 10.0f;
        public const float PLAYER_PASSIVE_REGENERATION_INTERVAL = 10.0f;
        public const float PLAYER_INVULNERABLE_TIME = 1.0f;
        public const float PLAYER_RESPAWN_TIME = 2.0f;

        public const int PLAYER_COIN_FOR_KILL = 2;

        public const int PLAYER_SCORE_FOR_EXIT = 0;
        public const int PLAYER_SCORE_FOR_LOSE = 15;
        public const int PLAYER_SCORE_FOR_WIN = 50;
        public const int PLAYER_SCORE_FOR_KILL = 25;
        public const int PLAYER_MAX_LIVES = 5;

        public const string PLAYER_LIVES = "PlayerLives";
        public const string PLAYER_READY = "IsPlayerReady";
        public const string PLAYER_LOADED_LEVEL = "PlayerLoadedLevel";

        public static Color GetColor(int colorChoice)
        {
            switch (colorChoice)
            {
                case 0: return Color.red;
                case 1: return Color.green;
                case 2: return Color.blue;
                case 3: return Color.yellow;
                case 4: return Color.cyan;
                case 5: return Color.grey;
                case 6: return Color.magenta;
                case 7: return Color.white;
            }

            return Color.black;
        }
    }
}
