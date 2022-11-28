using Platinum.Settings;
using System.Collections.Generic;
using TMPro;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class TeamsKillCounter : MonoBehaviour
    {
        public NotificationHUDManager NotificationHUDManager;

        [Tooltip("The text field displaying the team counter")]
        public TMP_Text PlayerNickname;
        public TMP_Text EnemyNickname;
        public Slider FriendlyKillCounter;
        public Slider EnemyKillCounter;
        public Color FriendlyTeamNotificationColor;
        public Color EnemyTeamNotificationColor;

        public List<int> TeamsKillScores { get; private set; }
        private int FriendlyAffiliation;
        private int EnemyAffiliation;
        private ActorsManager m_ActorsManager;
        private LoadManager m_LoadManager;
        private Actor m_Player;
        private Actor m_Enemy;
        private Actor m_DieActor;

        private void OnDestroy()
        {
            EventManager.RemoveListener<DieEvent>(OnDieEvent);
            EventManager.RemoveListener<EndSpawnEvent>(OnEndSpawnEvent);
            EventManager.RemoveListener<RefreshMatchEvent>(OnRefreshMatchEvent);
        }

        private void Awake()
        {
            TeamsKillScores = new List<int> { };
            FriendlyKillCounter.maxValue = GameSettings.COUNT_KILLS_TO_TEAM_WIN;
            EnemyKillCounter.maxValue = GameSettings.COUNT_KILLS_TO_TEAM_WIN;
            EventManager.AddListener<DieEvent>(OnDieEvent);
            EventManager.AddListener<EndSpawnEvent>(OnEndSpawnEvent);
            EventManager.AddListener<RefreshMatchEvent>(OnRefreshMatchEvent);
        }
        private void OnRefreshMatchEvent(RefreshMatchEvent evt)
        {
            m_DieActor = null;
        }

        private void OnEndSpawnEvent(EndSpawnEvent evt)
        {
            m_LoadManager = evt.LoadManager;
            m_ActorsManager = m_LoadManager.ActorsManager;

            switch (evt.LoadManager.TypeLevel)
            {
                case (TypeLevel.Arena):
                    InitArena();
                    break;
                case (TypeLevel.Generator):
                    break;
            }

        }

        private void InitArena()
        {
            m_Player = m_LoadManager.PlayerController.Actor;
            m_Enemy = m_LoadManager.EnemyController.Actor;

            PlayerNickname.text = m_Player.NickName;
            EnemyNickname.text = m_Enemy.NickName;

            //Fix int countTeams = PhotonNetwork.CurrentRoom.CountTeams + 1;
            int countTeams = m_Player.CountTeams + 1;
            for (int i = 0; i < countTeams; i++)
            {
                TeamsKillScores.Add(0);
            }

            FriendlyAffiliation = m_Player.Affiliation;
            EnemyAffiliation = m_Enemy.Affiliation;
        }


        public void OnDieEvent(DieEvent evt)
        {
            if (m_DieActor) return;
            m_DieActor = evt.Actor;
            Actor lastActor = m_ActorsManager.GetEnemyActor(m_DieActor);

            int team = lastActor.Affiliation;

            TeamsKillScores[team] += 1;
            FriendlyKillCounter.value = TeamsKillScores[FriendlyAffiliation];
            EnemyKillCounter.value = TeamsKillScores[EnemyAffiliation];

            /*
            if(team == FriendlyAffiliation)
                FriendlyCounter.text = TeamsKillScores[team].ToString();         
            else
                EnemyCounter.text = TeamsKillScores[team].ToString();

            NotificationHUDManager.OnTeamsKill(evt.killed.NickName + " killed by " + evt.killer.NickName,
                team == FriendlyAffiliation ? FriendlyTeamNotificationColor : EnemyTeamNotificationColor);
            */
        }
    }
}
