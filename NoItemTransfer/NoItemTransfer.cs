using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace NoItemTransfer
{

    [BepInPlugin("wrac768.NoItemTransfer", "No Item Transfer", "1.0.0")]
    public class NoItemTransfer : BaseUnityPlugin
    {
        private static ConfigEntry<bool> only_new_players;

        const string save_dir = "NoItemTransfer_saves";
        const string new_character_items = "$item_torch,1\r\n$item_chest_rags,1";
        const string new_character_skill = "";

        static bool are_player_items_valid = false;
        static bool are_player_skills_valid = false;

        private readonly Harmony harmony = new Harmony("wrac768.NoItemTransfer");

        void Awake()
        {
            only_new_players = Config.Bind<bool>("General", "only_new_players", false, "Only allow new players to be created");

            harmony.PatchAll();
            Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, save_dir));
        }

        void OnDestoy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        class On_Spawned
        {
            static void Prefix()
            {
                are_player_items_valid = false;
                are_player_skills_valid = false;
                Player local_player = Player.m_localPlayer;

                bool player_files_exist = do_player_files_exist(local_player);
                bool new_player = is_new_player(local_player);

                if (player_files_exist)
                {
                    are_player_items_valid = validate_player_items(local_player);
                    are_player_skills_valid = validate_player_skills(local_player);

                    if (!are_player_items_valid || !are_player_skills_valid)
                    {
                        handle_logout();
                    }
                } 
                else if(!only_new_players.Value) // Existing Players
                {
                    write_player_files(local_player);
                } 
                else if (only_new_players.Value && new_player) // Only New Players
                {
                    write_player_files(local_player);
                }
                else
                {
                    handle_logout();
                }
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.SavePlayerProfile))]
        class Logout
        {
            static void Prefix()
            {
                if (are_player_items_valid && are_player_skills_valid)
                {
                    write_player_files(Player.m_localPlayer);
                }
            }
        }

        static void handle_logout()
        {
            Chat.instance.SendText(Talker.Type.Shout, "CHEATER!!! (ITEMS/SKILLS ARE NOT THE SAME) - LOGGING OUT!");
            System.Threading.Thread.Sleep(5000);
            Game.instance.Logout();
        }

        static void write_player_files(Player local_player)
        {
            are_player_items_valid = true;
            are_player_skills_valid = true;

            string player_items = generate_player_items(local_player);
            string player_item_save_path = Path.Combine(Environment.CurrentDirectory, save_dir, $"{local_player.GetPlayerName()}_items.csv");
            File.WriteAllText(player_item_save_path, player_items);

            string player_skills = generate_player_skills(local_player);
            string player_skills_save_path = Path.Combine(Environment.CurrentDirectory, save_dir, $"{local_player.GetPlayerName()}_skills.csv");
            File.WriteAllText(player_skills_save_path, player_skills);
        }

        static string generate_player_items (Player local_player)
        {
            List<string> player_items = new List<string>();
            List<ItemDrop.ItemData> all_items = local_player.GetInventory().GetAllItems();

            foreach (ItemDrop.ItemData item in all_items)
            {
                player_items.Add($"{item.m_shared.m_name},{item.m_stack}");
            }

            return string.Join(Environment.NewLine, player_items);
        }

        static bool validate_player_items(Player local_player)
        {
            string current_player_items = generate_player_items(local_player);

            string player_save_path = Path.Combine(Environment.CurrentDirectory, save_dir, $"{local_player.GetPlayerName()}_items.csv");
            string saved_player_items = System.IO.File.ReadAllText(player_save_path);

            return saved_player_items.Equals(current_player_items);
        }

        static string generate_player_skills(Player local_player)
        {
            List<string> player_skills = new List<string>();
            List<Skills.Skill> all_skills = local_player.GetSkills().GetSkillList();

            foreach (Skills.Skill skill in all_skills)
            {
                player_skills.Add($"{skill.m_info.m_skill},{skill.m_level}");
            }

            return string.Join(Environment.NewLine, player_skills);
        }

        static bool validate_player_skills(Player local_player)
        {
            string current_player_skills = generate_player_skills(local_player);

            string player_save_path = Path.Combine(Environment.CurrentDirectory, save_dir, $"{local_player.GetPlayerName()}_skills.csv");
            string saved_player_skills = System.IO.File.ReadAllText(player_save_path);

            return saved_player_skills.Equals(current_player_skills);
        }

        static bool do_player_files_exist(Player local_player)
        {
            bool player_items_exists = File.Exists(Path.Combine(Environment.CurrentDirectory, save_dir, $"{local_player.GetPlayerName()}_items.csv"));
            bool player_skills_exists = File.Exists(Path.Combine(Environment.CurrentDirectory, save_dir, $"{local_player.GetPlayerName()}_skills.csv"));

            return player_items_exists && player_skills_exists;
        }

        static bool is_new_player(Player local_player)
        {
            string current_player_skills = generate_player_skills(local_player);
            string current_player_items = generate_player_items(local_player);

            return new_character_skill.Equals(current_player_skills) && new_character_items.Equals(current_player_items);
        }
    }
}