using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class ActorsManager : MonoBehaviour
    {
        public int PlayerAffiliation = 0;
        public List<Actor> PlayerActors { get; private set; }
        public List<Actor> Actors { get; private set; }

        private Dictionary<int, List<Actor>> PlayerTeams = new Dictionary<int, List<Actor>> { };

        public static ActorsManager Instance { get; private set; }

        public int GetRemainingPlayerEnemy()
        {
            return PlayerTeams[PlayerAffiliation + 1].Count;
        }
        
        public int GetEnemyAffiliation(int affiliation)
        {
            return GetEnemyActor(PlayerTeams[affiliation].FirstOrDefault()).Affiliation;
        }

        public List<Actor> GetEnemyActors(Actor actor)
        {
            List<Actor> enemyActors = new List<Actor>();
            for (int i = 0; i < actor.EnemysAffiliation.Count; i++)
            {
                if (PlayerTeams.ContainsKey(actor.EnemysAffiliation[i]))
                {
                    enemyActors.AddRange(
                        PlayerTeams[actor.EnemysAffiliation[i]]);
                }
            }

            return enemyActors;
        }
        public List<Actor> GetFriendlyActors(Actor actor)
        {
            List<Actor> FriendlyPlayers = PlayerTeams[actor.Affiliation];
            return FriendlyPlayers;
        }

        public Actor GetEnemyActor(Actor actor)
        {
            return GetEnemyActors(actor).FirstOrDefault();
        }

        public void AddActor(Actor actor)
        {
            if (actor.Affiliation == PlayerAffiliation) PlayerActors.Add(actor);
            Actors.Add(actor);
            if (!PlayerTeams.ContainsKey(actor.Affiliation)) PlayerTeams[actor.Affiliation] = new List<Actor> { };

            PlayerTeams[actor.Affiliation].Add(actor);
        }

        void Awake()
        {
            Instance = this;
            Actors = new List<Actor>();
            PlayerActors = new List<Actor>();
        }
    }
}
