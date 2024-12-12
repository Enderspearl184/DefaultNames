using SML;
using System;
using System.Collections.Generic;
using Services;
using UnityEngine;
using Server.Shared.State;
using Server.Shared.Info;
using Game.Simulation;
using HarmonyLib;
using System.IO;
using Newtonsoft.Json;
using Game.Interface;
using Game;
namespace DefaultNames;

[SML.DynamicSettings]
public class Settings
{
    public ModSettings.CheckboxSetting UseFullNames
    {
        get
        {
            ModSettings.CheckboxSetting UseFullNames = new()
            {
                Name = "Use Full Names",
                Description = "Use the FullNames list, instead of the lists of first and last names",
                DefaultValue = true,
                AvailableInGame = false,
                Available = true
            };
            return UseFullNames;
        }
    }
}

[Serializable]
public class NameList
{
    public List<string> FirstNames;
    public List<string> LastNames;
    public List<string> FullNames;
}

public class NameGenerator(NameList names)
{
    private readonly NameList Names = names;
    private readonly List<string> FullNames = new(names.FullNames);

    public string GetRandomName()
    {
        System.Random rand = new();
        if (SML.ModSettings.GetBool("Use Full Names"))
        {
            if (FullNames.Count == 0)
            {
                return "ERROR";
            }
            string name = FullNames[rand.Next(FullNames.Count)];
            FullNames.Remove(name);
            return name;
        }
        else
        {
            string FirstName = Names.FirstNames[rand.Next(Names.FirstNames.Count)];
            string LastName = Names.LastNames[rand.Next(Names.LastNames.Count)];
            return FirstName + " " + LastName;
        }
    }
}

public class CachedPlayerName(string name, int position)
{
    public readonly string name = name;
    public readonly int position = position;
}

[Mod.SalemMod]
public class DefaultNames
{
    static readonly string FolderPath = Path.GetDirectoryName(Application.dataPath) + "/SalemModLoader/ModFolders/DefaultNames/";
    static readonly string NamePath = FolderPath + "Names.json";
    public static NameGenerator generator;
    public readonly static NameList Names = new();
    public static bool isCurrentlyPickNames = false;

    public readonly static List<CachedPlayerName> cachedPlayerNames = [];
    public static void Start()
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }

        if (!File.Exists(NamePath))
        {
            StreamWriter NameFile = File.CreateText(NamePath);
            Names.FirstNames = new(["Cotton", "Deodat", "Edward", "Giles", "James", "James", "John", "John", "John", "Jonathan", "Samuel", "Samuel", "Thomas", "William", "William", "Abigail", "Alice", "Ann", "Ann", "Ann", "Betty", "Dorothy", "Lydia", "Martha", "Mary", "Mary", "Mary", "Sarah", "Sarah"]);
            Names.LastNames = new(["Mather", "Lawson", "Bishop", "Corey", "Bayley", "Russel", "Hathorne", "Proctor", "Willard", "Corwin", "Parris", "Sewall", "Danforth", "Hobbs", "Phips", "Hobbs", "Young", "Hibbins", "Putnam", "Sears", "Parris", "Good", "Dustin", "Corey", "Eastey", "Johnson", "Warren", "Bishop", "Good"]);
            Names.FullNames = new(["Cotton Mather", "Deodat Lawson", "Edward Bishop", "Giles Corey", "James Bayley", "James Russel", "John Hathorne", "John Proctor", "John Willard", "Jonathan Corwin", "Samuel Parris", "Samuel Sewall", "Thomas Danforth", "William Hobbs", "William Phips", "Abigail Hobbs", "Alice Young", "Ann Hibbins", "Ann Putnam", "Ann Sears", "Betty Parris", "Dorothy Good", "Lydia Dustin", "Martha Corey", "Mary Eastey", "Mary Johnson", "Mary Warren", "Sarah Bishop", "Sarah Good", "Sarah Wildes"]);
            NameFile.Write(JsonConvert.SerializeObject(Names, Formatting.Indented));
            NameFile.Close();
        }
        else
        {
            NameList temp = JsonConvert.DeserializeObject<NameList>(File.ReadAllText(NamePath));
            Names.FirstNames = temp.FirstNames;
            Names.LastNames = temp.LastNames;
            Names.FullNames = temp.FullNames;
        }
        
        Debug.Log(JsonConvert.SerializeObject(Names, Formatting.None));
    }
}

