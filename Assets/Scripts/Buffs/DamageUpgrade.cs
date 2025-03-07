using GameManagement;
using Player;
using UnityEditor;

namespace Buffs
{
    public class DamageUpgrade : Upgrade
    {
        protected override void SetUpgrade()
        {
            PlayerEntity.Instance.UI.DisplayTooltip("You have collected a damage upgrade. Shots will do more damage.");
            for (int i = 0; i < PlayerEntity.Instance.damageUpgradesCollected.Length; i++)
            {
                if (LevelManager.Instance.damageUpgrades[i] == this)
                {
                    PlayerEntity.Instance.damageUpgradesCollected[i] = true;
                    break;
                }
            }
            PlayerEntity.Instance.Combat.IncreaseMaxDamage();
            Destroy(gameObject);
        }
    }
}
