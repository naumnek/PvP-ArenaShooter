using System;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Platinum.Settings
{

    [Serializable]
    public struct MaxValueAttributes
    {
        public float MaxBullets;
        public float Damage;
        public float BulletSpeed;
        public float BulletSpreadAngle;
        public float BulletsPerShoot;
    }

    [Serializable]
    public struct PlayerSaves
    {
        public int Skin;
        public MusicInfo Music;
        public float MusicVolume;
        public float LookSensitivity;
        public Vector2 CrosshairPosition;
        public float CameraDistance;
        public ShadowQuality Shadows;
        public int Quality;
        public bool Framerate;
        public bool VisiblyTrailBullet;
    }

    [Serializable]
    public struct MatchSaves
    {
        public bool DisableInstanceHit;
        public bool PeacifulMode;
        public int Seed;
    }

    public enum DifficultyLevel
    {
        Default,
        Peaciful,
    }

    [CreateAssetMenu(menuName = "SettingsManager")]
    public class SettingsManager : ScriptableObject
    {
        public PlayerInfo PlayerInfo;

        public CustomizationInfo Customization;

        [SerializeField]
        private PlayerSaves DefaultPlayerSettings;

        [SerializeField]
        private MatchSaves DefaultMatchSettings;

        public MaxValueAttributes MaxWeaponAttributes;

        public Items[] ItemList;

        public MusicInfo[] MusicList;

        //
        private Items[] NotRequredItems;
        private Items[] RequredItems;

        private WeaponController[] RequredWeaponsList;
        //public WeaponController[] WeaponsList { get; private set; }

        public Room RequredRoom { get; private set; } = new Room();

        public PlayerSaves CurrentPlayerSaves { get; private set; }

        public MatchSaves CurrentMatchSaves { get; private set; }

        public PlayerSaves GetDefaultPlayerSaves()
        {
            return DefaultPlayerSettings;
        }
        public MatchSaves GetDefaultMatchSaves()
        {
            return DefaultMatchSettings;
        }

        public void CreatePlayer(PlayerInfo info)
        {
            PlayerInfo = info;
        }

        public void SetCurrentPlayerSaves(PlayerSaves settings)
        {
            CurrentPlayerSaves = settings;
        }

        public void SetCurrentMatchSaves(MatchSaves settings)
        {
            CurrentMatchSaves = settings;
        }

        public AudioClip[] GetMusic(SceneType sceneType, MusicType musicType)
        {
            List<AudioClip> music = new List<AudioClip> { };
            for (int i = 0; i < MusicList.Length; i++)
            {
                if (MusicList[i].SceneType == sceneType && MusicList[i].MusicType == musicType)
                {
                    music.Add(MusicList[i].Audio);
                }
            }
            return music.ToArray();
        }

        public WeaponController[] GetNotRequredWeapons()
        {
            NotRequredItems = GetNotRequredItems();
            RequredWeaponsList = new WeaponController[NotRequredItems.Length];
            for (int i = 0; i < NotRequredItems.Length; i++)
            {
                RequredWeaponsList[i] = NotRequredItems[i].Weapon;
                NotRequredItems[i].Reset();
            }
            return RequredWeaponsList;
        }
        public WeaponController GetPublicRandomWeapon()
        {
            Items[] currentItems = GetRequredItems().Where(i => !i.IsUnvisibly).ToArray();
            RequredWeaponsList = new WeaponController[currentItems.Length];
            for (int i = 0; i < currentItems.Length; i++)
            {
                RequredWeaponsList[i] = currentItems[i].Weapon;
                currentItems[i].Reset();
            }
            int indexWeapon = UnityEngine.Random.Range(0, RequredWeaponsList.Length);
            return RequredWeaponsList[indexWeapon];
        }
        public WeaponController[] GetRequredWeapons()
        {
            RequredItems = GetRequredItems();
            RequredWeaponsList = new WeaponController[RequredItems.Length];
            for (int i = 0; i < RequredItems.Length; i++)
            {
                RequredWeaponsList[i] = RequredItems[i].Weapon;
                RequredItems[i].Reset();
            }
            return RequredWeaponsList;
        }
        public Items[] GetRequredItems()
        {
            return ItemList.Where(i => !i.IsBlocked).ToArray();
        }
        public Items[] GetNotRequredItems()
        {
            return ItemList.Where(i => i.IsBlocked).ToArray();
        }

        public WeaponController[] ExceptNotRequredWeapon(WeaponController[] currentWeapon)
        {
            var weapons = currentWeapon.Except(GetNotRequredWeapons());
            WeaponController[] requredWeapon = weapons.ToArray();
            return requredWeapon;
        }

        public void ResetAllItemInfo()
        {
            foreach (Items item in ItemList)
            {
                item.Reset();
            }
        }

    }

    [Serializable]
    public class Room
    {
        public string Scene;
        public int CountTeams;
        public int PlayersPerTeam;

        public void CreateDefaultRoom()
        {
            Scene = GameSettings.DEFAULT_GAME_SCENE;
            CountTeams = GameSettings.MIN_TEAMS;
            PlayersPerTeam = GameSettings.MIN_PLAYERS_PER_TEAM;
        }
    }

    [Serializable]
    public class Items
    {
        [Header("Options Item")]
        public WeaponController Weapon;
        public bool IsUnvisibly;
        public bool IsBlocked;

        public string Name() => Weapon.WeaponName;
        public WeaponAttributes Attributes { get; private set; }

        public bool IsPaid { get; private set; }

        public bool RemovedFromList { get; private set; }

        public void OnPaid() => IsPaid = true;
        public void OnRemovedFromList() => RemovedFromList = true;

        public void Reset()
        {
            IsPaid = false;
            RemovedFromList = false;
            Attributes = new WeaponAttributes();
            Attributes.SetWeapon(Weapon);
        }
    }

    public class WeaponAttributes
    {
        public WeaponController Controller { get; private set; }

        public float MaxBullets { get; private set; }
        public float Damage { get; private set; }
        public float BulletSpeed { get; private set; }
        public float SpreadAngle { get; private set; }
        public float BulletsPerShoot { get; private set; }

        public void SetWeapon(WeaponController weapon)
        {
            ProjectileStandard projectile = weapon.ProjectilePrefab.GetComponent<ProjectileStandard>();

            MaxBullets = weapon.MaxBullets;
            Damage = projectile.Damage;
            BulletSpeed = projectile.Speed;
            SpreadAngle = weapon.BulletSpreadAngle;
            BulletsPerShoot = weapon.BulletsPerShot;
        }
    }

    public enum MusicType
    {
        Sad,
        Happy,
        Battle,
        Other,
    }
    public enum SceneType
    {
        Game,
        Menu,
    }

    [Serializable]
    public class MusicInfo
    {
        public string Name;
        public AudioClip Audio;
        public MusicType MusicType;
        public SceneType SceneType;
    }
    public enum SkinType
    {
        Free,
        NFT,
    }

    [Serializable]
    public class AvatarSkin
    {
        public string Name;
        public Material[] Materials;
    }

    [Serializable]
    public class CustomizationInfo
    {
        public Mesh[] FreeCharacterModels;
        public AvatarSkin[] FreeCharacterMaterials;
        public Mesh[] NFTCharacterModels;
        public AvatarSkin[] NFTCharacterMaterials;
        public SkinType Type { get; private set; } = SkinType.Free;

        public int CurrentIndexFreeModel { get; private set; } = 0;
        public int CurrentIndexFreeMaterial { get; private set; } = 0;
        public int CurrentIndexNFTModel { get; private set; } = 0;
        public int CurrentIndexNFTMaterial { get; private set; } = 0;

        private int IndexFreeModel = 0;
        private int IndexFreeMaterial = 0;
        private int IndexNFTModel = 0;
        private int IndexNFTMaterial = 0;

        private List<Mesh> AllModels = new List<Mesh>();
        private List<AvatarSkin> AllMaterials = new List<AvatarSkin>();

        public Mesh GetRandomModel()
        {
            if (AllModels.Count == 0)
            {
                AllModels.AddRange(FreeCharacterModels);
                AllModels.AddRange(NFTCharacterModels);
            }
            return AllModels[UnityEngine.Random.Range(0, AllModels.Count)];
        }
        public AvatarSkin GetRandomSkin()
        {
            if (AllMaterials.Count == 0)
            {
                AllMaterials.AddRange(FreeCharacterMaterials);
                AllMaterials.AddRange(NFTCharacterMaterials);
            }
            return AllMaterials[UnityEngine.Random.Range(0, AllMaterials.Count)];
        }

        public Mesh GetModel(SkinType type, int index)
        {
            switch (type)
            {
                case SkinType.Free:
                    return FreeCharacterModels[index];
                case SkinType.NFT:
                    return NFTCharacterModels[index];
            }
            return FreeCharacterModels[IndexFreeModel];
        }
        public AvatarSkin GetSkin(SkinType type, int index)
        {
            switch (type)
            {
                case SkinType.Free:
                    return FreeCharacterMaterials[index];
                case SkinType.NFT:
                    return FreeCharacterMaterials[index];
            }
            return FreeCharacterMaterials[IndexFreeMaterial];
        }

        public Mesh GetCurrentModel()
        {
            switch (Type)
            {
                case SkinType.Free:
                    return FreeCharacterModels[IndexFreeModel];
                case SkinType.NFT:
                    return NFTCharacterModels[IndexNFTModel];
            }
            return FreeCharacterModels[IndexFreeModel];
        }

        public AvatarSkin GetCurrentSkin()
        {
            switch (Type)
            {
                case SkinType.Free:
                    return FreeCharacterMaterials[IndexFreeMaterial];
                case SkinType.NFT:
                    return FreeCharacterMaterials[IndexNFTMaterial];
            }
            return FreeCharacterMaterials[IndexFreeMaterial];
        }

        public bool CheckIndexModel()
        {
            switch (Type)
            {
                case SkinType.Free:
                    return CurrentIndexFreeModel == IndexFreeModel;
                case SkinType.NFT:
                    return CurrentIndexNFTModel == IndexNFTModel;
            }
            return CurrentIndexFreeModel == IndexFreeModel;
        }

        public bool CheckIndexMaterial()
        {
            switch (Type)
            {
                case SkinType.Free:
                    return CurrentIndexFreeMaterial == IndexFreeMaterial;
                case SkinType.NFT:
                    return CurrentIndexNFTMaterial == IndexNFTMaterial;
            }
            return CurrentIndexFreeMaterial == IndexFreeMaterial;
        }

        public int GetCurrentIndexModel()
        {
            switch (Type)
            {
                case SkinType.Free:
                    return CurrentIndexFreeModel;
                case SkinType.NFT:
                    return CurrentIndexNFTModel;
            }
            return CurrentIndexFreeModel;
        }

        public int GetCurrentIndexSkin()
        {
            switch (Type)
            {
                case SkinType.Free:
                    return CurrentIndexFreeMaterial;
                case SkinType.NFT:
                    return CurrentIndexNFTMaterial;
            }
            return CurrentIndexFreeMaterial;
        }


        public void SetCharacterType(SkinType newType)
        {
            Type = newType;
        }

        public void SetCurrentIndexModel(int index)
        {
            switch (Type)
            {
                case SkinType.Free:
                    CurrentIndexFreeModel = index;
                    break;
                case SkinType.NFT:
                    CurrentIndexNFTModel = index;
                    break;
            }
        }

        public void SetCurrentIndexSkin(int index)
        {
            switch (Type)
            {
                case SkinType.Free:
                    CurrentIndexFreeMaterial = index;
                    break;
                case SkinType.NFT:
                    CurrentIndexNFTMaterial = index;
                    break;
            }
        }

        public void SetCurrentIndexModel()
        {
            switch (Type)
            {
                case SkinType.Free:
                    CurrentIndexFreeModel = IndexFreeModel;
                    break;
                case SkinType.NFT:
                    CurrentIndexNFTModel = IndexNFTModel;
                    break;
            }
        }

        public void SetCurrentIndexSkin()
        {
            switch (Type)
            {
                case SkinType.Free:
                    CurrentIndexFreeMaterial = IndexFreeMaterial;
                    break;
                case SkinType.NFT:
                    CurrentIndexNFTMaterial = IndexNFTMaterial;
                    break;
            }
        }

        public Mesh LeftModel()
        {
            switch (Type)
            {
                case SkinType.Free:
                    IndexFreeModel--;
                    if (IndexFreeModel < 0)
                        IndexFreeModel = FreeCharacterModels.Length - 1;
                    break;
                case SkinType.NFT:
                    IndexNFTModel--;
                    if (IndexNFTModel < 0)
                        IndexNFTModel = NFTCharacterModels.Length - 1;
                    return NFTCharacterModels[IndexNFTModel];
            }
            return FreeCharacterModels[IndexFreeModel];
        }
        public Mesh RightModel()
        {
            switch (Type)
            {
                case SkinType.Free:
                    IndexFreeModel++;
                    if (IndexFreeModel >= FreeCharacterModels.Length)
                        IndexFreeModel = 0;
                    return FreeCharacterModels[IndexFreeModel];
                case SkinType.NFT:
                    IndexNFTModel++;
                    if (IndexNFTModel >= NFTCharacterModels.Length)
                        IndexNFTModel = 0;
                    return NFTCharacterModels[IndexNFTModel];
            }
            return FreeCharacterModels[IndexFreeModel];
        }

        public AvatarSkin LeftMaterial()
        {
            switch (Type)
            {
                case SkinType.Free:
                    IndexFreeMaterial--;
                    if (IndexFreeMaterial < 0)
                        IndexFreeMaterial = FreeCharacterMaterials.Length - 1;
                    return FreeCharacterMaterials[IndexFreeMaterial];
                case SkinType.NFT:
                    IndexNFTMaterial--;
                    if (IndexNFTMaterial < 0)
                        IndexNFTMaterial = NFTCharacterMaterials.Length - 1;
                    return NFTCharacterMaterials[IndexNFTMaterial];
            }
            return FreeCharacterMaterials[IndexFreeMaterial];
        }
        public AvatarSkin RightMaterial()
        {
            switch (Type)
            {
                case SkinType.Free:
                    IndexFreeMaterial++;
                    if (IndexFreeMaterial >= FreeCharacterMaterials.Length)
                        IndexFreeMaterial = 0;
                    return FreeCharacterMaterials[IndexFreeMaterial];
                case SkinType.NFT:
                    IndexNFTMaterial++;
                    if (IndexNFTMaterial >= NFTCharacterMaterials.Length)
                        IndexNFTMaterial = 0;
                    return NFTCharacterMaterials[IndexNFTMaterial];
            }
            return FreeCharacterMaterials[IndexFreeMaterial];
        }
    }

    public class PlayerInfo
    {
        public string Name { get; private set; }
        public int Coin { get; private set; }
        public float Score { get; private set; } = 1;
        public float Level { get; private set; } = 1;
        public int CountKill { get; private set; } = 1;
        public float GetRatioScore() => Score / RequredScore;
        private float RequredScore => 100 * Level;

        public void SetName(string name)
        {
            Name = name;
        }
        public void AddCoin(int score)
        {
            Coin += score;
        }
        public void AddScore(float score)
        {
            Score += score;
            for (; Score >= RequredScore;)
            {
                Score -= RequredScore;
                Level++;
            }
        }
        public void AddCountKill(int score)
        {
            CountKill += score;
        }
    }
}