[HarmonyPatch(typeof(HudLobbyPreviousGamePanel), "OnPreviousGameInfoChanged")]
public class PreviousGamePatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix(PreviousGameData previousGameData)
    {
        try
        {
            GameObservations observations = Service.Game.Sim.simulation.observations;
            foreach (PreviousGameEntry entry in previousGameData.entries)
            {
                int existingPlayerIndex = DefaultNames.cachedPlayerNames.FindIndex((CachedPlayerName player) => player.position == entry.playerPosition);
                if (existingPlayerIndex != -1)
                {
                    entry.gameName = DefaultNames.cachedPlayerNames[existingPlayerIndex].name;
                }
                else if (entry.accountName != Service.Home.UserService.UserInfo.AccountName)
                {
                    Debug.LogWarning("DefaultNames was not able to find a CachedPlayerName!");
                }
            }
        } catch (Exception exception)
        {
            Debug.LogError("DefaultNames errored!");
            Debug.LogException(exception);
        }
    }
}

[HarmonyPatch(typeof(GameObservations), nameof(GameObservations.HandleDiscussionPlayerObservation))]
public class GameObservationsPatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix(DiscussionPlayerObservation discussionPlayerObservation)
    {
        try
        {
            GameObservations observations = Service.Game.Sim.simulation.observations;
            DiscussionPlayerState data = discussionPlayerObservation.Data;
            //make sure we aren't modifying ourselves, and that the packet actually includes an ingame name
            if (Service.Home.UserService.UserInfo.AccountName != data.accountName && data.gameName != "")
            {
                try
                {
                    DiscussionPlayerObservation existingObservation = observations.discussionPlayers.Find((DiscussionPlayerObservation player) => player.Data.position == data.position);
                    if (existingObservation != null && existingObservation.Data.gameName != "")
                    {
                        data.gameName = existingObservation.Data.gameName;
                    }
                    else
                    {
                        data.gameName = DefaultNames.generator.GetRandomName();

                        //also, check cachedplayernames and add it if it doesn't exist
                        if (DefaultNames.cachedPlayerNames.FindIndex((CachedPlayerName player) => player.position == data.position) == -1)
                        {
                            Debug.Log("Adding CachedPlayerName " + data.gameName);
                            DefaultNames.cachedPlayerNames.Add(new CachedPlayerName(data.gameName, data.position));
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("DefaultNames errored setting gameName!");
                    Debug.LogException(exception);
                    data.gameName = DefaultNames.generator.GetRandomName();
                    if (DefaultNames.cachedPlayerNames.FindIndex((CachedPlayerName player) => player.position == data.position) == -1)
                    {
                        Debug.Log("Adding CachedPlayerName " + data.gameName);
                        DefaultNames.cachedPlayerNames.Add(new CachedPlayerName(data.gameName, data.position));
                    }
                }
            }
        } catch (Exception exception)
        {
            Debug.LogError("DefaultNames errored!");
            Debug.LogException(exception);
        }
    }
}

[HarmonyPatch(typeof(PickNamesPanel), nameof(PickNamesPanel.Start))]
class PickNamesPatch
{
    public static void Prefix()
    {
        if (DefaultNames.generator == null)
        {
            Debug.Log("CREATING NAME GENERATOR AND CLEARING CACHED NAMES");
            DefaultNames.cachedPlayerNames.Clear();
            DefaultNames.generator = new NameGenerator(DefaultNames.Names);
        }
    }
}

[HarmonyPatch(typeof(GameSceneController), nameof(GameSceneController.HandleOnEnterActiveGame))]
[HarmonyPatch(typeof(GameSceneController), nameof(GameSceneController.HandleOnJoinLobby))]
class GameStartPatch
{
    public static void Prefix()
    {
        Debug.Log("DELETING NAME GENERATOR");
        DefaultNames.generator = null;
    }
}