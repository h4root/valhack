using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimAdminOverlay
{
    internal static class Progression
    {
        internal static int SkillLevel = 50;

        private static string _status = string.Empty;
        private static string[] _skillNames;

        internal static string Status => _status;

        internal static string[] SkillNames
        {
            get
            {
                if (_skillNames == null)
                    _skillNames = Enum.GetNames(typeof(Skills.SkillType))
                        .Where(n => n != "None" && n != "All")
                        .ToArray();

                return _skillNames;
            }
        }

        internal static void UnlockRecipes()
        {
            var player = Player.m_localPlayer;
            var db = ObjectDB.instance;

            if (player == null || db == null || db.m_recipes == null)
            {
                _status = "игрок или база предметов недоступны";
                return;
            }

            try
            {
                var addRecipe = AccessTools.Method(typeof(Player), "AddKnownRecipe");
                var added = 0;

                foreach (var recipe in db.m_recipes)
                {
                    if (recipe == null || recipe.m_item == null) continue;

                    addRecipe?.Invoke(player, new object[] { recipe });
                    player.AddKnownItem(recipe.m_item.m_itemData);
                    added++;
                }

                AccessTools.Method(typeof(Player), "UpdateKnownRecipesList")?.Invoke(player, null);
                _status = $"открыто рецептов: {added}";
            }
            catch (Exception e)
            {
                _status = "не удалось: " + e.Message;
                Log.Error("разблокировка рецептов: " + e);
            }
        }

        // CheatRaiseSkill поднимает навык НА величину, а не ДО неё, поэтому
        // сначала сбрасываем, затем поднимаем на нужный уровень.
        internal static void SetSkill(string skillName, int level)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                _status = "персонаж не загружен";
                return;
            }

            try
            {
                var skills = player.GetSkills();
                if (skills == null)
                {
                    _status = "навыки недоступны";
                    return;
                }

                skills.CheatResetSkill(skillName);

                if (level > 0)
                    skills.CheatRaiseSkill(skillName, level, false);

                _status = $"{skillName}: {level}";
            }
            catch (Exception e)
            {
                _status = "не удалось: " + e.Message;
                Log.Error("установка навыка: " + e);
            }
        }

        internal static void SetAllSkills(int level)
        {
            foreach (var name in SkillNames)
                SetSkill(name, level);

            _status = $"все навыки: {level}";
        }

        internal static IEnumerable<string> CurrentLevels()
        {
            var player = Player.m_localPlayer;
            var skills = player != null ? player.GetSkills() : null;
            if (skills == null) yield break;

            var list = skills.GetSkillList();
            if (list == null) yield break;

            foreach (var skill in list)
                yield return $"{skill.m_info.m_skill}  {skill.m_level:0}";
        }
    }
}
