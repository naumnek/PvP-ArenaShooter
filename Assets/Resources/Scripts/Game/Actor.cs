using System;
using Platinum.Player;
using System.Collections.Generic;
using Platinum.Settings;
using Unity.FPS.AI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.FPS.Game
{
    // This class contains general information describing an actor (player or enemies).
    // It is mostly used for AI detection logic and determining if an actor is friend or foe
    public class Actor : MonoBehaviour
    {
        public string NickName { get; private set; } = "Alex";
        public int AmountKill { get; private set; }

        /// <summary>
        /// Represents the affiliation (or team) of the actor. Actors of the same affiliation are friendly to each other
        /// </summary>
        /// 

        [Tooltip("Represents point where other actors will aim when they attack this actor")]
        public Transform AimPoint;

        //public string[] ListNickName = new string[] {"Alex", "Steve", "Aloha", "LegalDepartment", "Bot1234" };

        public int Affiliation { get; private set; } = 0;
        public List<int> EnemysAffiliation { get; private set; } = new List<int> { };

        public Health Health { get; private set; }
        public EnemyController EnemyController { get; private set; }
        public PlayerController PlayerController { get; private set; }

        public int CountTeams { get; private set; }

        private ActorsManager m_ActorsManager;

        private void Awake()
        {
            m_ActorsManager = FindObjectOfType<ActorsManager>();
        }

        public void AddKill()
        {
            AmountKill++;
        }

        public int GetRandomEnemyAffilition()
        {
            return EnemysAffiliation[Random.Range(0, EnemysAffiliation.Count)];
        }

        public void OnHitEnemy(bool hit)
        {
            if (PlayerController)
            {
                PlayerController.LoadManager.PlayerWeaponsManager.OnHitEnemy(hit);
            }
            if (EnemyController)
            {
                EnemyController.EnemyMobile.OnHitEnemy(hit);
            }
        }

        public void SetAffiliation(EnemyController bot, SettingsManager settings, int Team, string Name)
        {
            EnemyController = GetComponent<EnemyController>();
            if (EnemyController == bot)
            {
                CountTeams = settings.RequredRoom.CountTeams;
                Affiliation = Team;
                NickName = Name;

                for (int i = 0; i < CountTeams; i++)
                {
                    if (i != Affiliation) EnemysAffiliation.Add(i);
                }
            }
            AddActor(bot);
        }

        public void SetAffiliation(PlayerController player, SettingsManager settings, int Team, string Name)
        {
            PlayerController = GetComponent<PlayerController>();
            if (PlayerController == player)
            {
                CountTeams = settings.RequredRoom.CountTeams;
                Affiliation = Team;
                NickName = Name;

                for (int i = 0; i < CountTeams; i++)
                {
                    if (i != Affiliation) EnemysAffiliation.Add(i);
                }
            }
            AddActor(player);
        }



        private void AddActor(EnemyController controller)
        {
            Health = controller.Health;

            if (m_ActorsManager)
            {
                m_ActorsManager.AddActor(this);
            }
        }

        private void AddActor(PlayerController controller)
        {
            Health = controller.Health;

            if (m_ActorsManager)
            {
                m_ActorsManager.AddActor(this);
            }
        }


        void OnDestroy()
        {
            // Unregister as an actor
            if (m_ActorsManager)
            {
                m_ActorsManager.Actors.Remove(this);
            }
        }
    }
}