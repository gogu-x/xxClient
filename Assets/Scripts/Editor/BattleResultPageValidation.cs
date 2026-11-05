using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XiuXian.Core;

namespace XiuXian.EditorTools
{
    /// <summary>Isolated result UI regression check; does not send requests or change player data.</summary>
    public static class BattleResultPageValidation
    {
        public static string Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Enter Play Mode before validating the battle result page.");

            var root = new GameObject("BattleResultValidation", typeof(RectTransform), typeof(Canvas));
            root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<Canvas>().enabled = false;
            try
            {
                VerifyResult(root.transform, BattleOutcome.AttackerWin, "试炼胜利", 2, 0);
                VerifyResult(root.transform, BattleOutcome.DefenderWin, "试炼未成", 0, 1);
                VerifyResult(root.transform, BattleOutcome.Draw, "势均力敌", 1, 1);
                VerifyDefaultPage(root.transform);
                return "PASS: victory/defeat/draw titles, survivor counts, retry and lobby callbacks; default page header/back preserved.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyResult(Transform parent, BattleOutcome outcome, string title,
            int ownSurvivors, int enemySurvivors)
        {
            var result = new BattleFinishedNtf { Outcome = outcome };
            AddUnits(result, BattleTeam.Attacker, ownSurvivors);
            AddUnits(result, BattleTeam.Defender, enemySurvivors);

            int retries = 0, returns = 0;
            var page = BattleResultPage.Show(parent, result, () => retries++, () => returns++);
            RequireText(page, "ResultTitle", title);
            RequireText(page, "StatLabel0", "我方存活");
            RequireText(page, "StatValue0", ownSurvivors.ToString());
            RequireText(page, "StatLabel1", "敌方存活");
            RequireText(page, "StatValue1", enemySurvivors.ToString());
            RequireText(page, "StatValue2", title);
            var retry = RequireButton(page, "Retry", "再次挑战");
            RequireButton(page, "BackToLobby", "返回大厅");
            retry.onClick.Invoke();
            Check(retries == 1 && returns == 0, outcome + ": retry must invoke only its own callback.");
            page.SetActive(false);

            var returnPage = BattleResultPage.Show(parent, result, () => retries++, () => returns++);
            RequireButton(returnPage, "BackToLobby", "返回大厅").onClick.Invoke();
            Check(retries == 1 && returns == 1, outcome + ": return must invoke only its own callback.");
            returnPage.SetActive(false);
        }

        private static void AddUnits(BattleFinishedNtf result, BattleTeam team, int survivors)
        {
            for (int i = 0; i < 2; i++)
                result.Units.Add(new BattleUnitResult
                {
                    InstanceId = "result-validation-" + team + "-" + i,
                    Team = team,
                    Hp = i < survivors ? 50 : 0,
                    MaxHp = 100,
                    Alive = i < survivors,
                });
        }

        private static void VerifyDefaultPage(Transform parent)
        {
            int returns = 0;
            var design = ImmortalV4PageKit.Page(parent, "DefaultPageValidation", "角色", () => returns++, out var page);
            RequireText(page, "Title", "角色");
            var backplate = design.Find("Back/QuietInkBackplate");
            Check(backplate != null && backplate.gameObject.activeInHierarchy &&
                backplate.GetComponent<QuietInkBackplate>() != null, "Default page must use the shared visible back button.");
            RequireButton(page, "Back", null).onClick.Invoke();
            Check(returns == 1, "Default page back callback must remain usable.");
        }

        private static void RequireText(GameObject page, string name, string expected)
        {
            foreach (var text in page.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == name)
                {
                    Check(text.gameObject.activeInHierarchy && text.text == expected,
                        name + ": expected visible text '" + expected + "', got '" + text.text + "'.");
                    return;
                }
            throw new InvalidOperationException("Missing result text: " + name);
        }

        private static Button RequireButton(GameObject page, string name, string expectedLabel)
        {
            foreach (var button in page.GetComponentsInChildren<Button>(true))
                if (button.name == name)
                {
                    Check(button.gameObject.activeInHierarchy && button.interactable, name + " must be available.");
                    if (expectedLabel != null)
                    {
                        var label = button.GetComponentInChildren<TMP_Text>();
                        Check(label != null && label.text == expectedLabel, name + " has an incorrect label.");
                    }
                    return button;
                }
            throw new InvalidOperationException("Missing result button: " + name);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
